---
title: "MSBuildWorkspace: robustez de carga + empacotamento como dotnet global tool"
date: "2026-08-14"
tags:
    - research
    - roslyn
    - dotnet-tool
---

# MSBuildWorkspace: robustez de carga + empacotamento como dotnet global tool

Pesquisa para preencher dois gaps técnicos de uma sessão de design em andamento (registrada em
`grilling/`) sobre uma ferramenta CLI em C# (`dotnet global tool`) que usa Roslyn
(`MSBuildWorkspace`) para analisar uma codebase de microsserviços .NET e gerar Markdown + grafo
de dependências entre serviços. Uma pesquisa anterior no vault local (`.kb/vault/roslyn`,
`.kb/vault/dotnet`) já cobriu bem "como usar Roslyn/SemanticModel/SyntaxWalker" — este documento
cobre só os dois gaps que precisavam de fontes primárias externas: (1) robustez do
`MSBuildWorkspace` ao carregar soluções problemáticas, e (2) o mecanismo de empacotamento como
`dotnet global tool`.

**Não decide nada** — a decisão de design já foi tomada (MSBuildWorkspace com degradação
graciosa, empacotado como dotnet global tool); isto é só o levantamento técnico do "como".
Todas as consultas foram feitas em **2026-08-14**. Fontes primárias usadas: Microsoft Learn
(`learn.microsoft.com`, API reference gerada a partir do código-fonte + docs conceituais), o
código-fonte do repositório oficial `github.com/dotnet/roslyn` (lido diretamente, ex.:
`MSBuildWorkspace.cs`), e comentários de mantenedores do time Roslyn em issues/PRs do mesmo
repositório (lidos via `gh` — GitHub CLI — quando o fetch HTML simples não expunha os comentários).
Um gist de um ex-membro do time Roslyn (Dustin Campbell) foi usado como complemento e está
claramente identificado como fonte secundária onde aparece — a doc conceitual oficial do Roslyn
SDK não cobre o cenário de tratamento de erro de `MSBuildWorkspace` (ver nota no fim da seção 1.2).

---

## 1. Robustez do `MSBuildWorkspace`

### 1.1. O evento `WorkspaceFailed`

`Workspace.WorkspaceFailed` (herdado por `MSBuildWorkspace`, que deriva de `Workspace`) é
declarado assim:

```csharp
public event EventHandler<Microsoft.CodeAnalysis.WorkspaceDiagnosticEventArgs> WorkspaceFailed;
```

Descrição oficial: "An event raised whenever the workspace or part of its solution model fails
to access a file or other external resource." Namespace `Microsoft.CodeAnalysis`, assembly
`Microsoft.CodeAnalysis.Workspaces.dll`.
Fonte: [Workspace.WorkspaceFailed Event](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.workspace.workspacefailed), consultado 2026-08-14.

**Achado importante — o evento está obsoleto a partir do Roslyn 5.0.0** (branch mais nova,
ainda não é a linha estável amplamente distribuída pelos SDKs .NET 8/9 no momento desta
pesquisa): a partir da versão `roslyn-dotnet-5.0.0` o membro carrega
`[Obsolete("Use RegisterWorkspaceFailedHandler instead, which by default will no longer run on
the UI thread.", false)]` — o `false` final indica que é um aviso de compilação, não um erro, ou
seja, o padrão `+=` clássico continua funcionando, só emite warning nas versões novas. O
substituto oficial, disponível a partir de `roslyn-dotnet-5.0.0`:

```csharp
public Microsoft.CodeAnalysis.WorkspaceEventRegistration RegisterWorkspaceFailedHandler(
    Action<Microsoft.CodeAnalysis.WorkspaceDiagnosticEventArgs> handler,
    Microsoft.CodeAnalysis.WorkspaceEventOptions? options = default);
```

Mesma descrição textual do evento antigo. Retorna um `WorkspaceEventRegistration` (não
documentado em detalhe nesta pesquisa — provavelmente um `IDisposable` para cancelar o
registro, padrão comum nesse tipo de API, mas não confirmado com fonte primária).
Fonte: [Workspace.RegisterWorkspaceFailedHandler Method](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.workspace.registerworkspacefailedhandler), consultado 2026-08-14.

**Implicação prática**: como a maior parte do ecossistema (pacotes NuGet `Microsoft.CodeAnalysis.*`
distribuídos com os SDKs .NET 8/9 atuais) ainda está na linha 4.x, o padrão `+=` clássico
(`workspace.WorkspaceFailed += (sender, e) => ...`) é o que vale hoje na prática — mas vale
verificar a versão do pacote `Microsoft.CodeAnalysis.Workspaces.MSBuild` referenciada pela
ferramenta e considerar `RegisterWorkspaceFailedHandler` se ela mirar Roslyn 5.0+.

