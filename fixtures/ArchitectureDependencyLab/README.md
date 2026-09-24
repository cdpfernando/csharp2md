# architecture-dependency-lab

> [!IMPORTANT]
> **AVISO**: Esta codebase é 100% sintética, compilável e vinculável semanticamente. O objetivo é servir como corpus de validação e calibração para analisadores estáticos de arquitetura C#/.NET 10 (como o `csharp2md`), tornando observáveis dependências explícitas, inversões indevidas, clientes HTTP, brokers, canais em memória, conexões de banco e lookalikes. Não deve ser utilizada como produto ou base de produção.

---

## 1. Mapa dos Cinco Sistemas e da Cópia Aninhada

O repositório contém 6 soluções (`.slnx`) independentes, 41 projetos C# no total e exatamente 87 `ProjectReference`:

```text
src/
├── SistemaA/ (5 projetos, 4 ProjectReferences)
│   ├── SistemaA.Api
│   ├── SistemaA.Aplicacao (inversão proposital -> Infraestrutura)
│   ├── SistemaA.Infraestrutura
│   ├── SistemaA.Nucleo
│   └── SistemaA.Testes
│
├── SistemaB/ (7 projetos, 18 ProjectReferences)
│   ├── SistemaB.Api
│   ├── SistemaB.Aplicacao
│   ├── SistemaB.Dominio
│   ├── SistemaB.Infraestrutura
│   ├── SistemaB.Composicao (DI via extension methods, scanning de decorator e profiles)
│   ├── SistemaB.Transversal
│   ├── SistemaB.Testes
│   └── Copias/
│       └── SistemaE.Copia/ (10 projetos, 28 ProjectReferences) [ISOLADO]
│           └── SistemaE.Copia.slnx (NÃO referenciado pelo SistemaB nem incluído em SistemaB.slnx)
│
├── SistemaC/ (2 projetos, 1 ProjectReference)
│   ├── SistemaC.ApiMonolitica (monolito acessando DbContext e Cache diretamente)
│   └── SistemaC.Testes
│
├── SistemaD/ (5 projetos, 8 ProjectReferences)
│   ├── SistemaD.Api (BackgroundService consumindo Channel<T>, #if CONDITIONAL_AUTH)
│   ├── SistemaD.Aplicacao
│   ├── SistemaD.Infraestrutura (SQL literal parametrizado + SQL dinâmico concatenado)
│   ├── SistemaD.Nucleo
│   └── SistemaD.Testes
│
└── SistemaE/ (10 projetos, 28 ProjectReferences)
    ├── SistemaE.Api
    ├── SistemaE.Aplicacao (Orquestrador de venda despachando para módulos)
    ├── SistemaE.Nucleo
    ├── SistemaE.Infraestrutura (usada diretamente por todos os módulos)
    ├── SistemaE.Formalizacao
    ├── SistemaE.Identificacao
    ├── SistemaE.Oferta
    ├── SistemaE.PosProcessamento
    ├── SistemaE.Validacao
    └── SistemaE.Testes
```

Adicionalmente:
- `src/ArtefatoVazio/`: diretório deliberadamente vazio contendo apenas `.gitkeep` (`STR-002`).
- `package-source/`: contém o código-fonte de `PacotePrivado.Parametros` e `PacotePrivado.Broker`, empacotados em `local-feed/` e consumidos estritamente por `PackageReference`.

---

## 2. Comandos de Build e Teste

### PowerShell (Windows)
```powershell
./build.ps1
```

### Bash (Linux / macOS)
```bash
./build.sh
```

