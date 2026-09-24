namespace SistemaA.Nucleo;

public class Cotacao
{
    public Guid Id { get; set; }
    public string ProdutoId { get; set; } = string.Empty;
    public decimal ValorCalculado { get; set; }
    public decimal Taxa { get; set; }
    public DateTime DataCriacao { get; set; }
}
