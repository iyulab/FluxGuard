using System.Reflection;
using Iyu.Conventions.Testing.Snippets;
using Xunit;

namespace FluxGuard.Tests;

/// <summary>
/// Compiles every C# block in README.md against the current assemblies. <see cref="ReadmeExamplesTests"/> runs a few of
/// the examples; a compiler checks all of them — the receiver, the arguments, the return types a block goes on to use, and
/// the namespaces it needs.
/// </summary>
/// <remarks>
/// Each block is compiled as a top-level program by <see cref="SnippetCompiler"/>: its own usings, the implicit usings of a
/// console project, and the stand-ins below when the block uses the name without declaring it — values a reader already
/// has from the surrounding text (the service collection, the user's message, an inner chat client), not part of what the
/// block shows. No library namespace is assumed: the README's blocks span three packages and a dozen namespaces, so every
/// block states the usings it needs and a block copied on its own compiles.
/// </remarks>
public class ReadmeSnippetCompileTests
{
    // A block that is deliberately not a program (a signature sketch, pseudocode), by the heading it sits under, with the
    // reason. Shrink this, never grow it silently.
    private static readonly SnippetRoster Fragments = new(new Dictionary<string, string>(StringComparer.Ordinal));

    private static readonly Dictionary<string, string> StandIns = new(StringComparer.Ordinal)
    {
        ["services"] = "Microsoft.Extensions.DependencyInjection.IServiceCollection services = null!;",
        ["serviceProvider"] = "System.IServiceProvider serviceProvider = null!;",
        ["builder"] = "Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = null!;",
        ["app"] = "Microsoft.AspNetCore.Builder.WebApplication app = null!;",
        ["innerClient"] = "Microsoft.Extensions.AI.IChatClient innerClient = null!;",
        ["userMessage"] = "string userMessage = \"\";",
        ["retrievedText"] = "string retrievedText = \"\";",
        ["myLlmService"] = "FluxGuard.Remote.Abstractions.IRemoteLlmService myLlmService = null!;",
        ["llm"] = "ChatModel llm = new();",
        ["alertService"] = "AlertService alertService = new();",

        // The types of the value stand-ins above that are the reader's own (their model client, their alerting).
        ["ChatModel"] = """
            public class ChatModel
            {
                public Task<string> CompleteAsync(string prompt) => Task.FromResult("");
            }
            """,
        ["AlertService"] = """
            public class AlertService
            {
                public Task NotifyAsync(FluxGuard.Core.GuardResult result) => Task.CompletedTask;
            }
            """,
    };

    private static readonly SnippetCompiler Compiler = new(SnippetCompiler.ReferencesFor(
        [.. new[]
        {
            "FluxGuard", "FluxGuard.Remote", "FluxGuard.SDK",
            "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.AI", "Microsoft.Extensions.AI.Abstractions",
            "Microsoft.AspNetCore",
        }.Select(Assembly.Load)]))
    {
        // The implicit usings of an SDK console project (ImplicitUsings=enable), nothing more: the kit's default.
        StandIns = StandIns,
    };

    private static readonly IReadOnlyList<DocSnippet> Snippets = DocSnippets.Read(Path.Combine(RepoRoot(), "README.md"), "README.md");

    public static TheoryData<string> Blocks() => new(Snippets.Select(s => s.Key));

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var snippet = Snippets.Single(s => s.Key == key);
        if (!Fragments.IsFragment(snippet))
            Compiler.ShouldCompile(snippet);
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings() => Fragments.ShouldMatch([.. Snippets], atLeast: 15);

    /// <summary>Positive control: the compiler rejects a call the library does not have.</summary>
    [Fact]
    public void Compile_RejectsAMethodTheLibraryDoesNotHave()
    {
        var errors = Compiler.Compile("""
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
        Assert.Empty(Compiler.Compile("""
            using FluxGuard.Core;
            var mode = FailMode.Closed;
            """));
        Assert.NotEmpty(Compiler.Compile("""
            var mode = FailMode.Closed;
            """));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FluxGuard.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("FluxGuard.slnx not found above the test output directory");
    }
}
