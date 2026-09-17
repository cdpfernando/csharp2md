using SistemaE.Copia.Infraestrutura;
using SistemaE.Copia.Nucleo;

namespace SistemaE.Copia.Identificacao;

public class IdentificacaoModulo
{
    private readonly SharedInfraAuditService _auditService;

    public IdentificacaoModulo(SharedInfraAuditService auditService)
    {
        _auditService = auditService;
    }

    // SCENARIO:CALL-E-002
    public async Task<ClienteIdentificado> IdentificarClienteAsync(string clienteId, CancellationToken cancellationToken = default)
    {
        // Uso direto de implementacao concreta de infraestrutura compartilhada
        await _auditService.RegistrarPassoModuloAsync("Identificacao", "IdentificarCliente", clienteId, cancellationToken);
        return new ClienteIdentificado(clienteId, $"Cliente {clienteId}", true);
    }
}
