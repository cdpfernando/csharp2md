namespace Csharp2Md.Core.Detection.Contracts;

internal interface IDocumentFactDetector
{
    DetectorDescriptor Descriptor { get; }

    DetectorResult Detect(DocumentDetectionContext context);
}

internal interface IProjectFactDetector
{
    DetectorDescriptor Descriptor { get; }

    DetectorResult Detect(ProjectDetectionContext context);
}
