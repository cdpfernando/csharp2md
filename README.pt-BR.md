# csharp2md

[English](./README.md) | Português

`csharp2md` converte um projeto ou uma base C#/.NET em um modelo factual
validado e legível por máquina, além de documentação em Markdown, com uma
visão consolidada das dependências entre serviços.

## Pré-requisitos

- .NET SDK 10 disponível no `PATH`.
- Dependências dos projetos que serão analisados restauradas, quando possível.

## Executar a partir do código-fonte

Use `--` para separar os argumentos do `dotnet run` dos argumentos do
`csharp2md`:

```shell
dotnet run --project src/Csharp2Md.Cli -- --help
```

Os exemplos abaixo usam o comando global `csharp2md`. Durante o desenvolvimento,
ele pode ser substituído por `dotnet run --project src/Csharp2Md.Cli --`.

## Exemplos

### Analisar o diretório atual

```shell
cd MinhaSolucao
csharp2md
```

Se o diretório atual for `MinhaSolucao`, a saída será gravada por padrão em um
diretório irmão chamado `MinhaSolucao_md`.

### Analisar um diretório específico

```shell
csharp2md ./src
```

Nesse caso, a saída padrão será `./src_md`. Caminhos que contêm espaços devem
ser colocados entre aspas:

```shell
csharp2md "C:\repos\Minha Solucao"
```

### Escolher o diretório de saída

`--output` (ou `-o`) representa o caminho final exato; nenhum sufixo é
acrescentado:

```shell
csharp2md ./src --output ./docs/codigo
csharp2md ./src -o ./docs/codigo
```

### Analisar múltiplos serviços com um manifesto

Crie um arquivo `manifest.json`:

```json
{
  "services": [
    { "path": "services/Orders" },
    { "path": "services/Payments", "name": "Payments API" },
    {
      "path": "services/Notifications",
      "projects": ["services/Notifications/Notifications.Api.csproj"]
    }
  ]
}
```

Depois execute:

```shell
csharp2md --manifest ./manifest.json --output ./docs/codigo
```

O campo `path` também aceita `*` ou `?` no último segmento. Por exemplo:

```json
{
  "services": [
    { "path": "services/Acme.*" }
  ]
}
```

Os caminhos do manifesto são resolvidos a partir do diretório em que o comando
é executado. O argumento posicional de diretório e `--manifest` são modos
alternativos e não podem ser usados juntos.

### Substituir uma saída existente

O `csharp2md` pode atualizar automaticamente diretórios que ele próprio criou.
Para substituir um diretório não vazio que não foi criado pela ferramenta, use
`--force`:

```shell
csharp2md ./src --output ./docs/codigo --force
```

Esse comando remove o conteúdo anterior do diretório de saída. A ferramenta
sempre recusa usar como saída o diretório de entrada, um ancestral dele ou a
raiz do sistema de arquivos.

### Tópico e domínio

`--topic` e `--domain` rotulam a saída gerada — gravados em `raw/topic.yaml`
e no manifesto factual — para organizar várias bases analisadas sob um mesmo
site de documentação. Ambos têm padrões sensatos (`--topic` assume um slug do
nome do diretório de entrada; `--domain` assume `system-design`), então a
maioria das execuções pode omiti-los:

```shell
csharp2md ./src --topic payments-api --domain billing
```

### Executar contra a fixture deste repositório

Sem instalar a ferramenta globalmente:

```shell
dotnet run --project src/Csharp2Md.Cli -- \
  ./fixtures/SyntheticSolution/Acme.Orders \
  --output ./artifacts/example
```

No PowerShell, a mesma execução pode ser escrita em uma linha:

```powershell
dotnet run --project src/Csharp2Md.Cli -- ./fixtures/SyntheticSolution/Acme.Orders --output ./artifacts/example
```

## Instalar como ferramenta global a partir do repositório

```shell
dotnet pack src/Csharp2Md.Cli -c Release
dotnet tool install --global --add-source ./src/Csharp2Md.Cli/nupkg csharp2md
csharp2md --help
```

Para reinstalar uma versão empacotada localmente, remova primeiro a instalação
anterior com `dotnet tool uninstall --global csharp2md`.

## Modos de análise e confiança

Por padrão, o `csharp2md` analisa arquivos de código-fonte e de projeto de
forma **inerte**: ele nunca inicia o `dotnet msbuild`, carrega um workspace
Roslyn, nem executa analisadores, geradores de código ou qualquer outro
caminho de análise executável. Isso torna o padrão seguro para apontar a um
repositório arbitrário ou não confiável, ao custo de precisão semântica — a
resolução de símbolos, a detecção de frameworks e os fatos de relação
permanecem apenas sintáticos.

