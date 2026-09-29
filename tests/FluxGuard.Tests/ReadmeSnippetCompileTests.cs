using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace FluxGuard.Tests;

/// <summary>
/// Compiles every <c>```csharp</c> block in README.md against the current assemblies. <see cref="ReadmeExamplesTests"/>
/// runs a few of the examples; a compiler checks all of them — the receiver, the arguments, the return types a block goes
/// on to use, and the namespaces it needs.
/// </summary>
/// <remarks>
/// A block is compiled as a top-level program: its <c>using</c> lines are hoisted, the implicit usings of a console
/// project are added, and the stand-ins below are declared when the block uses the name without declaring it — values a
/// reader already has from the surrounding text (the service collection, the user's message, an inner chat client), not
/// part of what the block shows. No library namespace is assumed: the README's blocks span three packages and a dozen
/// namespaces, so every block states the usings it needs and a block copied on its own compiles.
/// </remarks>
public class ReadmeSnippetCompileTests
{
    // A block that is deliberately not a program (a signature sketch, pseudocode) is listed here by the heading it sits
    // under, with the reason. Shrink this, never grow it silently.
    private static readonly Dictionary<string, string> Fragments = new(StringComparer.Ordinal);

    // The implicit usings of an SDK console project (ImplicitUsings=enable), nothing more.
    private const string CommonUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Net.Http;
        using System.Threading;
        using System.Threading.Tasks;
        """;

    private static readonly (string Name, string Declaration)[] StandIns =
    [
        ("services", "Microsoft.Extensions.DependencyInjection.IServiceCollection services = null!;"),
        ("serviceProvider", "System.IServiceProvider serviceProvider = null!;"),
        ("builder", "Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = null!;"),
        ("app", "Microsoft.AspNetCore.Builder.WebApplication app = null!;"),
        ("innerClient", "Microsoft.Extensions.AI.IChatClient innerClient = null!;"),
        ("userMessage", "string userMessage = \"\";"),
        ("retrievedText", "string retrievedText = \"\";"),
        ("myLlmService", "FluxGuard.Remote.Abstractions.IRemoteLlmService myLlmService = null!;"),
        ("llm", "ChatModel llm = new();"),
        ("alertService", "AlertService alertService = new();"),
    ];

    // The types of the value stand-ins above that are the reader's own (their model client, their alerting). Appended
    // after the body: a top-level program declares its types after its statements.
    private static readonly (string Name, string Declaration)[] TypeStandIns =
    [
        ("ChatModel", """
            public class ChatModel
            {
                public Task<string> CompleteAsync(string prompt) => Task.FromResult("");
            }
            """),
        ("AlertService", """
            public class AlertService
            {
                public Task NotifyAsync(FluxGuard.Core.GuardResult result) => Task.CompletedTask;
            }
            """),
    ];

    private static readonly string[] AssembliesToLoad =
    [
        "FluxGuard", "FluxGuard.Remote", "FluxGuard.SDK",
        "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.AI", "Microsoft.Extensions.AI.Abstractions",
        "Microsoft.AspNetCore",
    ];

    public static TheoryData<string> Blocks()
    {
        var data = new TheoryData<string>();
        foreach (var block in ReadBlocks())
            data.Add(block.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var block = ReadBlocks().Single(b => b.Key == key);
        if (Fragments.ContainsKey(block.Heading))
            return;

        var errors = Compile(block.Code);

        Assert.True(errors.IsEmpty,
            $"README block {key} does not compile against the current API:\n" +
            string.Join("\n", errors.Select(e => e.ToString())) + "\n--- source ---\n" + Program(block.Code));
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings()
    {
        var blocks = ReadBlocks();
        Assert.True(blocks.Count >= 15, $"expected the README's C# blocks, found {blocks.Count}");
        Assert.All(Fragments.Keys, heading => Assert.Contains(blocks, b => b.Heading == heading));
    }

    /// <summary>Positive control: the compiler rejects a call the library does not have.</summary>
    [Fact]
    public void Compile_RejectsAMethodTheLibraryDoesNotHave()
    {
        var errors = Compile("""
            using FluxGuard.Extensions;
            services.AddFluxGuardThatDoesNotExist();
            """);

        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// Positive control for "no library namespace is assumed": the same line compiles with its using and fails without it.
    /// </summary>
    [Fact]
    public void Compile_RejectsALibraryTypeWithoutItsUsing()
    {
        Assert.Empty(Compile("""
            using FluxGuard.Core;
            var mode = FailMode.Closed;
            """));
        Assert.NotEmpty(Compile("""
            var mode = FailMode.Closed;
            """));
    }

    private sealed record Block(string Key, string Heading, string Code);

    private static List<Block> ReadBlocks()
    {
        var lines = File.ReadAllText(Path.Combine(RepoRoot(), "README.md")).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var blocks = new List<Block>();
        var heading = "(top)";
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
                heading = lines[i].TrimStart('#').Trim();
            if (lines[i].Trim() != "```csharp")
                continue;

            var start = i + 1;
            var code = new StringBuilder();
            for (i++; i < lines.Length && lines[i].Trim() != "```"; i++)
                code.AppendLine(lines[i]);
            blocks.Add(new Block($"README.md line {start}: {heading}", heading, code.ToString()));
        }

        return blocks;
    }

    private static string Program(string code)
    {
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        // "using X;   // what it is for" is a directive too: the README annotates its usings.
        static string Code(string l) => l.Split("//", 2)[0].TrimEnd();
        bool IsUsingDirective(string l) =>
            l.StartsWith("using ", StringComparison.Ordinal) && Code(l).EndsWith(';') && !l.StartsWith("using var ", StringComparison.Ordinal);

        var body = string.Join("\n", lines.Where(l => !IsUsingDirective(l)));
        var standIns = StandIns
            .Where(s => Regex.IsMatch(body, $@"\b{s.Name}\b")
                        && !Regex.IsMatch(body, $@"\b(var|[A-Z][\w<>?,\s]*)\s+{s.Name}\s*[=;]"))
            .ToList();

        var typeStandIns = TypeStandIns
            .Where(s => (Regex.IsMatch(body, $@"\b{s.Name}\b") || standIns.Any(v => v.Declaration.StartsWith(s.Name + " ", StringComparison.Ordinal)))
                        && !Regex.IsMatch(body, $@"\bclass\s+{s.Name}\b"))
            .Select(s => s.Declaration);

        return string.Join("\n", lines.Where(IsUsingDirective)) + "\n" + CommonUsings + "\n"
               + string.Join("\n", standIns.Select(s => s.Declaration)) + "\n" + body + "\n" + string.Join("\n", typeStandIns);
    }

    private static ImmutableArray<Diagnostic> Compile(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(Program(code), new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "ReadmeSnippet", [tree], References(),
            new CSharpCompilationOptions(
                // A block that only declares types (a guard, a service) is a library, not a program.
                tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax>().Any()
                    ? OutputKind.ConsoleApplication
                    : OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        return compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
    }

    private static List<MetadataReference> References()
    {
        foreach (var name in AssembliesToLoad)
            Assembly.Load(name);

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
            paths.UnionWith(trusted.Split(Path.PathSeparator).Where(p => p.Length > 0));
        paths.UnionWith(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.Location.Length > 0)
            .Select(a => a.Location));
        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FluxGuard.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("FluxGuard.slnx not found above the test output directory");
    }
}
