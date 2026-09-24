using PacotePrivado.Broker;
using SistemaB.Dominio;
using SistemaB.Transversal;

namespace SistemaB.Aplicacao;

public interface IContratacaoUseCase
{
    Task<ContratoDto> ExecutarAsync(CriarContratoRequest request, CancellationToken cancellationToken = default);
}

// SCENARIO:CALL-B-001
public class ContratacaoUseCase : IContratacaoUseCase
{
    private readonly IContratacaoGateway _gateway;
    private readonly IEventBus _eventBus;
    private readonly ContratoCriadoLocalHandler _localHandler;

    public ContratacaoUseCase(
        IContratacaoGateway gateway,
        IEventBus eventBus,
        ContratoCriadoLocalHandler localHandler)
    {
        _gateway = gateway;
        _eventBus = eventBus;
        _localHandler = localHandler;
    }

    // SCENARIO:CALL-B-001
    public async Task<ContratoDto> ExecutarAsync(CriarContratoRequest request, CancellationToken cancellationToken = default)
    {
        // SCENARIO:CALL-B-002
        // Call site 1: Verificação preliminar no gateway
        var preliminarOk = await _gateway.VerificarPreliminarAsync(request.Numero, cancellationToken);
        if (!preliminarOk)
        {
            throw new InvalidOperationException("Falha na verificação preliminar.");
        }

        // SCENARIO:CALL-B-002
        // Call site 2: Validação de risco no mesmo gateway
        var avaliacao = await _gateway.ValidarAsync(request.ValorTotal, cancellationToken);
        if (!avaliacao.Aprovado)
        {
            throw new InvalidOperationException("Contratação rejeitada pela análise de risco.");
        }

        // Aggregate de domínio
        var contrato = new Contrato
        {
            Id = Guid.NewGuid(),
            Numero = request.Numero,
            ValorTotal = request.ValorTotal,
            Itens = request.Itens.Select(i => new ItemContrato
            {
                Id = Guid.NewGuid(),
                Descricao = i.Descricao,
                Valor = i.Valor
            }).ToList()
        };

        // Disparo de evento de domínio local
        await _localHandler.HandleAsync(new ContratoCriadoDominioEvent(contrato.Id, contrato.Numero, contrato.ValorTotal), cancellationToken);

        // SCENARIO:CALL-B-002
        // Call site 3: Auditoria final no mesmo gateway
        await _gateway.AuditarAsync(contrato.Id, "Aprovado", cancellationToken);

        // SCENARIO:MSG-B-001
        // SCENARIO:PKG-002
        // SCENARIO:PKG-003
        await _eventBus.PublishAsync(new PedidoRecebido(contrato.Id, "ClienteSintetico", contrato.ValorTotal, DateTime.UtcNow), cancellationToken);

        // SCENARIO:MSG-GAP-001
        // SCENARIO:PKG-003
        await _eventBus.PublishAsync(new EventoSemConsumidor(Guid.NewGuid(), "AuditoriaSintetica", DateTime.UtcNow), cancellationToken);

        return new ContratoDto(
            contrato.Id,
            contrato.Numero,
            contrato.ValorTotal,
            contrato.Itens.Select(i => new ItemContratoDto(i.Id, i.Descricao, i.Valor)).ToList()
        );
    }
}

// SCENARIO:HOST-003
public class LoggingContratacaoUseCaseDecorator : IContratacaoUseCase
{
    private readonly IContratacaoUseCase _inner;

    public LoggingContratacaoUseCaseDecorator(IContratacaoUseCase inner)
    {
        _inner = inner;
    }

    public async Task<ContratoDto> ExecutarAsync(CriarContratoRequest request, CancellationToken cancellationToken = default)
    {
        // Decorator adicionado via scanning/reflection
        return await _inner.ExecutarAsync(request, cancellationToken);
    }
}
