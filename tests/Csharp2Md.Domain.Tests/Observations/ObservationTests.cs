using System.Collections;
using System.Reflection;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Tests.Observations;

public sealed class ObservationTests
{
    private static readonly FactReference ValidOwner = new(FactIdGrammar.Create("widget", ("name", "value")), "widget");

    private static readonly NormalizedPayload ValidPayload = NormalizedPayload.Create([]);

    private static readonly EvidenceLocator ValidLocator = new(DocumentId.Create("doc"), "file.cs", new SourceSpan(1, 1, 1, 5));

    private static readonly BindingDiagnostic ValidDiagnostic = new("BIND001", "Bound successfully.");

    private static readonly DocumentHash ValidDocumentHash = DocumentHash.Create(new string('a', 64));

    private static readonly ExtractorVersion ValidExtractorVersion = new(1);

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Create_AllEightComponentsPresent_ProducesAnObservation()
    {
        var observation = Observation.Create(
            ValidOwner, ObservationKind.Invocation, ValidPayload, 1,
            ValidLocator, EvidenceMethod.Semantic, ValidDiagnostic, ValidDocumentHash, ValidExtractorVersion);

        Assert.Equal(ObservationKind.Invocation, observation.Identity.Kind);
        Assert.Equal(EvidenceMethod.Semantic, observation.ExtractionMethod);
        Assert.Equal(ValidDocumentHash, observation.DocumentHash);
        Assert.Equal(ValidExtractorVersion, observation.ExtractorVersion);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Create_MissingOwner_IsRejectedNamingOwner()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            default, ObservationKind.Invocation, ValidPayload, 1,
            ValidLocator, EvidenceMethod.Semantic, ValidDiagnostic, ValidDocumentHash, ValidExtractorVersion));

        Assert.Equal("owner", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Create_UndefinedKind_IsRejectedNamingKind()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            ValidOwner, (ObservationKind)(-1), ValidPayload, 1,
            ValidLocator, EvidenceMethod.Semantic, ValidDiagnostic, ValidDocumentHash, ValidExtractorVersion));

        Assert.Equal("kind", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Create_MissingPayload_IsRejectedNamingPayload()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            ValidOwner, ObservationKind.Invocation, default, 1,
            ValidLocator, EvidenceMethod.Semantic, ValidDiagnostic, ValidDocumentHash, ValidExtractorVersion));

        Assert.Equal("payload", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Create_MissingLocator_IsRejectedNamingLocator()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            ValidOwner, ObservationKind.Invocation, ValidPayload, 1,
            default, EvidenceMethod.Semantic, ValidDiagnostic, ValidDocumentHash, ValidExtractorVersion));

        Assert.Equal("locator", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Create_UndefinedExtractionMethod_IsRejectedNamingExtractionMethod()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            ValidOwner, ObservationKind.Invocation, ValidPayload, 1,
            ValidLocator, (EvidenceMethod)(-1), ValidDiagnostic, ValidDocumentHash, ValidExtractorVersion));

        Assert.Equal("extractionMethod", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Create_MissingDiagnostic_IsRejectedNamingDiagnostic()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            ValidOwner, ObservationKind.Invocation, ValidPayload, 1,
            ValidLocator, EvidenceMethod.Semantic, default, ValidDocumentHash, ValidExtractorVersion));

        Assert.Equal("diagnostic", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-39")]
    public void Create_MissingDocumentHash_IsRejectedNamingDocumentHash()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            ValidOwner, ObservationKind.Invocation, ValidPayload, 1,
            ValidLocator, EvidenceMethod.Semantic, ValidDiagnostic, default, ValidExtractorVersion));

        Assert.Equal("documentHash", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-39")]
    public void Create_MissingExtractorVersion_IsRejectedNamingExtractorVersion()
    {
        var exception = Assert.Throws<ArgumentException>(() => Observation.Create(
            ValidOwner, ObservationKind.Invocation, ValidPayload, 1,
            ValidLocator, EvidenceMethod.Semantic, ValidDiagnostic, ValidDocumentHash, default));

        Assert.Equal("extractorVersion", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-40")]
    public void ExposedSurface_DoesNotContainAMicrosoftCodeAnalysisType()
    {
        var offendingType = ExposedMemberTypes(typeof(Observation))
            .FirstOrDefault(type => type.Namespace?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true);

        Assert.True(offendingType is null, $"'{offendingType?.FullName}' is a Roslyn type exposed by Observation.");
    }

    [Fact]
    [Trait("Requirement", "TAX-41")]
    public void Observation_HasNoSettableProperty()
    {
        var settableProperty = typeof(Observation)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.SetMethod is not null);

        Assert.True(settableProperty is null, $"'{settableProperty?.Name}' has a setter.");
    }

    [Fact]
    [Trait("Requirement", "TAX-41")]
    public void Observation_IsNotARecord_SoNoWithExpressionCanBypassCreatesValidation()
    {
        var cloneMethod = typeof(Observation).GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Null(cloneMethod);
    }

    [Fact]
    [Trait("Requirement", "TAX-41")]
    public void Observation_HasNoMutableCollectionMember()
    {
        var mutableCollectionProperty = typeof(Observation)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.PropertyType.IsArray || typeof(IList).IsAssignableFrom(property.PropertyType));

        Assert.True(mutableCollectionProperty is null, $"'{mutableCollectionProperty?.Name}' exposes a mutable collection.");
    }

    private static IEnumerable<Type> ExposedMemberTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var candidates = type.GetProperties(flags).Select(property => property.PropertyType)
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetMethods(flags).SelectMany(method => new[] { method.ReturnType }.Concat(method.GetParameters().Select(p => p.ParameterType))));

        return candidates.SelectMany(Flatten);
    }

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