```shell
csharp2md ./src                                                # syntax-only, não confiável (padrão)
csharp2md ./src --analysis semantic --trust trusted-solution   # enriquecimento semântico completo
```

| Opção | Valores | Padrão | Efeito |
| --- | --- | --- | --- |
| `--analysis` | `syntax-only`, `semantic` | `syntax-only` | `semantic` avalia os projetos MSBuild e faz o binding das compilações Roslyn. |
| `--trust` | `untrusted`, `trusted-solution` | `untrusted` | O modo `semantic` exige `--trust trusted-solution`; omiti-lo relata a exigência de confiança e encerra com código `1` antes de tocar no diretório de saída. |
| `--include-source-generators` | flag | desativado | Executa os geradores de código do próprio projeto (nunca analisadores de diagnóstico). Exige o modo semântico confiável; a combinação inválida encerra com código `1` antes de tocar no diretório de saída. |
| `--analysis-timeout` | duração, ex. `00:05:00` | `00:10:00` | Tempo limite de análise por serviço, positivo. Um valor não positivo ou não interpretável encerra com código `1` antes de tocar no diretório de saída. |

O modo semântico confiável ainda nunca executa restore nem targets
personalizados do MSBuild ou analisadores de diagnóstico — apenas avaliação
de propriedades/itens, compilação e, com opt-in explícito, os geradores de
código do próprio projeto. Ative-o somente para soluções em que você confia.

Qualquer falha na avaliação do projeto, no carregamento do workspace, na
compilação, no binding ou em um único detector degrada apenas o escopo
afetado para seus fatos sintáticos e registra um diagnóstico; a execução
ainda encerra com código `0`, a menos que ocorra uma falha estrutural de
validação (por exemplo, uma identidade de fato duplicada).

## Saída gerada

Toda execução grava em `<saída>/raw/`, junto com um marcador
`.csharp2md-output` na raiz da saída, usado pela ferramenta para reconhecer
diretórios que ela mesma gerencia:

- `raw/codebase/<serviço>/...` — um arquivo `.cs.md` por arquivo de origem
  analisado, reconstruído byte a byte a partir do código-fonte mais
  anotações factuais fora do código, e `components.md`, um índice de
  propriedade das bibliotecas.
- `raw/facts/manifest.json` — versões de esquema e da ferramenta, modo de
  análise solicitado e efetivo, confiança, contagens de cobertura, além da
  referência endereçada por conteúdo e do hash SHA-256 de cada fragmento de
  fato persistido. É o ponto de entrada para qualquer ferramenta que consuma
  a saída do `csharp2md` programaticamente.
- `raw/facts/<tipo>/<prefixo-do-hash>/<hash>.json` — fragmentos de fato
  validados e endereçados por conteúdo (um por solução, projeto, target,
  documento ou símbolo), cada um referenciado a partir do manifesto.
- `raw/facts/relations/{compile-time,inheritance,dependency-injection,http,grpc,events}.json`
  — relações validadas, particionadas por tipo. Um destino em tempo de
  execução não comprovado (por exemplo, uma chamada HTTP cujo destino é
  apenas um nome presente em configuração) mantém o destino `null` e um
  `unresolved_reason` explícito, em vez de arriscar uma correspondência.
- `raw/facts/diagnostics.json`, `raw/facts/coverage.json` — toda degradação
  encontrada durante a execução e a cobertura honesta por escopo
  (not-applicable, unattempted, syntactic, partial, exact ou failed).
- `raw/dependencies.mmd` — um diagrama Mermaid construído a partir das
  relações validadas acima; vazio no modo syntax-only, já que nenhuma relação
  em tempo de execução pode ser confirmada sem binding semântico.
- `raw/topic.yaml`, `raw/CLAUDE.md` — um pequeno scaffold de tópico que aponta
  para `raw/facts/manifest.json`.
- `raw/log.md` — um resumo legível por humanos da execução. É o único arquivo
  gerado que carrega um timestamp, e nunca é fonte de fatos legíveis por
  máquina.

Cada arquivo `.cs.md` gerado carrega um frontmatter YAML (`schema_version: 2`)
com a identidade do documento, projeto, IDs de componente, classificação, um
resumo da análise e um `facts_ref` que resolve para o próprio fragmento de
documento persistido.

## Migrando do v2

A saída do v3 **não é compatível** com o v2: o grafo de dependências plano
`dependencies.json` foi removido, substituído pelos arquivos de relação
validados e particionados em `raw/facts/relations/`, e todo artefato legível
por máquina agora deriva de fatos validados em vez de detectores heurísticos.
Não há modo de compatibilidade nem opção de gravação dupla — aponte os
consumidores downstream para `raw/facts/manifest.json` em vez do antigo
`dependencies.json`.
