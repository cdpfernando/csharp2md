using System.Reflection;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class SourceDocumentReaderPortTests
{
    private static readonly string[] ForbiddenNamespaces =
    [
        "Microsoft.CodeAnalysis",
        "Microsoft.Build",
        "System.Text.Json",
    ];

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void ISourceDocumentReader_Surface_ExposesNoRoslynJsonOrMsBuildType()
    {
        var exposed = typeof(ISourceDocumentReader)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(ExposedTypes)
            .Distinct()
            .ToArray();

        Assert.Contains(exposed, type => type == typeof(DocumentId));
        Assert.DoesNotContain(
            exposed,
            type => type.Namespace is not null
                && ForbiddenNamespaces.Any(ns =>
                    type.Namespace == ns || type.Namespace.StartsWith(ns + ".", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void ISourceDocumentReader_DeclaresTryReadAndDocumentsOfDomainIdentityTypes()
    {
        var tryRead = typeof(ISourceDocumentReader).GetMethod(nameof(ISourceDocumentReader.TryRead));
        Assert.NotNull(tryRead);
        Assert.Equal(typeof(bool), tryRead.ReturnType);

        var parameters = tryRead.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(DocumentId), parameters[0].ParameterType);
        Assert.Equal("document", parameters[0].Name);
        Assert.True(parameters[1].IsOut);
        Assert.Equal(typeof(ImmutableArray<byte>).MakeByRefType(), parameters[1].ParameterType);
        Assert.Equal("bytes", parameters[1].Name);

        var documents = typeof(ISourceDocumentReader).GetProperty(nameof(ISourceDocumentReader.Documents));
        Assert.NotNull(documents);
        Assert.Equal(typeof(ImmutableArray<DocumentId>), documents.PropertyType);
        Assert.NotNull(documents.GetMethod);
        Assert.Null(documents.SetMethod);
    }

    private static IEnumerable<Type> ExposedTypes(MemberInfo member) => member switch
    {
        MethodInfo method => Flatten(method.ReturnType)
            .Concat(method.GetParameters().SelectMany(parameter => Flatten(parameter.ParameterType))),
        PropertyInfo property => Flatten(property.PropertyType),
        _ => [],
    };

    private static IEnumerable<Type> Flatten(Type type)
    {
        var unwrapped = type;
        while (unwrapped.IsByRef || unwrapped.IsArray || unwrapped.IsPointer)
        {
            unwrapped = unwrapped.GetElementType()!;
        }

        yield return unwrapped;

        if (unwrapped.IsGenericType)
        {
            foreach (var argument in unwrapped.GetGenericArguments())
            {
                foreach (var nested in Flatten(argument))
                {
                    yield return nested;
                }
            }
        }
    }
}
