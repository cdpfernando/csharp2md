namespace Csharp2Md.Analysis.Extraction;

internal interface IRegisteredContextDetector
{
    ObservationDraft? TryObserve(BoundOccurrence occurrence);
}
