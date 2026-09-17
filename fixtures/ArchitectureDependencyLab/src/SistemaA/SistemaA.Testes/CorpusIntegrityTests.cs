using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace SistemaA.Testes;

public class CorpusIntegrityTests
{
    private static string ObterDiretorioRaiz()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "oracle", "project-references.json")) &&
                File.Exists(Path.Combine(dir, "oracle", "scenarios.json")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Raiz do repositório contendo a pasta 'oracle' não encontrada.");
    }

    private record ProjectReferenceOracleItem(string Solution, string SourceProject, string TargetProject);
    private record ScenarioOracleItem(string Id, string Kind, string ExpectedState);

    [Fact]
    public void Teste1_CarregarTodosOsCsprojECompararProjectReferencesExatas()
    {
        var raiz = ObterDiretorioRaiz();
        var jsonOraclePath = Path.Combine(raiz, "oracle", "project-references.json");
        var oracleItems = JsonSerializer.Deserialize<List<ProjectReferenceOracleItem>>(
            File.ReadAllText(jsonOraclePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        )!;

        var csprojFiles = Directory.GetFiles(raiz, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\") && !f.Contains(@"/bin/") && !f.Contains(@"/obj/"))
            .ToList();

        var relacoesReais = new List<(string Source, string Target)>();

        foreach (var csproj in csprojFiles)
        {
            var doc = XDocument.Load(csproj);
            var sourceName = Path.GetFileNameWithoutExtension(csproj);

            var projectReferences = doc.Descendants("ProjectReference")
                .Select(pr => pr.Attribute("Include")?.Value)
                .Where(val => !string.IsNullOrEmpty(val))
                .Select(val => val!)
                .ToList();

            foreach (var pr in projectReferences)
            {
                var targetName = Path.GetFileNameWithoutExtension(pr);
                relacoesReais.Add((sourceName, targetName));
            }
        }

        // Deve haver exatamente 87 referências reais
        Assert.Equal(87, relacoesReais.Count);
        Assert.Equal(87, oracleItems.Count);

        var oracleSet = oracleItems.Select(o => $"{o.SourceProject}->{o.TargetProject}").ToHashSet();
        var reaisSet = relacoesReais.Select(r => $"{r.Source}->{r.Target}").ToHashSet();

        Assert.Equal(oracleSet, reaisSet);
    }

    [Fact]
    public void Teste2_VerificarQueTodoIdDoOraculoApareceEmAoMenosUmMarcadorScenario()
    {
        var raiz = ObterDiretorioRaiz();
        var jsonScenariosPath = Path.Combine(raiz, "oracle", "scenarios.json");
        var scenarios = JsonSerializer.Deserialize<List<ScenarioOracleItem>>(
            File.ReadAllText(jsonScenariosPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        )!;

        // Deve haver exatamente 63 cenários
        Assert.Equal(63, scenarios.Count);

        var allFiles = Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\") && !f.Contains(@"\.git\")
                     && !f.Contains(@"/bin/") && !f.Contains(@"/obj/") && !f.Contains(@"/.git/"))
            .Where(f => !f.EndsWith(".nupkg"))
            .ToList();

        var regex = new Regex(@"SCENARIO:([A-Za-z0-9\-]+)", RegexOptions.Compiled);
        var marcadoresEncontrados = new HashSet<string>();

        foreach (var file in allFiles)
        {
            try
            {
                var text = File.ReadAllText(file);
                var matches = regex.Matches(text);
                foreach (Match match in matches)
                {
                    marcadoresEncontrados.Add(match.Groups[1].Value);
                }
            }
            catch
            {
                // Ignora arquivos binários ocasionais
            }
        }

        var faltantes = new List<string>();
        foreach (var sc in scenarios)
        {
            if (!marcadoresEncontrados.Contains(sc.Id))
            {
                faltantes.Add(sc.Id);
            }
        }

        Assert.Empty(faltantes);
    }

    [Fact]
    public void Teste3_VerificarQueNenhumSistemaReferenciaProjetoDeOutroSistema()
    {
        var raiz = ObterDiretorioRaiz();
        var csprojFiles = Directory.GetFiles(raiz, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\") && !f.Contains(@"/bin/") && !f.Contains(@"/obj/"))
            .Where(f => !f.Contains("package-source"))
            .ToList();

        foreach (var csproj in csprojFiles)
        {
            var sourceName = Path.GetFileNameWithoutExtension(csproj);
            var doc = XDocument.Load(csproj);

            var projectReferences = doc.Descendants("ProjectReference")
                .Select(pr => pr.Attribute("Include")?.Value)
                .Where(val => !string.IsNullOrEmpty(val))
                .Select(val => Path.GetFileNameWithoutExtension(val!))
                .ToList();

            var sistemaOrigem = ObterPrefixoSistema(sourceName);

            foreach (var target in projectReferences)
            {
                var sistemaDestino = ObterPrefixoSistema(target);
                Assert.Equal(sistemaOrigem, sistemaDestino);
            }
        }
    }

    private static string ObterPrefixoSistema(string nomeProjeto)
    {
        if (nomeProjeto.StartsWith("SistemaE.Copia")) return "SistemaE.Copia";
        if (nomeProjeto.StartsWith("SistemaA")) return "SistemaA";
        if (nomeProjeto.StartsWith("SistemaB")) return "SistemaB";
        if (nomeProjeto.StartsWith("SistemaC")) return "SistemaC";
        if (nomeProjeto.StartsWith("SistemaD")) return "SistemaD";
        if (nomeProjeto.StartsWith("SistemaE")) return "SistemaE";
        return nomeProjeto;
    }

    [Fact]
    public void Teste4_VerificarQueSistemaBSlnxNaoIncluiACopiaAninhada()
    {
        var raiz = ObterDiretorioRaiz();
        var sistemaBSlnx = Path.Combine(raiz, "src", "SistemaB", "SistemaB.slnx");
        Assert.True(File.Exists(sistemaBSlnx));

        var conteudo = File.ReadAllText(sistemaBSlnx);
        Assert.DoesNotContain("SistemaE.Copia", conteudo);
        Assert.DoesNotContain("Copias", conteudo);
    }

    [Fact]
    public void Teste5_VerificarQueNenhumValorProibidoOuSegredoRealApareceNoRepositorio()
    {
        var raiz = ObterDiretorioRaiz();
        var allFiles = Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\") && !f.Contains(@"\.git\")
                     && !f.Contains(@"/bin/") && !f.Contains(@"/obj/") && !f.Contains(@"/.git/"))
            .Where(f => !f.EndsWith(".nupkg") && !f.EndsWith("CorpusIntegrityTests.cs"))
            .ToList();

        var padroesProibidos = new[]
        {
            "ghp_", "github_pat_", "AKIA", "ASIA", "-----BEGIN RSA PRIVATE KEY-----",
            "-----BEGIN OPENSSH PRIVATE KEY-----", "AccountKey=", "SharedAccessKey="
        };

        foreach (var file in allFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var padrao in padroesProibidos)
            {
                Assert.DoesNotContain(padrao, text);
            }
        }
    }

    [Fact]
    public void Teste6_VerificarQueArtefatoVazioNaoGanhouConteudoAnalisavel()
    {
        var raiz = ObterDiretorioRaiz();
        var artefatoVazioDir = Path.Combine(raiz, "src", "ArtefatoVazio");
        Assert.True(Directory.Exists(artefatoVazioDir));

        var arquivos = Directory.GetFiles(artefatoVazioDir, "*", SearchOption.AllDirectories);
        Assert.Single(arquivos);
        Assert.Equal(".gitkeep", Path.GetFileName(arquivos[0]));

        var subdirs = Directory.GetDirectories(artefatoVazioDir);
        Assert.Empty(subdirs);
    }
}
