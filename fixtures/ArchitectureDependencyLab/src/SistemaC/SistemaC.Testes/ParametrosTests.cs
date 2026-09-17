using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PacotePrivado.Parametros;
using SistemaC.ApiMonolitica;
using Xunit;

namespace SistemaC.Testes;

public class ParametrosTests
{
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

    private class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Cache indisponível propositalmente.");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Cache indisponível propositalmente.");
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ServicoDeCache_DeveObterDoProviderEArmazenarEmCache()
    {
        // Arrange
        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var distributedCache = new MemoryDistributedCache(memoryOptions);
        var parameterProvider = new DefaultParameterProvider();
        var cacheService = new ServicoDeCache(distributedCache, parameterProvider);

        // Act
        var valor1 = await cacheService.ObterParametroComCacheAsync("TaxaJurosPadrao");

        // Assert
        Assert.Equal("0.05", valor1);

        // Modifica chave diretamente no cache para provar que a segunda chamada bate no cache
        await distributedCache.SetStringAsync("param:TaxaJurosPadrao", "9.99");
        var valor2 = await cacheService.ObterParametroComCacheAsync("TaxaJurosPadrao");
        Assert.Equal("9.99", valor2);
    }

    [Fact]
    public async Task ServicoAutorizacao_ComFalhaDeCache_DevePermitirAcessoPorFallbackPermissivo()
    {
        // Arrange
        var throwingCache = new ThrowingDistributedCache();
        var autorizacaoService = new ServicoAutorizacaoComFallback(throwingCache);

        // Act
        var permitido = await autorizacaoService.VerificarAutorizacaoAsync("usuario-teste");

        // Assert (fallback permissivo: retorna true mesmo com exceção no cache)
        Assert.True(permitido);
    }

    [Fact]
    public async Task ParametrosController_DeveObterDoBancoCasoNaoEstejaEmCache()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<SistemaCDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new SistemaCDbContext(dbOptions);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Parametros.Add(new ParametroSistema
        {
            Id = Guid.NewGuid(),
            Chave = "ChaveDoBanco",
            Valor = "ValorDoBanco123",
            Descricao = "Descricao de teste",
            AtualizadoEm = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var distributedCache = new MemoryDistributedCache(memoryOptions);
        var parameterProvider = new DefaultParameterProvider();
        var cacheService = new ServicoDeCache(distributedCache, parameterProvider);
        var autorizacaoService = new ServicoAutorizacaoComFallback(distributedCache);

        var httpHandler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new ValidarTokenResponse(true, "user", "Admin")), System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("http://localhost:5104") };
        var clientFactory = new FakeHttpClientFactory(httpClient);
        var identidadeClient = new ServicoIdentidadeClient(clientFactory);

        var controller = new ParametrosController(dbContext, cacheService, autorizacaoService, identidadeClient);

        // Act
        var result = await controller.ObterParametro("ChaveDoBanco", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var val = Assert.IsType<ParametroResponseDto>(okResult.Value);
        Assert.Equal("ChaveDoBanco", val.Chave);
        Assert.Equal("ValorDoBanco123", val.Valor);
        Assert.Equal("Banco", val.Fonte);
    }
}
