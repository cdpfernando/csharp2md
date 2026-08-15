using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Loading;

public sealed record LoadedService(Solution Solution, LoadReport Report);
