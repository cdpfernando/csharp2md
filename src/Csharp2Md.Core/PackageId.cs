namespace Csharp2Md.Core;

public readonly record struct PackageId(string Value)
{
    public override string ToString() => Value;
}
