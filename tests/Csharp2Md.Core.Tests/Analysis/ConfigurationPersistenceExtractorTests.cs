using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Extraction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Analysis;
public sealed class ConfigurationPersistenceExtractorTests
{
    [Theory]
    [InlineData("config.GetSection(\"Orders\")", "section:Orders")]
    [InlineData("config.GetValue<string>(\"Orders:Timeout\")", "key:Orders:Timeout")]
    [InlineData("config.GetConnectionString(\"Orders\")", "key:Orders")]
    public void Extract_ConfigurationEmitsSafeStructuralNames(string call, string expected) => Assert.Contains(Extract($"class Cfg {{ public void GetSection(string x){{}} public T GetValue<T>(string x)=>default!; public void GetConnectionString(string x){{}} }} class C {{ void M(Cfg config) {{ {call}; }} }}").Entities, x => x.Kind == EntityKind.Configuration && x.DisplayName == expected);
    [Fact] public void Extract_ConfigValueNeverEntersGraph() { var r=Extract("class Cfg { public T GetValue<T>(string x)=>default!; } class C { void M(Cfg config) { config.GetValue<string>(\"Password\"); } }"); Assert.DoesNotContain(r.Entities, x=>x.DisplayName.Contains("secret",StringComparison.OrdinalIgnoreCase)); Assert.DoesNotContain(r.Evidence, x=>x.ContentDigest.Contains("secret",StringComparison.OrdinalIgnoreCase)); }
    [Theory]
    [InlineData("ctx.SaveChangesAsync()")]
    [InlineData("ctx.Orders.Add(new Order())")]
    [InlineData("ctx.Orders.ToList()")]
    public void Extract_PersistenceEmitsStoreAndOperation(string call) { var r=Extract($"class Order {{ }} class DbContext {{ public DbSet<Order> Orders {{get;}}=new(); public void SaveChangesAsync() {{}} }} class DbSet<T>{{ public void Add(T x){{}} public void ToList(){{}} }} class C {{ void M(DbContext ctx) {{ {call}; }} }}"); Assert.Contains(r.Entities,x=>x.Kind==EntityKind.DataStore); Assert.Contains(r.Entities,x=>x.Kind==EntityKind.DataOperation); Assert.NotEmpty(r.Relations); }
    [Fact] public void Extract_RelationsHaveEvidence() { var r=Extract("class DbContext { public void SaveChanges(){} } class C { void M(DbContext ctx) { ctx.SaveChanges(); } }"); Assert.All(r.Relations, x=>Assert.Contains(r.Evidence,e=>x.EvidenceCanonicalKeys.Contains(e.CanonicalKey))); }
    [Fact] public void Extract_SameInputIsOrdered() { var a=Extract("class Cfg { public void GetSection(string x){} } class C { void M(Cfg c){ c.GetSection(\"A\"); c.GetSection(\"B\"); } }"); var b=Extract("class Cfg { public void GetSection(string x){} } class C { void M(Cfg c){ c.GetSection(\"A\"); c.GetSection(\"B\"); } }"); Assert.Equal(a.Entities.Select(x=>x.CanonicalKey),b.Entities.Select(x=>x.CanonicalKey)); }
    private static ConfigurationPersistenceExtractionResult Extract(string source) { var tree=CSharpSyntaxTree.ParseText(source,path:"App.cs"); var compilation=CSharpCompilation.Create("app",[tree],[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)); var s=CanonicalIdentity.CreateSolution("app","App.sln"); var p=CanonicalIdentity.CreateProject(s,"App.csproj"); return ConfigurationPersistenceExtractor.Extract(new(s,p,CanonicalIdentity.CreateVariant("net10.0","Release",[],"ci"),compilation)); }
}
