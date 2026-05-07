using System.Reflection;
using System.Text.RegularExpressions;
using FluxGuard.Remote.Guards;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FluxGuard.Remote.Tests;

// Operator log pipelines (grep / Loki / Elastic) and international support require
// English-only log messages. Korean tokens land as opaque tokens in Latin-tokenized
// indexes and require UTF-8-aware regex from operators. See CLAUDE.md logging conventions.
//
// Note: [Fact] instead of [Theory]+[MemberData] because FluxGuard.Remote uses Roslyn
// source-generated regex types that cause GetTypes() to return null entries on some
// platforms; parameterized xUnit tests with null type parameters trigger an unresolvable
// "unknown test case" failure in xunit.runner.visualstudio regardless of test outcome.
public class LogLanguageConventionTests
{
    private static readonly Regex HangulRegex = new(@"[가-힣ᄀ-ᇿ㄰-㆏]");

    [Fact]
    public void LoggerMessageAttributes_HaveAsciiOnlyMessages()
    {
        var assembly = typeof(L3HallucinationGuard).Assembly;
        var offenders = SafeGetTypes(assembly)
            .Where(t => !t.IsCompilerGenerated())
            .SelectMany(type =>
                type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(m => (Method: m, Attr: m.GetCustomAttribute<LoggerMessageAttribute>(), Type: type))
                    .Where(x => x.Attr is not null && !string.IsNullOrEmpty(x.Attr.Message) && HangulRegex.IsMatch(x.Attr.Message))
                    .Select(x => $"  {type.Name}.{x.Method.Name}: {x.Attr!.Message}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Found Korean text in [LoggerMessage] attributes — log messages must be English-only:\n"
            + string.Join("\n", offenders));
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes().OfType<Type>(); }
        catch (ReflectionTypeLoadException e) { return e.Types.OfType<Type>(); }
    }
}

internal static class TypeReflectionExtensions
{
    public static bool IsCompilerGenerated(this Type type)
        => type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is not null;
}
