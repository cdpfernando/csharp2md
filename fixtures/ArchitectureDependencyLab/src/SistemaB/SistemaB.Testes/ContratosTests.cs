using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PacotePrivado.Broker;
using SistemaB.Api.Controllers;
using SistemaB.Aplicacao;
using SistemaB.Dominio;
using SistemaB.Infraestrutura;
using SistemaB.Transversal;
using Xunit;

namespace SistemaB.Testes;

public class ContratosTests
{
    // SCENARIO:NEG-011
    // Adapter de testes em memoria nao e dependencia de broker externo de producao
    private class FakeEventBus : IEventBus
    {
        public List<object> PublishedEvents { get; } = new();

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(@event!);
            return Task.CompletedTask;
        }

        public Task SubscribeAsync<TEvent, THandler>(CancellationToken cancellationToken = default)
            where THandler : IIntegrationEventHandler<TEvent> => Task.CompletedTask;
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public FakeHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    [Fact]
    public async Task CriarContrato_DeveExecutarTresCallSitesDoGatewayEPublicarEventos()
    {
        // Arrange DbContexts
        var dbOptions1 = new DbContextOptionsBuilder<ContratacaoDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var contratacaoDb = new ContratacaoDbContext(dbOptions1);
        await contratacaoDb.Database.OpenConnectionAsync();
        await contratacaoDb.Database.EnsureCreatedAsync();

        var dbOptions2 = new DbContextOptionsBuilder<OperacaoDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var operacaoDb = new OperacaoDbContext(dbOptions2);
        await operacaoDb.Database.OpenConnectionAsync();
        await operacaoDb.Database.EnsureCreatedAsync();

        var contratacaoRepo = new ContratacaoRepository(contratacaoDb);
        var operacaoRepo = new OperacaoRepository(operacaoDb);

        // Arrange HTTP clients
        var httpHandler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("/v1/itens/"))
            {
                var item = new ItemCatalogoDto("item-padrao", "Item Teste", 100m);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(item), System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri.ToString().Contains("/v1/avaliacoes"))
            {
                var avaliacao = new AvaliacaoRiscoDto("RISK-1", "BAIXO", true);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(avaliacao), System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("http://localhost:5102") };
        var clientFactory = new FakeHttpClientFactory(httpClient);

        var catalogoClient = new ServicoCatalogoClient(clientFactory);
        var riscoClient = new ServicoRiscoClient(clientFactory);
        var gateway = new ContratacaoGatewayAdapter(catalogoClient, riscoClient, operacaoRepo);

        var fakeBus = new FakeEventBus();
        var localHandler = new ContratoCriadoLocalHandler();

        var useCase = new ContratacaoUseCase(gateway, fakeBus, localHandler);
        var decoratedUseCase = new LoggingContratacaoUseCaseDecorator(useCase);

        var request = new CriarContratoRequest("CTR-2026-001", 1500.00m, new List<ItemContratoDto>
        {
            new(Guid.NewGuid(), "Item 1", 500m),
            new(Guid.NewGuid(), "Item 2", 1000m)
        });

        // Act
        var resultado = await decoratedUseCase.ExecutarAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("CTR-2026-001", resultado.Numero);
        Assert.Equal(2, resultado.Itens.Count);

        // Verifica que eventos foram publicados
        Assert.Equal(2, fakeBus.PublishedEvents.Count);
        Assert.Contains(fakeBus.PublishedEvents, e => e is PedidoRecebido);
        Assert.Contains(fakeBus.PublishedEvents, e => e is EventoSemConsumidor);

        // Salva contrato no repositório de contratação para testar controller
        var contratoDominio = new Contrato
        {
            Id = resultado.Id,
            Numero = resultado.Numero,
            ValorTotal = resultado.ValorTotal,
            Itens = resultado.Itens.Select(i => new ItemContrato { Id = i.Id, ContratoId = resultado.Id, Descricao = i.Descricao, Valor = i.Valor }).ToList()
        };
        await contratacaoRepo.SalvarContratoAsync(contratoDominio);

        // Testa Controller
        var controller = new ContratosController(decoratedUseCase, contratacaoRepo);
        var actionResult = await controller.ObterContrato(resultado.Id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var contratoObtido = Assert.IsType<ContratoDto>(okResult.Value);
        Assert.Equal(resultado.Id, contratoObtido.Id);
    }
}
