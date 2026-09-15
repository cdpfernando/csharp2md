# csharp2md

[English](README.md) | Português

O `csharp2md` analisa `1..N` soluções C#/.NET explícitas e publica fatos, observações, relações causais e projeções de fonte precisas, auditáveis e diretamente navegáveis por uma LLM.

## Estado do projeto

A base do motor de conhecimento arquitetural foi implementada pelos workstreams 1–8. O workstream posterior `analysis-publication-resilience` também foi encerrado com PASS. O código-fonte, o CLI e os schemas atuais são a base operacional, mas não definem o contrato da próxima substituição incompatível. [A proposta para o próximo contrato do pacote](docs/specs/pacote-conhecimento-util-e-confiavel.md) existe como especificação local e sua implementação não foi iniciada.

Comece por:

1. [Linguagem do domínio](CONTEXT.md)
2. [Decisões ativas e handoff](.specs/STATE.md)
3. [Proposta para o próximo contrato do pacote](docs/specs/pacote-conhecimento-util-e-confiavel.md)

## Limites do produto

- O gerador é responsável por extração determinística, classificação, validação, persistência e projeções de recuperação.
- Markdown é uma projeção; fatos e observações legíveis por máquina são a autoridade.
- A interpretação de regras de negócio pertence a uma LLM posterior, alimentada com o código relevante completo.
- Query engines, ingestão de wiki, `kb`, QMD, embeddings e análise incremental ficam adiados até o output do gerador estar completo.
- Não existe exigência de retrocompatibilidade durante a substituição. A continuidade começa no primeiro release completo pós-migração.

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

## Executar o CLI atual

Os comandos abaixo descrevem a base operacional atual. A proposta para o próximo contrato do pacote pode substituí-los de forma incompatível.

Restaure a solução que deseja analisar e informe seu arquivo `.sln` ou `.slnx` e um diretório de saída:

```shell
dotnet restore "caminho/para/MinhaSolucao.slnx"
csharp2md analyze --solution "caminho/para/MinhaSolucao.slnx" --output "artifacts/minha-solucao"
```

Substitua o caminho de exemplo pelo da sua solução. Repita `--solution` para analisar várias soluções na mesma execução. Use `csharp2md analyze --help` para consultar todas as opções de análise; `csharp2md --help` também lista os comandos `validate` e `compose` para pacotes já publicados.

### Executar sem instalar

Use `dotnet run` a partir da raiz do repositório. `--no-launch-profile` desativa as configurações de execução predefinidas do repositório, e tudo após `--` é repassado ao CLI:

```shell
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- --help
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution "caminho/para/MinhaSolucao.slnx" --output "artifacts/minha-solucao"
```

### Experimentar com a fixture incluída

`Acme.Shipping` é uma solução pequena incluída no repositório:

```shell
dotnet restore fixtures/SyntheticSolution/Acme.Shipping/Acme.Shipping.slnx
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution fixtures/SyntheticSolution/Acme.Shipping/Acme.Shipping.slnx --output artifacts/shipping-out
```

Com a ferramenta instalada, substitua `dotnet run --project src/Csharp2Md.Cli --no-launch-profile --` por `csharp2md`.

A fixture pode publicar um pacote com certificação `degraded` (código de saída `3`): o exemplo atual informa cobertura incompleta de chamadas. Consulte os motivos em `run-certification.json`, dentro do pacote publicado.

O diretório de saída recebe `batch-manifest.json` e um diretório de pacote chamado `s-<hash>` para cada solução publicada. Comece pelo manifesto do lote para localizar cada pacote. Leia os diagnósticos e o resultado da certificação no CLI: a existência de arquivos gerados, por si só, não indica uma análise totalmente bem-sucedida. Os códigos de saída atuais são:

| Código | Significado |
| --- | --- |
| `0` | Sucesso |
| `1` | Invocação inválida |
| `2` | Composição parcial ou falha na publicação do lote |
| `3` | Certificação degradada |
| `4` | Certificação reprovada |
| `5` | Corrupção estrutural |
| `6` | Proveniência ou versão de contrato incompatível |

## Base de desenvolvimento

- SDK do .NET conforme o [global.json](global.json)
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0
- sem referência direta a `Microsoft.Build.*` e sem `MSBuildLocator.RegisterDefaults()`

```shell
dotnet build csharp2md.slnx
dotnet test csharp2md.slnx
```

Não deduza a semântica da próxima substituição a partir da implementação atual. O trabalho começa somente quando a proposta do pacote for convertida explicitamente em uma feature executável pelo processo `tlc-spec-driven`.
