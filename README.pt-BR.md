# csharp2md

[English](README.md) | Português

O `csharp2md` analisa `1..N` soluções C#/.NET explícitas e publica um único pacote de conhecimento: fatos, observações, relações causais e projeções de fonte precisas, auditáveis e diretamente navegáveis por uma LLM.

## Estado do projeto

O contrato do pacote de conhecimento é o único contrato do repositório. A substituição está concluída: `Csharp2Md.Core` é dono da análise, da construção do pacote e da publicação, e `Csharp2Md.Cli` é seu único seam externo. Não existe dispatch de versão, leitor legado, conversor nem rota de compatibilidade.

Comece por:

1. [Linguagem do domínio](CONTEXT.md)
2. [Decisões ativas e handoff](.specs/STATE.md)
3. [Especificação do contrato do pacote](docs/specs/pacote-conhecimento-util-e-confiavel.md)

## Limites do produto

- O gerador é responsável por extração determinística, classificação, validação, persistência e projeções de recuperação.
- Markdown é uma projeção; fatos e observações legíveis por máquina são a autoridade.
- A interpretação de regras de negócio pertence a uma LLM posterior, alimentada com o código relevante completo.
- Query engines, ingestão de wiki, `kb`, QMD, embeddings e análise incremental ficam adiados até o output do gerador estar completo.

## Pré-requisitos

- Instale o [SDK do .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0), não apenas o runtime. O [global.json](global.json) solicita o SDK `10.0.300` com `rollForward: latestFeature`, permitindo faixas de recursos mais recentes do .NET 10.
- Instale o Git para clonar o repositório e tenha acesso aos feeds NuGet para restaurar as dependências.
- Para as soluções que serão analisadas, instale os SDKs/workloads exigidos por elas e configure eventuais feeds privados de pacotes.

Confira os SDKs disponíveis e clone o repositório. Execute os exemplos seguintes a partir da raiz dele:

```shell
dotnet --list-sdks
git clone https://github.com/cdpfernando/csharp2md.git
cd csharp2md
dotnet restore csharp2md.slnx
dotnet build csharp2md.slnx --configuration Release --no-restore
```

## Instalar a partir do código-fonte

O projeto do CLI é empacotado como ferramenta .NET. Gere um pacote local e instale-o globalmente para seu usuário:

```shell
dotnet pack src/Csharp2Md.Cli/Csharp2Md.Cli.csproj --configuration Release --output artifacts/packages
dotnet tool install --global csharp2md --add-source ./artifacts/packages --version 4.0.0
csharp2md --help
```

`4.0.0` é a versão atual do pacote em [Directory.Build.props](Directory.Build.props); ajuste `--version` se ela mudar. Consulte a [documentação de instalação de ferramentas .NET](https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-install) para outras opções de instalação.

Se `csharp2md` não for encontrado, reabra o terminal e confira se o diretório de ferramentas globais está no `PATH`: `%USERPROFILE%\.dotnet\tools` no Windows ou `$HOME/.dotnet/tools` no Linux/macOS.

Para substituir uma instalação existente por um pacote recém-gerado, execute `dotnet tool uninstall --global csharp2md` e repita os comandos de empacotamento e instalação acima. O mesmo comando de desinstalação remove a ferramenta quando ela não for mais necessária.

## Executar o CLI

O CLI expõe dois comandos: `analyze` e `validate`.

### `analyze`

Restaure as soluções que deseja analisar e informe cada uma delas e um único diretório de saída:

```shell
dotnet restore "caminho/para/MinhaSolucao.slnx"
csharp2md analyze --solution "caminho/para/MinhaSolucao.slnx" --output "artifacts/minha-solucao"
```

| Opção             | Obrigatória | Significado                                                                        |
| ----------------- | ----------- | ---------------------------------------------------------------------------------- |
| `--solution`      | sim         | Caminho de uma solução a analisar. Repita a opção por solução; um pacote cobre todas. |
| `--output`        | sim         | Diretório que recebe o pacote comprometido.                                          |
| `--include-tests` | não         | Inclui projetos e documentos de teste e registra essa escolha na identidade do pacote. |

A análise compromete o pacote inteiro ou não altera nada. Uma execução materializa, reidrata e valida em staging, certifica as quatro jornadas de recuperação e só então troca o `manifest.json` raiz sob lock. Uma execução rejeitada deixa o pacote anterior intacto.

### `validate`

Revalida um pacote já publicado. Lê apenas o pacote e não toca em nenhuma solução:

```shell
csharp2md validate --package "artifacts/minha-solucao"
```

### Executar sem instalar

Use `dotnet run` a partir da raiz do repositório. `--no-launch-profile` desativa as configurações de execução predefinidas do repositório, e tudo após `--` é repassado ao CLI:

```shell
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- --help
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution "caminho/para/MinhaSolucao.slnx" --output "artifacts/minha-solucao"
```

### Experimentar com a fixture incluída

`fixtures/SyntheticSolution` é a fixture de análise versionada. `Acme.Orders` é uma de suas soluções:

```shell
dotnet restore fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx --output artifacts/orders-out
```

Com a ferramenta instalada, substitua `dotnet run --project src/Csharp2Md.Cli --no-launch-profile --` por `csharp2md`.

## O pacote publicado

Quem lê o pacote começa por exatamente um arquivo: o `manifest.json` na raiz da saída. Ele aponta para a geração comprometida em `generations/<digest>/`, e nada mais precisa ser enumerado para navegar o pacote.

Para cada solução analisada o manifesto declara oito índices de navegação — `identity`, `roots`, `outgoing`, `incoming`, `contracts`, `persistence`, `evidence` e `measures` — e quatro jornadas de recuperação certificadas: `locate`, `follow_flow`, `reverse_impact` e `evidence_disposition`. O resultado da certificação fica em `certification.json` e os budgets das jornadas em `measurements.json`.

Leia os diagnósticos do CLI além dos arquivos: a existência de conteúdo gerado, por si só, não indica uma análise bem-sucedida. Os diagnósticos vão para stderr no formato `csharp2md: code=... stage=... cause=...`, com as coordenadas de solução, projeto, variante, família e artefato que se aplicarem.

Códigos de saída:

| Código | Significado             |
| ------ | ----------------------- |
| `0`    | Sucesso                 |
| `1`    | Invocação inválida      |
| `4`    | Certificação reprovada  |
| `5`    | Corrupção estrutural    |

## Base de desenvolvimento

- SDK do .NET conforme o [global.json](global.json)
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0
- sem referência direta a `Microsoft.Build.*` e sem `MSBuildLocator.RegisterDefaults()`: o Roslyn 4.9+ carrega projetos por um BuildHost fora de processo

A solução contém quatro projetos — `Csharp2Md.Core`, `Csharp2Md.Cli` e seus projetos de teste:

```shell
dotnet build csharp2md.slnx
dotnet test csharp2md.slnx
```

`fixtures/SyntheticSolution` é a única fixture de análise versionada. Clones locais de corpora maiores são opcionais: quando presentes ampliam a aceitação e, quando ausentes, seus testes são ignorados em vez de falhar.