### Execução Manual por Solução
```bash
# 1. Empacotar pacotes no feed local
dotnet pack package-source/PacotePrivado.Parametros/PacotePrivado.Parametros.csproj -o local-feed -c Release
dotnet pack package-source/PacotePrivado.Broker/PacotePrivado.Broker.csproj -o local-feed -c Release

# 2. Restaurar, compilar e testar individualmente
dotnet test src/SistemaA/SistemaA.slnx -c Release
dotnet test src/SistemaB/SistemaB.slnx -c Release
dotnet test src/SistemaC/SistemaC.slnx -c Release
dotnet test src/SistemaD/SistemaD.slnx -c Release
dotnet test src/SistemaE/SistemaE.slnx -c Release
dotnet test src/SistemaB/Copias/SistemaE.Copia/SistemaE.Copia.slnx -c Release
```

---

## 3. Diretrizes de Análise Estática

1. **Análise Isolada de Cada Solução**: Cada `.slnx` representa uma fronteira de compilação independente. O analisador deve ser executado para cada solução separadamente e não deve mesclar grafos entre soluções.
2. **Nomes Iguais Não Comprovam Identidade Compartilhada**: As conexões Primary e Secondary compartilham o nome de arquivo `synthetic_data.db` sem que sejam o mesmo Data Store. Tipos semelhantes (como `PacotePrivadoDeParametrosLocal` no SistemaC) são lookalikes nominais que não possuem qualquer relação com o pacote real `PacotePrivado.Parametros`.
3. **Cópia Aninhada Isolada**: `src/SistemaB/Copias/SistemaE.Copia` existe fisicamente dentro da pasta do SistemaB, porém sua solução `SistemaE.Copia.slnx` é totalmente desacoplada e independente.

---

## 4. Lista de Lacunas e Anti-Patterns Deliberados

- **Inversão Arquitetural Indevida**: `SistemaA.Aplicacao` referencia diretamente `SistemaA.Infraestrutura`.
- **Monolito Sem Camadas**: `SistemaC.ApiMonolitica` concentra controladores, acesso direto ao DbContext e serviços de cache no mesmo projeto.
- **SQL Dinâmico Inseguro**: `SistemaD.Infraestrutura.PrecoSqlQueries` concatena strings dinamicamente para consulta SQL.
- **Gravação em Duas Etapas Sem 2PC**: `CalcularPrecoUseCase` salva no banco e atualiza o cache em seguida sem transação distribuída.
- **Canal em Memória Sem Broker**: `SistemaD` utiliza `System.Threading.Channels.Channel<T>` consumido por um `BackgroundService` em memória, simulando processamento assíncrono sem broker externo.
- **Evento Sem Consumidor**: `SistemaB` publica `EventoSemConsumidor` sem nenhum assinante correspondente.
- **Migrações Parciais**: Apenas `SistemaA`, `SistemaB (ContratacaoDbContext)` e `SistemaE` possuem migrações geradas; `OperacaoDbContext`, `SistemaC` e `SistemaD` foram deliberadamente deixados sem migrações.
- **Fallback Permissivo**: Em caso de falha no serviço de cache/autorização do SistemaC, o sistema permite o acesso preventivamente.
- **CORS Aberto**: `SistemaB` possui política CORS com `AllowAnyOrigin`, `AllowAnyMethod` e `AllowAnyHeader`.

---

## 5. Tabela de Rastreabilidade de Cenários (`SCENARIO:<id>`)