### 1.2. O que `WorkspaceDiagnosticEventArgs`/`WorkspaceDiagnostic` carregam

`WorkspaceDiagnosticEventArgs : EventArgs` — um construtor
(`WorkspaceDiagnosticEventArgs(WorkspaceDiagnostic)`), uma única propriedade: `Diagnostic`
(tipo `WorkspaceDiagnostic`).
Fonte: [WorkspaceDiagnosticEventArgs Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.workspacediagnosticeventargs), consultado 2026-08-14.

`WorkspaceDiagnostic` — classe base de `DocumentDiagnostic` e `ProjectDiagnostic` (subtipos mais
específicos não aprofundados nesta pesquisa). Um construtor
(`WorkspaceDiagnostic(WorkspaceDiagnosticKind, string)`), duas propriedades: `Kind`
(`WorkspaceDiagnosticKind`) e `Message` (`string`), mais um `ToString()` sobrescrito.
Fonte: [WorkspaceDiagnostic Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.workspacediagnostic), consultado 2026-08-14.

`WorkspaceDiagnosticKind` — enum com exatamente dois valores: `Failure = 0` e `Warning = 1`. A
tabela de referência gerada automaticamente pelo Microsoft Learn **não preenche a coluna
"Description"** para nenhum dos dois — ou seja, a doc oficial não explica em prosa a diferença
semântica entre os dois valores.
Fonte: [WorkspaceDiagnosticKind Enum](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.workspacediagnostickind), consultado 2026-08-14.

