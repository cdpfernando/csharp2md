using SistemaE.Infraestrutura;
using SistemaE.Nucleo;

namespace SistemaE.Validacao;

public class ValidacaoModulo
{
    private readonly SharedInfraAuditService _auditService;

    public ValidacaoModulo(SharedInfraAuditService auditService)
    {
        _auditService = auditService;
    }

    // SCENARIO:CALL-E-002
    public async Task<bool> ValidarRegrasAsync(string clienteId, decimal valorTotal, CancellationToken cancellationToken = default)
    {
        // Uso direto de implementacao concreta de infraestrutura compartilhada
        await _auditService.RegistrarPassoModuloAsync("Validacao", "ValidarRegras", $"{clienteId}:{valorTotal}", cancellationToken);
        return valorTotal > 0;
    }
}
