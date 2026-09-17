using SistemaE.Copia.Infraestrutura;
using SistemaE.Copia.Nucleo;

namespace SistemaE.Copia.PosProcessamento;

public class PosProcessamentoModulo
{
    private readonly SharedInfraAuditService _auditService;

    public PosProcessamentoModulo(SharedInfraAuditService auditService)
    {
        _auditService = auditService;
    }

    // SCENARIO:CALL-E-002
    public async Task PosProcessarVendaAsync(Guid vendaId, CancellationToken cancellationToken = default)
    {
        // Uso direto de implementacao concreta de infraestrutura compartilhada
        await _auditService.RegistrarPassoModuloAsync("PosProcessamento", "FinalizarVenda", vendaId.ToString(), cancellationToken);
    }
}
