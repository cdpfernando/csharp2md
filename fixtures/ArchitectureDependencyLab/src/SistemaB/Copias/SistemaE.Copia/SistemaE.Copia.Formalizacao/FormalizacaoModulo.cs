using SistemaE.Copia.Infraestrutura;
using SistemaE.Copia.Nucleo;
using SistemaE.Copia.Oferta;
using SistemaE.Copia.PosProcessamento;

namespace SistemaE.Copia.Formalizacao;

public class FormalizacaoModulo
{
    private readonly SharedInfraAuditService _auditService;
    private readonly ServicoFormalizacaoClient _formalizacaoClient;
    private readonly PosProcessamentoModulo _posProcessamento;

    public FormalizacaoModulo(
        SharedInfraAuditService auditService,
        ServicoFormalizacaoClient formalizacaoClient,
        PosProcessamentoModulo posProcessamento)
    {
        _auditService = auditService;
        _formalizacaoClient = formalizacaoClient;
        _posProcessamento = posProcessamento;
    }

    // SCENARIO:CALL-E-002
    public async Task<bool> FormalizarVendaAsync(Venda venda, CancellationToken cancellationToken = default)
    {
        // Uso direto de implementacao concreta de infraestrutura compartilhada
        await _auditService.RegistrarPassoModuloAsync("Formalizacao", "FormalizarVenda", venda.Codigo, cancellationToken);

        // SCENARIO:HTTP-E-002
        var proposta = new PropostaVenda(venda.Id, venda.Codigo, venda.ValorTotal);
        var enviado = await _formalizacaoClient.EnviarPropostaAsync(proposta, cancellationToken);

        if (enviado)
        {
            await _posProcessamento.PosProcessarVendaAsync(venda.Id, cancellationToken);
        }

        return enviado;
    }
}
