namespace SistemaA.Aplicacao;

public record PrecoProdutoDto(string ProdutoId, decimal PrecoBase);

public record SimularCotacaoCommand(string ProdutoId, decimal ValorDesejado);

public record CotacaoResultadoDto(Guid Id, string ProdutoId, decimal ValorFinal, decimal TaxaAplicada);
