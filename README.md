# csharp2md

`csharp2md` converte um projeto ou uma base C#/.NET em arquivos Markdown e gera
índices e uma visão consolidada das dependências entre serviços.

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

## Saída gerada

Além de um arquivo `.cs.md` para cada fonte C#, a saída contém:

- `index.md` na raiz e em cada serviço;
- `dependencies.json` com o grafo de dependências;
- `dependencies.mmd` com o diagrama Mermaid;
- `.csharp2md-output`, que identifica um diretório gerenciado pela ferramenta.
