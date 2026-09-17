namespace SistemaE.Copia.Nucleo;

public class Venda
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataVenda { get; set; }
    public List<ItemVenda> Itens { get; set; } = new();
}

public class ItemVenda
{
    public Guid Id { get; set; }
    public Guid VendaId { get; set; }
    public string ProdutoId { get; set; } = string.Empty;
    public decimal PrecoUnitario { get; set; }
    public int Quantidade { get; set; }
}

public record PropostaVenda(Guid PropostaId, string Codigo, decimal ValorTotal);
public record ClienteIdentificado(string ClienteId, string Nome, bool Regular);

// SCENARIO:MSG-E-001
public record VendaConcluida(Guid VendaId, string Codigo, decimal ValorTotal, DateTime Timestamp);