**Gap confirmado por evidência indireta**: a issue
[dotnet/roslyn#75182](https://github.com/dotnet/roslyn/issues/75182) ("MSBuildWorkspace reports
warnings as errors/failures") mostra um caso real onde um aviso de segurança do NuGet (NU1903,
sobre uma dependência vulnerável) foi **incorretamente** classificado como `Failure` em vez de
`Warning`, por um bug em `Microsoft.CodeAnalysis.MSBuild.DiagnosticReporter.Report(DiagnosticLog
log)` que "ignora `DiagnosticLogItem.Kind`". A thread da issue não tem resposta de mantenedor
esclarecendo a distinção pretendida entre os dois kinds — então, na prática, **não dá para
confiar cegamente em `Kind == Failure` como sinal definitivo de "o projeto não carregou"**; o bug
relatado mostra que classificações erradas acontecem. Fonte:
[dotnet/roslyn#75182](https://github.com/dotnet/roslyn/issues/75182) — issue do GitHub, não é
doc oficial, mas é evidência primária de comportamento real do código (consultado 2026-08-14).

### 1.3. Assinando o evento e exemplo de uso (fonte secundária, doc conceitual oficial é omissa aqui)

A doc conceitual oficial "Work with the .NET Compiler Platform SDK workspace model" (Microsoft
Learn, seção C#/Roslyn SDK) cobre só o modelo genérico Workspace/Solution/Project/Document — ela
**não menciona `WorkspaceFailed`, tratamento de erro, nem nada específico de `MSBuildWorkspace`**.
Ou seja, a única cobertura "oficial" de `WorkspaceFailed` é a referência de API terse (assinatura
+ uma frase), sem um guia de "como usar" com exemplo de código.
Fonte: [Work with the .NET Compiler Platform SDK workspace model](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-workspace), consultado 2026-08-14 — confirmando a ausência.

Na falta de um exemplo oficial, o padrão de uso mais citado no ecossistema vem de um gist do
GitHub de **Dustin Campbell**, ex-membro do time Roslyn na Microsoft (autor de boa parte do
código-fonte de `WorkspaceDiagnostic*` no repo oficial, conforme os links "Source" nas páginas de
referência acima) — **é uma fonte secundária** (gist pessoal, não doc oficial), mas com
proximidade de autoria relevante:

```csharp
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

MSBuildLocator.RegisterDefaults();

using (var workspace = MSBuildWorkspace.Create())
{
    var solution = await workspace.OpenSolutionAsync("MySolution.sln");

    foreach (var project in solution.Projects)
    {
        var compilation = await project.GetCompilationAsync();
        // Perform analysis...
    }
}
```

Notas relevantes do mesmo gist: (a) `MSBuildLocator.RegisterDefaults()` deve ser chamado **antes**
de criar a `MSBuildWorkspace` (senão dá erro de composição MEF); (b) não incluir assemblies
`Microsoft.Build.*` no output da própria aplicação, exceto `Microsoft.Build.Locator` — eles
interferem nos handlers de resolução de assembly; (c) o gist menciona a propriedade
`MSBuildWorkspace.Diagnostics`, mas não traz um exemplo completo de assinatura do evento nem de
tratamento por-projeto.
Fonte: [gist de Dustin Campbell, "Using MSBuildWorkspace"](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3) — fonte secundária, consultado 2026-08-14.

**Confirmação direta no código-fonte** (fonte primária de maior confiança que o gist, porque é o
próprio código que implementa a classe): o arquivo
[`MSBuildWorkspace.cs`](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/MSBuild/Core/MSBuild/MSBuildWorkspace.cs)
no repositório `dotnet/roslyn` (lido diretamente via `gh api`, branch `main`, consultado
2026-08-14) confirma duas APIs muito relevantes para o design de degradação graciosa, além do
evento `WorkspaceFailed`:

```csharp
/// <summary>
/// Diagnostics logged while opening solutions, projects and documents.
/// </summary>
public ImmutableList<WorkspaceDiagnostic> Diagnostics => Reporter.Diagnostics;

protected internal override void OnWorkspaceFailed(WorkspaceDiagnostic diagnostic)
{
    Reporter.AddDiagnostic(diagnostic);
    base.OnWorkspaceFailed(diagnostic);
}
```

Ou seja, **não é preciso assinar o evento `WorkspaceFailed` para coletar os diagnósticos** — a
própria `MSBuildWorkspace` já acumula tudo internamente na lista `Diagnostics`, disponível para
inspeção **depois** de `OpenSolutionAsync`/`OpenProjectAsync` retornar. Isso simplifica bastante o
padrão de "logar e seguir": basta ler `workspace.Diagnostics` de uma vez ao final do carregamento,
sem necessariamente precisar de um handler assíncrono de evento.

E, mais relevante ainda para a causa (c) do item 1.4 abaixo:

```csharp
/// <summary>
/// Determines if unrecognized projects are skipped when solutions or projects are opened.
///
/// An project is unrecognized if it either has
///   a) an invalid file path,
///   b) a non-existent project file,
///   c) has an unrecognized file extension
///
/// If unrecognized projects cannot be skipped a corresponding exception is thrown.
/// </summary>
public bool SkipUnrecognizedProjects
{
    get => _loader.SkipUnrecognizedProjects;
    set => _loader.SkipUnrecognizedProjects = value;
}
```

Esta é literalmente a propriedade que implementa "pular projeto problemático em vez de abortar a
solução inteira" — mas só para os três casos documentados no próprio XML doc comment (caminho de
arquivo inválido, arquivo de projeto inexistente, extensão de arquivo não reconhecida). O
comentário deixa explícito que, **se `SkipUnrecognizedProjects` for `false` (o que não é
confirmado nesta pesquisa se é o default) e um projeto não reconhecido for encontrado, uma
exceção é lançada** — ou seja, esse é o mecanismo correto para blindar contra parte da causa (c)
("erros de parsing do próprio `.sln`/`.csproj`") do item 1.4, especificamente para os três
sub-casos listados. Não cobre, pelo texto do comentário, erros de SDK MSBuild não encontrado nem
`TargetFramework` inválido dentro de um `.csproj` que por outro lado é um arquivo válido e
reconhecido — esses continuam caindo no fluxo genérico de `WorkspaceDiagnostic`/`WorkspaceFailed`
(`Kind == Failure`, presumivelmente, mas não confirmado com um caso de teste real nesta pesquisa).
Fonte: [`dotnet/roslyn` — `MSBuildWorkspace.cs`, branch `main`](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/MSBuild/Core/MSBuild/MSBuildWorkspace.cs), lido via GitHub API em 2026-08-14.

### 1.4. Causas de falha ao carregar solução/projeto

**(a) Pacotes NuGet não restaurados.** A issue
[dotnet/roslyn#52293](https://github.com/dotnet/roslyn/issues/52293) ("MSBuildWorkspace should
have a way to trigger a restore automatically") estabelece que **`MSBuildWorkspace` não dispara
restore automaticamente**. O relato original descreve que abrir um projeto .NET Standard 2.0 sem
pacotes restaurados produz erros de compilação do tipo "The type or namespace name 'System' could
not be found in the global namespace" — ou seja, o projeto **aparenta carregar** (aparece em
`solution.Projects`), mas a `Compilation` resultante fica com referências faltando.

**Atualização — confirmado com comentário de mantenedor** (lido via `gh issue view 52293
--repo dotnet/roslyn --json comments`, já que a versão renderizada em HTML da issue não expõe os
comentários para fetch simples): **JoeRobich**, do time Roslyn/.NET na Microsoft, confirma
diretamente no thread que rodar `dotnet restore` manualmente antes de abrir o projeto resolve o
problema —

> "@ceztko If you run `dotnet restore` against the net standard project first, does it open as
> expected?"

— e o autor da issue confirma que sim, o `dotnet restore` prévio contorna o problema. Em um
comentário posterior, JoeRobich reproduziu o problema e apontou a PR
[dotnet/roslyn#61391](https://github.com/dotnet/roslyn/pull/61391) ("Specify targets when loading
MSBuildWorkspace") como uma tentativa de permitir solicitar restore ao carregar o projeto no
workspace. **Essa PR e uma anterior relacionada,
[dotnet/roslyn#52554](https://github.com/dotnet/roslyn/pull/52554) ("MSBuildWorkspace Add Reload
Apis"), estão ambas com status `CLOSED` e `mergedAt: null`** (confirmado via `gh pr view --json
state,mergedAt`) — ou seja, **nenhuma das duas tentativas de adicionar restore nativo ao
`MSBuildWorkspace` foi mergeada**. Isso confirma com alto grau de confiança, direto do repositório
oficial: **a ferramenta do usuário precisa rodar `dotnet restore` (ou equivalente) por conta
própria antes de chamar `OpenSolutionAsync`/`OpenProjectAsync`** — não há (ainda) um jeito nativo
de pedir isso ao `MSBuildWorkspace`.
Fontes: [dotnet/roslyn#52293](https://github.com/dotnet/roslyn/issues/52293) (issue + comentários,
via `gh`), [dotnet/roslyn#61391](https://github.com/dotnet/roslyn/pull/61391) e
[dotnet/roslyn#52554](https://github.com/dotnet/roslyn/pull/52554) (ambas PRs, status via `gh`),
consultado 2026-08-14.

Reforço indireto: a doc oficial de `dotnet pack`/NuGet confirma que o `restore` é um target MSBuild
separado do `build`, que gera `project.assets.json` e os arquivos `.nuget.g.props`/`.nuget.g.targets`
no diretório `obj` — artefatos que o processo de design-time build do MSBuild (usado por
`MSBuildWorkspace` internamente) precisa encontrar para resolver referências de pacote. Fonte:
[NuGet pack and restore as MSBuild targets — seção "restore target"](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets), consultado 2026-08-14.

**(b) Falha de infraestrutura/ambiente (não é falha de compilação do código-fonte em si).** A
issue [dotnet/roslyn#77640](https://github.com/dotnet/roslyn/issues/77640) mostra que
`MSBuildWorkspace.OpenProjectAsync` pode falhar completamente (lançando exceção, não apenas
produzindo uma `Compilation` degradada) quando o processo `BuildHost` — introduzido como
mecanismo out-of-process a partir de ~4.9.0 — não consegue localizar/lançar `dotnet.exe` porque
o `.NET` não está pré-instalado no sistema alvo (o `ProcessStartInfo.WorkingDirectory` fica vazio
e a busca por `dotnet.exe` no PATH falha). Isso é uma causa de falha **de ambiente**, distinta de
NuGet não restaurado ou de erro de compilação no código do usuário. A issue está aberta, sem
comentário de mantenedor sobre resolução no momento desta pesquisa.
Fonte: [dotnet/roslyn#77640](https://github.com/dotnet/roslyn/issues/77640), consultado 2026-08-14.

**(c) Erros de parsing do próprio `.sln`/`.csproj` (SDK não encontrado, `TargetFramework`
inválido).** **Gap não preenchido nesta pesquisa** — não foi encontrada, nem em doc oficial nem
em issue dedicada do `dotnet/roslyn`, uma confirmação end-to-end e específica desse cenário (por
exemplo, um projeto referenciando um SDK MSBuild não instalado). O comportamento geral do
`MSBuildWorkspace` de repassar diagnósticos do MSBuild via `WorkspaceDiagnostic`/`WorkspaceFailed`
é consistente com essa causa também produzir um diagnóstico de `Kind == Failure`, mas isso é
inferência a partir do design geral da API, não uma confirmação direta de fonte primária para
esse caso específico. **Revisitar se a ferramenta encontrar esse cenário na prática.**

### 1.5. Correção importante sobre o padrão de "pular projeto problemático"

O enunciado original desta pesquisa presumia que `Project.GetCompilationAsync()` retornando
`null` seria o sinal de "o projeto falhou ao carregar". **A doc oficial contradiz essa
suposição**:

```csharp
public Task<Compilation?> GetCompilationAsync(CancellationToken cancellationToken = default);
```

> "Returns the produced Compilation, or `null` if `SupportsCompilation` returns `false`. This
> function will return the same value if called multiple times."

Ou seja, `GetCompilationAsync()` só retorna `null` para tipos de projeto que **não suportam
compilação de forma alguma** (ex.: projetos de linguagens sem suporte a `Compilation` no modelo
Roslyn) — não é um sinal geral de "carregamento falhou". Um projeto com pacotes não restaurados
(caso 1.4a) tipicamente **não** retorna `null` — ele retorna uma `Compilation` real, só que com
diagnósticos de erro (referências faltando) dentro dela.
Fonte: [Project.GetCompilationAsync(CancellationToken) Method](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.project.getcompilationasync), consultado 2026-08-14.

**Implicação para o design de degradação graciosa**: checar `compilation is null` sozinho não
identifica projetos com carga degradada por falta de restore — para isso é preciso (i) inspecionar
os `WorkspaceDiagnostic`s emitidos durante `OpenSolutionAsync`/`OpenProjectAsync`
(`Kind == WorkspaceDiagnosticKind.Failure`, com a ressalva de 1.2 de que a classificação pode
estar errada em casos de borda) e/ou (ii) inspecionar `compilation.GetDiagnostics()` procurando
erros de referência não resolvida, depois de obter a `Compilation`. Este parágrafo é síntese
própria a partir dos fatos documentados acima (assinatura de `GetCompilationAsync`, o enum
`WorkspaceDiagnosticKind`, e o relato da issue #52293) — **não é uma citação de um exemplo
oficial**, porque nenhuma fonte primária consultada apresenta esse padrão completo pronto.

Um esboço combinando os fatos confirmados (não é um sample oficial, é síntese):

```csharp
var diagnostics = new List<WorkspaceDiagnostic>();
workspace.WorkspaceFailed += (_, e) =>
{
    diagnostics.Add(e.Diagnostic);
    logger.Log(e.Diagnostic.Kind, e.Diagnostic.Message);
};

var solution = await workspace.OpenSolutionAsync(solutionPath);

foreach (var project in solution.Projects)
{
    var compilation = await project.GetCompilationAsync();
    if (compilation is null)
    {
        // SupportsCompilation == false para este tipo de projeto — não é "falhou", é
        // um tipo de projeto que o Roslyn não modela como Compilation.
        continue;
    }

    var loadErrors = compilation.GetDiagnostics()
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .ToList();
    if (loadErrors.Count > 0)
    {
        logger.Warn($"Projeto {project.Name} carregou com {loadErrors.Count} erro(s) " +
                     "de compilação (possível restore ausente) — seguindo com análise parcial.");
    }
    // seguir análise mesmo com erros, ou pular conforme política da ferramenta...
}
```

### 1.6. Performance/memória em soluções grandes

**Gap parcial** — o único documento oficial dedicado a esse tema no repositório `dotnet/roslyn`,
["Performance considerations for large solutions"](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Performance-considerations-for-large-solutions.md),
é **inteiramente focado em configurações do Visual Studio** (desabilitar "Full Solution
Diagnostics", CodeLens, ajustar chaves de registro de cache, desabilitar "Solution Crawler") — ele
**não cobre `MSBuildWorkspace` nem cenários de uso programático fora da IDE**. Fonte:
[dotnet/roslyn — Performance-considerations-for-large-solutions.md](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Performance-considerations-for-large-solutions.md), consultado 2026-08-14.

Evidência indireta relevante encontrada em issues do repositório (não é doc oficial, mas é sinal
concreto de comportamento real):

- [dotnet/roslyn#76679](https://github.com/dotnet/roslyn/issues/76679): regressão de performance
  medida diretamente com `MSBuildWorkspace` (não IDE) entre as versões 4.8.0 e 4.12.0 — tempos de
  abertura de solução aumentaram entre **419% e 828%** em três soluções open-source de teste
  (SharpDevelop, 44 projetos: 14.40s → 119.24s; SimplCommerce, 34 projetos: 13.97s → 80.36s; CAP,
  31 projetos: 11.66s → 48.85s). O autor da issue suspeita que a causa é o `BuildHost`
  (processo MSBuild fora do processo principal, introduzido por volta da 4.9.0), mas não testou
  versões intermediárias para confirmar.

  **Atualização — orientação real de mantenedor, lida via `gh issue view 76679 --repo
  dotnet/roslyn --json comments`** (comentários não aparecem no fetch HTML simples): o relato
  original media o tempo abrindo **um projeto de cada vez em loop**
  (`workspace.OpenProjectAsync(...)` por projeto). **jasonmalinowski**, do time Roslyn, respondeu
  apontando exatamente essa prática como suspeita:

  > "Is there a reason this wouldn't be opening the solution directly and looking at each of the
  > projects that came out there?"

  O autor da issue (`jzielnik`) testou a sugestão e confirmou uma melhora drástica:

  > "We've switched to opening the entire solution at once. The performance is still worse than
  > original 4.8.0 version (about 20-40% rather than 400-800%) but acceptable."

  jasonmalinowski fechou o assunto reconhecendo que ainda há espaço para otimizar, mas que o custo
  extra do `BuildHost` (processo fora do principal) é uma troca aceitável pela precisão que ele
  traz. **Esta é a recomendação prática mais concreta e verificada desta pesquisa para
  performance**: preferir **abrir a solução inteira uma única vez** (`OpenSolutionAsync`) e iterar
  `solution.Projects` a partir daí, em vez de chamar `OpenProjectAsync` em loop por projeto — isso
  por si só reduziu a regressão relatada de 400-800% para 20-40% no caso real relatado. Não é uma
  eliminação completa da regressão de performance entre versões do Roslyn, mas é a mitigação mais
  direta e confirmada por mantenedor encontrada nesta pesquisa.
  Fonte: [dotnet/roslyn#76679](https://github.com/dotnet/roslyn/issues/76679) (issue + comentários,
  via `gh`), consultado 2026-08-14.
- [dotnet/roslyn#78868](https://github.com/dotnet/roslyn/issues/78868): consumo de até 95% da RAM
  de uma máquina com 32GB, em ~15 minutos, para uma solução de 22 projetos/~3M linhas — mas esse
  caso é sobre o **Visual Studio 2022 IDE** (`ServiceHub.RoslynCodeAnalysisService.exe`), não
  `MSBuildWorkspace` isolado, então tem relevância limitada para uma ferramenta CLI standalone.

**Conclusão desta subseção**: não há nota oficial de performance/memória específica para
`MSBuildWorkspace` fora da IDE. O sinal mais próximo e mais diretamente aplicável é a issue
#76679 (regressão medida com `MSBuildWorkspace` puro), que sugere **fixar/testar contra uma
versão específica do pacote `Microsoft.CodeAnalysis.Workspaces.MSBuild`** em vez de sempre usar a
mais recente, e considerar medir o tempo de abertura em soluções de referência do tamanho
esperado antes de assumir que a ferramenta escala bem. Isso é inferência própria a partir da
issue, não uma recomendação oficial.

---

## 2. Empacotamento como `dotnet global tool`

### 2.1. `PackAsTool` e propriedades relacionadas no `.csproj`

A partir do tutorial oficial "Tutorial: Create a .NET tool" (aplica-se ao **.NET 8 SDK e
versões posteriores**, testado no tutorial com .NET SDK 10.0), o exemplo de `.csproj` completo é:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>

    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <PackAsTool>true</PackAsTool>
    <ToolCommandName>dotnet-env</ToolCommandName>
    <PackageOutputPath>./nupkg</PackageOutputPath>

  </PropertyGroup>

</Project>
```

- **`OutputType`** deve ser `Exe` (requisito confirmado pelo exemplo oficial — o tutorial usa
  `dotnet new console` como ponto de partida, que já gera `OutputType=Exe`).
- **`ToolCommandName`**: "elemento opcional que especifica o comando que invoca a ferramenta
  depois de instalada. Se este elemento não for fornecido, o nome do comando é o nome do
  assembly, que é tipicamente o nome do arquivo de projeto sem a extensão `.csproj`." A doc
  recomenda escolher um valor único e evitar extensões de arquivo (`.exe`, `.cmd`) no nome,
  porque a ferramenta é instalada como um app host e o comando não deve incluir extensão.
- **`PackageOutputPath`**: "elemento opcional que determina onde o .NET produz o pacote NuGet." O
  CLI usa esse pacote para instalar a ferramenta.
Fonte: [Tutorial: Create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create), consultado 2026-08-14.

**`PackageId` e `Version`** não aparecem nesse tutorial (ele deixa os dois no default), mas a doc
de referência do target `pack` do NuGet confirma os defaults:

| Propriedade MSBuild | Origem/Default | Nota |
|---|---|---|
| `PackageId` | `$(AssemblyName)` | "If not specified, the pack operation will default to using the `AssemblyName` or directory name as the name of the package." |
| `PackageVersion` | `$(Version)` (que por sua vez, se não setado, é `1.0.0` por convenção do SDK) | "Default is the value of `$(Version)`, that is, of the property `Version` in the project." |
| `PackageOutputPath` | `$(OutputPath)` | Sobrescrito para `./nupkg` no exemplo do tutorial acima. |

Fonte: [NuGet pack and restore as MSBuild targets — tabela "pack target inputs"](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets), consultado 2026-08-14.

**Requisito de `TargetFramework` mínimo**: a doc geral de .NET tools ("**.NET tools - .NET
CLI**") afirma no topo: "**This article applies to:** .NET Core 2.1 SDK and later versions" — ou
seja, o recurso de global tools (e por extensão `PackAsTool`) está disponível desde o .NET Core
2.1 SDK. O tutorial específico de criação de tool já citado acima roda com "**.NET 8 SDK and
later versions**" como pré-requisito mínimo confirmado no próprio texto do tutorial (usando .NET
SDK 10.0 nos exemplos, mas afirmando explicitamente que "this guide applies to .NET 8.0 and
later"). Não há, portanto, contradição: o mecanismo básico de `PackAsTool` é suportado desde
2.1, mas a doc tutorial atual foi escrita/testada a partir do .NET 8.
Fontes: [.NET tools - .NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) e [Tutorial: Create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create), ambas consultadas 2026-08-14.

### 2.2. Fluxo `dotnet pack` → `dotnet tool install --global --add-source`

**`dotnet pack`**: "builds the project and creates NuGet packages. The result of this command is
a NuGet package (that is, a `.nupkg` file)." Executa restore implícito por padrão (desabilitável
com `--no-restore`), e build implícito por padrão (desabilitável com `--no-build`, que também
desabilita implicitamente o restore).
Fonte: [dotnet pack command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack), consultado 2026-08-14.

No tutorial oficial, rodar `dotnet pack` na raiz do projeto gera `dotnet-env.1.0.0.nupkg` na
pasta apontada por `PackageOutputPath` (`./nupkg` no exemplo).

**`dotnet tool install --global --add-source <pasta> <PackageId>`**: a opção `--add-source
<SOURCE>` "adds an additional NuGet package source to use during installation. Feeds are
accessed in parallel, not sequentially in some order of precedence. If the same package and
version is in multiple feeds, the fastest feed wins." O tutorial de "instalar como local tool"
(terceira parte da série) usa exatamente esse padrão para instalar a partir do `.nupkg` local
antes de publicar num feed real:

```console
dotnet tool install --add-source ./dotnet-env/nupkg dotnet-env
```
(esse exemplo específico é para instalação **local**, mas o mesmo `--add-source` funciona
igualmente com `--global`, já que é uma opção comum a ambos os modos de `dotnet tool install`,
conforme a sintaxe completa documentada).
Fontes: [dotnet tool install command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install) e [Tutorial: Install and use .NET local tools](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use), ambas consultadas 2026-08-14.

Para instalação **global** especificamente, a sintaxe documentada é:

```console
dotnet tool install -g dotnetsay
```
(exemplo genérico da doc; o padrão equivalente com `--add-source` seria
`dotnet tool install -g <PackageId> --add-source <pasta-do-nupkg>`, combinando as duas opções
documentadas separadamente — não há um exemplo único na doc que junte as duas exatamente para o
caso global, mas ambas as opções (`-g`/`--global` e `--add-source`) fazem parte da mesma sintaxe
unificada de `dotnet tool install` mostrada na seção "Synopsis" da referência do comando).
Fonte: [dotnet tool install command — Synopsis](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install), consultado 2026-08-14.

Publicação num feed real (NuGet.org ou feed privado) depois do teste local: a doc do tutorial diz
textualmente — "To release a tool publicly, upload it to `https://www.nuget.org`. Once the tool
is available on NuGet, developers can install the tool using the `dotnet tool install` command."
Fonte: [Tutorial: Create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create), consultado 2026-08-14.

### 2.3. Instalação global vs. tool manifest local

A doc "**.NET tools - .NET CLI**" descreve três formas de instalar um .NET tool: global (default
location), global em local customizado (`--tool-path`), e local (por diretório, via manifest).
Fatos documentados para cada uma (sem recomendação — a doc apresenta como opções equivalentes
para cenários diferentes):

**Global** (`dotnet tool install -g <pkg>`):
- Binários instalados em `$HOME/.dotnet/tools` (Linux/macOS) ou `%USERPROFILE%\.dotnet\tools`
  (Windows) — local adicionado ao PATH do usuário quando o SDK roda pela primeira vez.
- "Global tools can be invoked from any directory without specifying the tool location."
- "Tool access is user-specific, not machine global. A global tool is only available to the user
  that installed the tool."
- **Uma única versão instalada por vez, compartilhada entre todos os diretórios da máquina** (do
  usuário que instalou).

**Local** (tool manifest — `dotnet new tool-manifest` + `dotnet tool install` sem `--global`):
- Aplica-se a partir do .NET Core 3.0 SDK.
- Cria/usa um arquivo `.config/dotnet-tools.json` na raiz do repositório, versionável em
  controle de versão.
- "Different directories can use different versions of the same tool" — cada repositório/pasta
  pode fixar sua própria versão.
- Restaurado via `dotnet tool restore` — um único comando reinstala todas as ferramentas listadas
  no manifest, útil para outros desenvolvedores que clonam o repositório.
- Invocado via `dotnet tool run <comando>` ou `dotnet <comando>` (formas longa/curta), sempre a
  partir do diretório de instalação ou subdiretórios.
- **Aviso oficial de segurança**: "the .NET CLI launches local tools with `dotnet tool run` based
  on the contents of the tool manifest. If the manifest is modified by an untrusted party, it
  could cause the CLI to run malicious code."

A doc não recomenda um modo específico para "ferramenta de análise rodada manualmente contra
codebases variadas" — ela só descreve os fatos acima; a escolha entre os dois modelos depende do
cenário de uso (mencionado textualmente): tools locais existem para o caso em que "a contributor
can clone the repository and invoke a single .NET CLI command to install all of the tools listed
in the manifest files" — ou seja, o modelo local é desenhado para **ferramentas atreladas a um
repositório específico e compartilhadas entre contribuidores daquele repositório**, enquanto o
modelo global é para **uma ferramenta que o usuário individual quer disponível em qualquer
diretório da própria máquina**. Isso é uma leitura direta do texto oficial, não uma opinião
própria — a decisão de qual modelo faz mais sentido para o caso do usuário (ferramenta de análise
rodada manualmente, não parte de build automatizado de um repo específico) fica para a sessão de
grill.
Fonte: [.NET tools - .NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools), consultado 2026-08-14.

### 2.4. Atualizar e desinstalar

**Atualizar**: `dotnet tool update` com a mesma opção usada na instalação:

```console
dotnet tool update --global <packagename>
dotnet tool update --tool-path <packagename>
dotnet tool update <packagename>
```

"Updating a tool involves uninstalling and reinstalling it with the latest stable version." Para
tool local, o SDK procura o primeiro arquivo de manifest (na árvore de diretórios, subindo) que
contenha aquele `PackageId`; se não achar, adiciona uma nova entrada no manifest mais próximo.
Fonte: [.NET tools - .NET CLI — seção "Update a tool"](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools), consultado 2026-08-14.

**Desinstalar**: `dotnet tool uninstall` com a mesma lógica de opções:

```console
dotnet tool uninstall <PACKAGE_NAME> -g|--global
dotnet tool uninstall <PACKAGE_NAME> --tool-path <PATH> [--tool-manifest <PATH>]
dotnet tool uninstall <PACKAGE_NAME>
```

Omitir `--global` e `--tool-path` desinstala a ferramenta como tool local (do manifest mais
próximo na árvore de diretórios).
Fonte: [dotnet tool uninstall command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-uninstall), consultado 2026-08-14.

---

## Resumo dos gaps residuais desta pesquisa

- **Gap 1(c) — parcialmente preenchido**: `SkipUnrecognizedProjects` (ver 1.3) resolve, de forma
  confirmada no código-fonte, os três sub-casos de "projeto não reconhecido" (caminho inválido,
  arquivo inexistente, extensão não reconhecida). O que **continua sem confirmação de fonte
  primária dedicada** é o comportamento específico para um `.csproj` que é um arquivo válido e
  reconhecido, mas referencia um SDK MSBuild não instalado ou tem `TargetFramework` malformado —
  presume-se que caia no fluxo genérico `WorkspaceDiagnostic`/`Kind == Failure`, mas isso não foi
  testado nem encontrado documentado em um caso real nesta pesquisa.
- **Gap 1 (performance) — largamente preenchido**: não existe nota oficial de performance/memória
  dedicada a `MSBuildWorkspace` fora de IDE (o wiki "large solutions" do próprio `dotnet/roslyn` é
  só sobre Visual Studio), mas a issue #76679 rendeu uma recomendação prática **verificada e
  atribuída a um mantenedor Roslyn** (jasonmalinowski): abrir a solução inteira de uma vez via
  `OpenSolutionAsync` em vez de `OpenProjectAsync` em loop, o que reduziu uma regressão relatada
  de 400-800% para 20-40% num caso real. Não há, ainda, um teto numérico oficial de memória/nº de
  projetos suportado.
- **Gap 1 (semântica de `WorkspaceDiagnosticKind`)**: a doc oficial não documenta em prosa a
  diferença pretendida entre `Failure` e `Warning`, e há pelo menos um bug relatado (issue
  #75182) de classificação incorreta — tratar `Kind` como sinal auxiliar, não como fonte única de
  verdade.
- **Gap 2**: nenhum de grande porte identificado — a doc oficial de .NET tools é bastante
  completa para o fluxo pedido (PackAsTool, pack, install, update, uninstall, global vs. local).
  O único ponto sem exemplo literal de doc é a combinação exata `--global` + `--add-source` num
  único comando (ambas as opções são documentadas separadamente, mas não há um exemplo oficial
  que as combine lado a lado — a composição é inferida da sintaxe unificada do comando, não uma
  lacuna de fato, só de exemplo).

### Nota sobre o processo desta pesquisa

Esta pesquisa foi refeita/complementada depois de uma primeira tentativa (via agente em
background) não ter deixado rastro verificável em disco. A versão final combina duas passadas: uma
via `WebFetch`/`WebSearch` direto (fontes Microsoft Learn + issues do GitHub renderizadas em
HTML) e uma segunda passada usando `gh` (GitHub CLI) para ler comentários de issues/PRs que o
fetch HTML simples não expõe (GitHub carrega comentários via JavaScript/API separada) — foi assim
que os comentários de mantenedores (JoeRobich, jasonmalinowski) e o status real das PRs
#61391/#52554 puderam ser confirmados. Recomenda-se, em pesquisas futuras que dependam de threads
de issues/PRs do GitHub, preferir `gh issue view --json comments`/`gh pr view --json state,mergedAt`
desde o início, em vez de `WebFetch` na URL HTML.
