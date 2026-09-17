using SistemaE.Infraestrutura;
using SistemaE.Nucleo;

namespace SistemaE.Oferta;

public record ItemOfertaDto(string ProdutoId, decimal PrecoUnitario, int Quantidade);
public record OfertaCalculada(decimal ValorTotal, List<ItemOfertaDto> Itens);

public class OfertaModulo
{
    private readonly SharedInfraAuditService _auditService;

    public OfertaModulo(SharedInfraAuditService auditService)
    {
        _auditService = auditService;
    }

    // SCENARIO:CALL-E-002
    public async Task<OfertaCalculada> CalcularOfertaAsync(List<ItemOfertaDto> itens, CancellationToken cancellationToken = default)
    {
        // Uso direto de implementacao concreta de infraestrutura compartilhada
        await _auditService.RegistrarPassoModuloAsync("Oferta", "CalcularOferta", $"TotalItens:{itens.Count}", cancellationToken);
        var total = itens.Sum(i => i.PrecoUnitario * i.Quantidade);
        return new OfertaCalculada(total, itens);
    }
}