| ID | Categoria | Estado Esperado | Arquivo / Símbolo | Descrição |
|---|---|---|---|---|
| `STR-001` | structural | `confirmed` | `Directory.Build.props` / `project-references.json` | Mapeamento exato das 87 Project References |
| `STR-002` | structural | `confirmed` | `src/ArtefatoVazio/.gitkeep` | Diretório vazio intencional |
| `STR-003` | structural | `confirmed` | `src/SistemaB/Copias/SistemaE.Copia/README.md` | Cópia aninhada isolada |
| `CALL-A-001` | internal-invocation | `confirmed` | `CotacoesController.Simular` -> `SimularCotacaoHandler` | Chamada de controller para handler |
| `CALL-B-001` | internal-invocation | `confirmed` | `ContratosController.CriarContrato` -> `ContratacaoUseCase` | Chamada de controller para use case |
| `CALL-B-002` | internal-invocation | `confirmed` | `ContratacaoUseCase.ExecutarAsync` | Gateway invocado em 3 call sites |
| `CALL-C-001` | internal-invocation | `confirmed` | `ParametrosController.ObterParametro` | Acesso direto a DbContext e ServicoDeCache |
| `CALL-D-001` | internal-invocation | `confirmed` | `CalculosController.Calcular` -> `CalcularPrecoUseCase` | Invocação de caso de uso |
| `CALL-E-001` | internal-invocation | `confirmed` | `VendasController` -> `OrquestradorDeVenda` | Despacho para múltiplos módulos |
| `CALL-E-002` | internal-invocation | `confirmed` | Módulos -> `SharedInfraAuditService` | Módulos usando infraestrutura concreta |
| `PKG-001` | package-dependency | `confirmed` | `SimularCotacaoHandler` | Consumo de `PacotePrivado.Parametros` |
| `PKG-002` | package-dependency | `confirmed` | `SistemaBComposicaoExtensions` | Referência a `PacotePrivado.Broker` |
| `PKG-003` | package-dependency | `confirmed` | `ContratacaoUseCase.ExecutarAsync` | Publicação via `IEventBus` |
| `PKG-004` | lookalike | `candidate` | `PacotePrivadoDeParametrosLocal.cs` | Lookalike nominal local |
| `HTTP-A-001` | http-invocation | `confirmed` | `ServicoPrecosClient.cs` | `GET /v1/precos/{produtoId}` |
| `HTTP-B-001` | http-invocation | `confirmed` | `ServicoCatalogoClient.cs` | `GET /v1/itens/{id}` com 4 retries |
| `HTTP-B-002` | http-invocation | `confirmed` | `ServicoRiscoClient.cs` | `POST /v1/avaliacoes` com circuit breaker |
| `HTTP-C-001` | http-invocation | `confirmed` | `ServicoIdentidadeClient.cs` | `POST /v1/tokens/validar` |
| `HTTP-D-001` | http-invocation | `confirmed` | `ServicoCalculoClient.cs` | `POST /v1/calculos` |
| `HTTP-E-001` | http-invocation | `confirmed` | `ServicoCatalogoClient.cs` | `GET /v1/itens/{itemId}` |
| `HTTP-E-002` | http-invocation | `confirmed` | `ServicoFormalizacaoClient.cs` | `POST /v1/propostas` |
| `MSG-B-001` | message-publication | `confirmed` | `ContratacaoUseCase.cs` | `IEventBus.PublishAsync<PedidoRecebido>` |
| `MSG-E-001` | message-publication | `confirmed` | `OrquestradorDeVenda.cs` | `IEventBus.PublishAsync<VendaConcluida>` |
| `MSG-E-002` | message-subscription | `confirmed` | `PedidoRecebidoIntegrationEventHandler.cs` | `IIntegrationEventHandler<PedidoRecebido>` |
| `MSG-D-001` | in-memory-channel | `confirmed` | `LoteDePrecoChannel.cs` | In-memory `Channel<LoteDePreco>` |
| `MSG-D-002` | in-memory-channel | `confirmed` | `LoteDePrecoBackgroundProcessor.cs` | BackgroundService consumindo canal |
| `MSG-GAP-001` | message-gap | `open-frontier` | `ContratacaoUseCase.cs` | `EventoSemConsumidor` sem subscriber |
| `MSG-GAP-002` | message-gap | `candidate` | `OrquestradorDeVenda.cs` | Publicação dependente de topic e registry |
| `DATA-001` | data-access | `confirmed` | Contextos EF Core | Definição de `DbContext` e `DbSet<T>` |
| `DATA-002` | data-access | `confirmed` | `OnModelCreating` | Mapeamento fluente `ToTable` / `HasColumnName` |
| `DATA-003` | data-access | `confirmed` | `Program.cs` / `appsettings.json` | Configuração Primary e Secondary |
| `DATA-004` | data-access | `confirmed` | Repositórios | LINQ, `.Include()` e `SaveChangesAsync` |
| `DATA-005` | data-access | `confirmed` | `OperacaoRepository.cs` | SQL literal parametrizado |
| `DATA-006` | data-access | `candidate` | `PrecoSqlQueries.cs` | SQL dinâmico por concatenação |
| `DATA-007` | data-access | `open-frontier` | Pastas `Migrations/` | Migrações parciais |
| `DATA-008` | data-access | `unknown` | `appsettings.json` | Primary e Secondary com mesmo nome sintético de banco |
| `CACHE-C-001` | cache | `confirmed` | `ServicoDeCache.cs` | Uso de `IDistributedCache` |
| `CACHE-D-001` | cache-resilience | `candidate` | `ServicoAutorizacaoComFallback.cs` | Fallback permissivo de autorização |
| `CACHE-002` | cache | `confirmed` | `ServicoDeCache.cs` | Leitura/escrita em cache com provider |
| `CACHE-003` | cache | `candidate` | `CalcularPrecoUseCase.cs` | Salva no banco e atualiza cache sem 2PC |
| `CFG-001` | configuration | `confirmed` | `appsettings.json` / `Program.cs` | Chaves obrigatórias de configuração |
| `CFG-002` | configuration | `confirmed` | `appsettings.json` | Sentinelas falsos e chave declarada/não lida |
| `CFG-003` | configuration | `candidate` | `SistemaD.Api/Program.cs` | Configuração sob `#if CONDITIONAL_AUTH` |
| `CFG-004` | configuration | `candidate` | `SistemaA.Api/Program.cs` | Composição de chave por interpolação |
| `CFG-005` | configuration | `confirmed` | `appsettings.Development.json` | Divergência segura entre ambientes |
| `HOST-001` | hosting | `confirmed` | `deploy/` e `pipelines/` | Manifestos de deploy e pipelines |
| `HOST-002` | hosting | `confirmed` | `SistemaBComposicaoExtensions.cs` | Registro de DI por extension methods |
| `HOST-003` | hosting | `candidate` | `SistemaBComposicaoExtensions.cs` | Decorator registrado por reflexão |
| `HOST-004` | hosting | `candidate` | `SistemaB.Dominio/Perfis.cs` | Perfis genéricos por reflexão |
| `HOST-005` | hosting | `candidate` | `SistemaB.Api/Program.cs` | Política CORS ampla deliberada |
| `HOST-006` | hosting | `confirmed` | `ContratosController.cs` | Ação com rotas múltiplas |
| `NEG-001` | negative | `absent` | `SistemaA.Api.csproj` | Ausência de referência para SistemaB |
| `NEG-002` | negative | `absent` | `SistemaB.Api.csproj` | Ausência de referência para SistemaE.Copia |
| `NEG-003` | negative | `absent` | `SistemaA.Api.csproj` | Ausência de referência Api -> Infraestrutura |
| `NEG-004` | negative | `absent` | `PacotePrivadoDeParametrosLocal.cs` | Lookalike de pacote privado |
| `NEG-005` | negative | `absent` | `Contratos.cs` | Lookalike de URL em comentário |
| `NEG-006` | negative | `absent` | `Contratos.cs` | Evento sem handler registrado |
| `NEG-007` | negative | `absent` | `LoteDePrecoChannel.cs` | Canal em memória não é broker externo |
| `NEG-008` | negative | `absent` | `SistemaBComposicaoExtensions.cs` | Reflexão não resolve serviço inexistente |
| `NEG-009` | negative | `absent` | `SistemaCDbContext.cs` | Stored procedure não é SQL injection |
| `NEG-010` | negative | `absent` | `SistemaA.Api/Program.cs` | Conexões separadas não são cluster |
| `NEG-011` | negative | `absent` | `ContratosTests.FakeEventBus` | Fake de teste não é broker de produção |
| `NEG-012` | negative | `absent` | `appsettings.json` | Sentinela não é vazamento de credencial real |
