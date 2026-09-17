using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PacotePrivado.Broker;
using PacotePrivado.Parametros;
using SistemaE.Copia.Aplicacao;
using SistemaE.Copia.Formalizacao;
using SistemaE.Copia.Identificacao;
using SistemaE.Copia.Infraestrutura;
using SistemaE.Copia.Oferta;
using SistemaE.Copia.PosProcessamento;
using SistemaE.Copia.Validacao;
using Xunit;

namespace SistemaE.Copia.Testes;

public class VendasTests
{
    private class FakeEventBus : IEventBus
    {
        public List<object> Events { get; } = new();

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        {
            Events.Add(@event!);
            return Task.CompletedTask;
        }

        public Task SubscribeAsync<TEvent, THandler>(CancellationToken cancellationToken = default)
            where THandler : IIntegrationEventHandler<TEvent> => Task.CompletedTask;
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    [Fact]
    public async Task ProcessarVenda_DeveExecutarTodosOsModulosGravarNoBancoEPublicarEvento()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<SistemaEDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new SistemaEDbContext(dbOptions);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var repository = new VendaRepository(dbContext);
        var audit = new SharedInfraAuditService();

        var httpClient = new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("http://localhost:5106") };
        var clientFactory = new FakeHttpClientFactory(httpClient);
        var formalizacaoClient = new ServicoFormalizacaoClient(clientFactory);

        var identificacao = new IdentificacaoModulo(audit);
        var oferta = new OfertaModulo(audit);
        var validacao = new ValidacaoModulo(audit);
        var posProcessamento = new PosProcessamentoModulo(audit);
        var formalizacao = new FormalizacaoModulo(audit, formalizacaoClient, posProcessamento);

        var fakeBus = new FakeEventBus();
        var paramProvider = new DefaultParameterProvider();
        var brokerOptions = Options.Create(new BrokerOptions
        {
            Topic = "vendas-topic",
            SchemaRegistryUrl = "http://localhost:8081"
        });

        var orquestrador = new OrquestradorDeVenda(
            identificacao,
            oferta,
            validacao,
            formalizacao,
            posProcessamento,
            repository,
            fakeBus,
            paramProvider,
            brokerOptions
        );

        var request = new CriarVendaRequest(
            "VND-001",
            "CLI-001",
            new List<ItemOfertaDto>
            {
                new("PROD-A", 150m, 2),
                new("PROD-B", 50m, 1)
            }
        );

        // Act
        var resultado = await orquestrador.ProcessarVendaAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, resultado.VendaId);
        Assert.Equal("VND-001", resultado.Codigo);
        Assert.Equal(350m, resultado.ValorTotal);
        Assert.Equal("Concluida", resultado.Status);

        // Verifica persistencia
        var salva = await repository.ObterPorIdAsync(resultado.VendaId);
        Assert.NotNull(salva);
        Assert.Equal(2, salva.Itens.Count);

        // Verifica evento de integracao publicado (MSG-E-001)
        Assert.Single(fakeBus.Events);
        var eventoPublicado = Assert.IsType<SistemaE.Copia.Nucleo.VendaConcluida>(fakeBus.Events[0]);
        Assert.Equal(resultado.VendaId, eventoPublicado.VendaId);
    }

    [Fact]
    public async Task PedidoRecebidoIntegrationEventHandler_DeveExecutarFluxoDeVendaComSucesso()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<SistemaEDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new SistemaEDbContext(dbOptions);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var repository = new VendaRepository(dbContext);
        var audit = new SharedInfraAuditService();

        var httpClient = new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("http://localhost:5106") };
        var clientFactory = new FakeHttpClientFactory(httpClient);
        var formalizacaoClient = new ServicoFormalizacaoClient(clientFactory);

        var identificacao = new IdentificacaoModulo(audit);
        var oferta = new OfertaModulo(audit);
        var validacao = new ValidacaoModulo(audit);
        var posProcessamento = new PosProcessamentoModulo(audit);
        var formalizacao = new FormalizacaoModulo(audit, formalizacaoClient, posProcessamento);

        var fakeBus = new FakeEventBus();
        var paramProvider = new DefaultParameterProvider();
        var brokerOptions = Options.Create(new BrokerOptions
        {
            Topic = "vendas-topic",
            SchemaRegistryUrl = "http://localhost:8081"
        });

        var orquestrador = new OrquestradorDeVenda(
            identificacao,
            oferta,
            validacao,
            formalizacao,
            posProcessamento,
            repository,
            fakeBus,
            paramProvider,
            brokerOptions
        );

        var handler = new PedidoRecebidoIntegrationEventHandler(orquestrador);
        var pedido = new PedidoRecebido(Guid.NewGuid(), "ClienteIntegracao", 250m, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(pedido);

        // Assert: publicou VendaConcluida
        Assert.Single(fakeBus.Events);
        Assert.IsType<SistemaE.Copia.Nucleo.VendaConcluida>(fakeBus.Events[0]);
    }
}
