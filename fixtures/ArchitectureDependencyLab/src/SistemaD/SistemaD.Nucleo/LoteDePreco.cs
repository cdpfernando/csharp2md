namespace SistemaD.Nucleo;

public record PrecoItem(Guid Id, string Codigo, decimal Valor);

public record LoteDePreco(Guid LoteId, string Categoria, decimal ValorBase, List<PrecoItem> Itens, DateTime CriadoEm);
