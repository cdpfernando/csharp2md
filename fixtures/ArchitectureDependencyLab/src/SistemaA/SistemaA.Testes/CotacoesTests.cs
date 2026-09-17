using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PacotePrivado.Parametros;
using SistemaA.Aplicacao;
using SistemaA.Infraestrutura;
using Xunit;

namespace SistemaA.Testes;

public class CotacoesTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
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
    public async Task SimularCotacao_DeveCalcularValorESalvarComSucesso()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<SistemaADbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var dbContext = new SistemaADbContext(dbOptions);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var repository = new CotacaoRepository(dbContext);

        var precoDto = new PrecoProdutoDto("PROD-001", 100.00m);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(precoDto), System.Text.Encoding.UTF8, "application/json")
        };
        var fakeHandler = new FakeHttpMessageHandler(httpResponse);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://localhost:5101") };
        var clientFactory = new FakeHttpClientFactory(httpClient);
        var precosClient = new ServicoPrecosClient(clientFactory);

        var paramProvider = new DefaultParameterProvider();
        var handler = new SimularCotacaoHandler(precosClient, repository, paramProvider);

        var command = new SimularCotacaoCommand("PROD-001", 50.00m);

        // Act
        var resultado = await handler.HandleAsync(command);

        // Assert
        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("PROD-001", resultado.ProdutoId);
        Assert.True(resultado.ValorFinal > 0);

        var saved = await repository.GetByIdAsync(resultado.Id);
        Assert.NotNull(saved);
        Assert.Equal(resultado.Id, saved.Id);
    }
}
