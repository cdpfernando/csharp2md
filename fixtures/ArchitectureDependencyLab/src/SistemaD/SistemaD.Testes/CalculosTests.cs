using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PacotePrivado.Parametros;
using SistemaD.Aplicacao;
using SistemaD.Infraestrutura;
using Xunit;

namespace SistemaD.Testes;

public class CalculosTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = JsonSerializer.Serialize(new { valorFinal = 200.00m });
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    [Fact]
    public async Task CalcularPreco_DeveGravarNoBancoAtualizarCacheEGravarNoCanal()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<SistemaDDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new SistemaDDbContext(dbOptions);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var repository = new PrecoRepository(dbContext);

        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var distributedCache = new MemoryDistributedCache(memoryOptions);

        var httpClient = new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("http://localhost:5105") };
        var clientFactory = new FakeHttpClientFactory(httpClient);
        var calculoClient = new ServicoCalculoClient(clientFactory);

        var channel = new LoteDePrecoChannel();
        var parameterProvider = new DefaultParameterProvider();

        var useCase = new CalcularPrecoUseCase(repository, distributedCache, calculoClient, channel, parameterProvider);

        var request = new CalcularPrecoRequest("SKU-999", "ELETRONICOS", 100.00m);

        // Act
        var resultado = await useCase.ExecutarAsync(request);

        // Assert 1: Gravou no banco
        var salvo = await repository.ObterPorCodigoAsync("SKU-999");
        Assert.NotNull(salvo);
        Assert.Equal(resultado.ValorFinal, salvo.Valor);

        // Assert 2: Gravou no cache sem 2PC
        var valorEmCache = await distributedCache.GetStringAsync("preco:SKU-999");
        Assert.NotNull(valorEmCache);
        Assert.Equal(resultado.ValorFinal.ToString(), valorEmCache);

        // Assert 3: Canal em memoria recebeu o lote
        channel.Concluir();
        var lote = await channel.Reader.ReadAsync();
        Assert.NotNull(lote);
        Assert.Equal("ELETRONICOS", lote.Categoria);
        Assert.Single(lote.Itens);
        Assert.Equal("SKU-999", lote.Itens[0].Codigo);
    }

    [Fact]
    public async Task ConsultarPrecos_SQL_DeveFuncionarTantoLiteralQuantoDinamico()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<SistemaDDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new SistemaDDbContext(dbOptions);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Precos.Add(new PrecoRegistro
        {
            Id = Guid.NewGuid(),
            Codigo = "ITEM-SQL",
            Categoria = "TESTE_SQL",
            Valor = 150m,
            AtualizadoEm = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Act 1: Literal
        var literais = await PrecoSqlQueries.ConsultarPorCategoriaLiteralAsync(dbContext, "TESTE_SQL");
        // Act 2: Dinamico
        var dinamicos = await PrecoSqlQueries.ConsultarPorCategoriaDinamicoAsync(dbContext, "TESTE_SQL");

        // Assert
        Assert.Single(literais);
        Assert.Single(dinamicos);
        Assert.Equal("ITEM-SQL", literais[0].Codigo);
        Assert.Equal("ITEM-SQL", dinamicos[0].Codigo);
    }
}
