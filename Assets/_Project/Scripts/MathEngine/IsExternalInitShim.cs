// Polyfill for C# 9 `init`-only property setters, used by EvaluationOptions
// (contract math-engine.md 1.: "public bool AllowNegativeIntermediate { get;
// init; }"). Both compile targets for this assembly — the dotnet mirror
// solution (ADR-003, game/tests/, LangVersion 9.0, netstandard2.1) AND the
// real Unity Editor batch-mode compile (Unity 6000.3, confirmed by an actual
// test.ps1 -Full run, NOT assumed) — support the C# 9 LANGUAGE syntax but
// neither one's own BCL/api-compatibility profile defines the single marker
// type the compiler needs to accept it: System.Runtime.CompilerServices.
// IsExternalInit. Without this shim, Unity batch-mode fails with the exact
// same CS0518 as `dotnet test` ("Predefined type ... is not defined or
// imported") on EvaluationOptions.cs — this is NOT Unity-version-gated
// (an earlier `#if !UNITY_2021_2_OR_NEWER` guard here was wrong and was
// corrected after the full gate's own compiler log proved it), so the type
// is declared unconditionally, in both compile targets.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
