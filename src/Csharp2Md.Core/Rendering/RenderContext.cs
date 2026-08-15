using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Rendering;

/// <summary>
/// Input to <see cref="MarkdownRenderer"/>. <paramref name="SemanticModel"/> is nullable on purpose:
/// rendering is syntax-driven and must succeed for projects that failed to restore (AD-002).
/// </summary>
public sealed record RenderContext(
    string RelativePath,
    SyntaxTree SyntaxTree,
    SemanticModel? SemanticModel = null);
