namespace SistemaB.Dominio;

public class Contrato
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public List<ItemContrato> Itens { get; set; } = new();
}

public class ItemContrato
{
    public Guid Id { get; set; }
    public Guid ContratoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

public class Operacao
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}

public class HistoricoOperacao
{
    public Guid Id { get; set; }
    public Guid OperacaoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateTime DataRegistro { get; set; }
}
