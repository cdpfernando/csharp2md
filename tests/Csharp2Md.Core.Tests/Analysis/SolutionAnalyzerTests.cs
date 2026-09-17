using Csharp2Md.Core.Analysis;
namespace Csharp2Md.Core.Tests.Analysis;
public sealed class SolutionAnalyzerTests
{
    [Fact] public void Assemble_ReturnsInMemoryGraph() { var (s,g)=Input(); var r=SolutionAnalyzer.Assemble(s,[g],2,1); Assert.Equal(s,r.Solution); Assert.Equal(2,r.Measurements.ExtractedCount); }
    [Fact] public void Assemble_OrdersEntitiesDeterministically() { var (s,g)=Input(); var r=SolutionAnalyzer.Assemble(s,[g,g],0,0); Assert.Equal(r.Entities.Select(x=>x.CanonicalKey).Order(StringComparer.Ordinal),r.Entities.Select(x=>x.CanonicalKey)); }
    [Fact] public void Assemble_DeduplicatesEvidence() { var(s,g)=Input(); Assert.Single(SolutionAnalyzer.Assemble(s,[g,g],0,0).Evidence); }
    [Fact] public void Assemble_DeduplicatesRelations() { var(s,g)=Input(); Assert.Single(SolutionAnalyzer.Assemble(s,[g,g],0,0).Relations); }
    [Fact] public void Assemble_PreservesMeasurements() { var(s,g)=Input(); var r=SolutionAnalyzer.Assemble(s,[g],9,4); Assert.Equal(4,r.Measurements.FilteredCount); }
    [Fact] public void Assemble_CancellationPropagates() { var(s,g)=Input(); using var c=new CancellationTokenSource(); c.Cancel(); Assert.Throws<OperationCanceledException>(()=>SolutionAnalyzer.Assemble(s,[g],0,0,c.Token)); }
    [Fact] public void Assemble_MismatchedSolutionHasCoordinates() { var(s,g)=Input(); var other=CanonicalIdentity.CreateSolution("other","Other.sln"); var e=Assert.Throws<SolutionAnalysisException>(()=>SolutionAnalyzer.Assemble(other,[g],0,0)); Assert.Equal(other,e.Solution); Assert.Equal("solution-mismatch",e.Cause); }
    [Fact] public void Assemble_DoesNotWriteFiles() { var(s,g)=Input(); var before=Directory.GetFiles(CoreTestPaths.RepoRoot,"*",SearchOption.TopDirectoryOnly); _=SolutionAnalyzer.Assemble(s,[g],0,0); Assert.Equal(before,Directory.GetFiles(CoreTestPaths.RepoRoot,"*",SearchOption.TopDirectoryOnly)); }
    [Fact] public void Assemble_OrdersRelations() { var(s,g)=Input(); var r=SolutionAnalyzer.Assemble(s,[g],0,0); Assert.Equal(r.Relations.Select(x=>x.CanonicalKey).Order(StringComparer.Ordinal),r.Relations.Select(x=>x.CanonicalKey)); }
    [Fact] public void Assemble_EmptyFragmentsProducesEmptyGraph() { var(s,_)=Input(); var r=SolutionAnalyzer.Assemble(s,[],0,0); Assert.Empty(r.Entities); Assert.Empty(r.Relations); }
    private static (SolutionIdentity,FactualGraph) Input() { var s=CanonicalIdentity.CreateSolution("app","App.sln"); var p=CanonicalIdentity.CreateProject(s,"App.csproj"); var v=CanonicalIdentity.CreateVariant("net10.0","Release",[],"ci"); var l=new LogicalLocator("App.cs",new SourceSpan(1,1,1,1),p); var e=new EvidenceRecord("evidence:a",CanonicalIdentity.CreateDocumentKey(s,"App.cs"),v,l.Span,"a"); var a=new LogicalEntity(EntityKind.Symbol,CanonicalIdentity.CreateEntityKey(s,EntityKind.Symbol,"A"),"A","A"); var b=new LogicalEntity(EntityKind.Symbol,CanonicalIdentity.CreateEntityKey(s,EntityKind.Symbol,"B"),"B","B"); return(s,new FactualGraph(s,[b,a],[new VariantOccurrence(a.CanonicalKey,p,v,l,"x",[e.CanonicalKey])],[e],[new FactualRelation("relation:a",a.CanonicalKey,b.CanonicalKey,"use",[e.CanonicalKey])],[],[],new ExtractionMeasurements(0,0))); }
}
