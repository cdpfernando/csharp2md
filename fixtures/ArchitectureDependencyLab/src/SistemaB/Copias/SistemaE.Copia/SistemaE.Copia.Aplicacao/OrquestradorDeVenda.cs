using Microsoft.Extensions.Options;
using PacotePrivado.Broker;
using PacotePrivado.Parametros;
using SistemaE.Copia.Formalizacao;
using SistemaE.Copia.Identificacao;
using SistemaE.Copia.Infraestrutura;
using SistemaE.Copia.Nucleo;
using SistemaE.Copia.Oferta;
using SistemaE.Copia.PosProcessamento;
using SistemaE.Copia.Validacao;

namespace SistemaE.Copia.Aplicacao;

public record CriarVendaRequest(string Codigo, string ClienteId, List<ItemOfertaDto> Itens);
public record VendaResultadoDto(Guid VendaId, string Codigo, decimal ValorTotal, string Status);

// SCENARIO:CALL-E-001
public class OrquestradorDeVenda
{
    private readonly IdentificacaoModulo _identificacao;
    private readonly OfertaModulo _oferta;
    private readonly ValidacaoModulo _validacao;
    private readonly FormalizacaoModulo _formalizacao;
    private readonly PosProcessamentoModulo _posProcessamento;
    private readonly VendaRepository _vendaRepository;
    private readonly IEventBus _eventBus;
    private readonly IParameterProvider _parameterProvider;
    private readonly BrokerOptions _brokerOptions;

    public OrquestradorDeVenda(
        IdentificacaoModulo identificacao,
        OfertaModulo oferta,
        ValidacaoModulo validacao,
        FormalizacaoModulo formalizacao,
        PosProcessamentoModulo posProcessamento,
        VendaRepository vendaRepository,
        IEventBus eventBus,
        IParameterProvider parameterProvider,
        IOptions<BrokerOptions> brokerOptions)
    {
        _identificacao = identificacao;
        _oferta = oferta;
        _validacao = validacao;
        _formalizacao = formalizacao;
        _posProcessamento = posProcessamento;
        _vendaRepository = vendaRepository;
        _eventBus = eventBus;
        _parameterProvider = parameterProvider;
        _brokerOptions = brokerOptions.Value;
    }

    // SCENARIO:CALL-E-001
    public async Task<VendaResultadoDto> ProcessarVendaAsync(CriarVendaRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Identificacao
        var cliente = await _identificacao.IdentificarClienteAsync(request.ClienteId, cancellationToken);

        // 2. Oferta
        var oferta = await _oferta.CalcularOfertaAsync(request.Itens, cancellationToken);

        // 3. Validacao
        var valido = await _validacao.ValidarRegrasAsync(cliente.ClienteId, oferta.ValorTotal, cancellationToken);
        if (!valido)
        {
            throw new InvalidOperationException("Venda nao passou na validacao de regras.");
        }

        var venda = new Venda
        {
            Id = Guid.NewGuid(),
            Codigo = request.Codigo,
            ClienteId = request.ClienteId,
            ValorTotal = oferta.ValorTotal,
            Status = "EmFormalizacao",
            DataVenda = DateTime.UtcNow,
            Itens = oferta.Itens.Select(i => new ItemVenda
            {
                Id = Guid.NewGuid(),
                ProdutoId = i.ProdutoId,
                PrecoUnitario = i.PrecoUnitario,
                Quantidade = i.Quantidade
            }).ToList()
        };

        // 4. Formalizacao (que internamente chama PosProcessamento)
        var formalizado = await _formalizacao.FormalizarVendaAsync(venda, cancellationToken);
        venda.Status = formalizado ? "Concluida" : "Pendente";

        // Salva venda no banco
        await _vendaRepository.SalvarAsync(venda, cancellationToken);

        // SCENARIO:MSG-GAP-002
        // Publicação que depende de configuração de tópico e schema registry
        if (string.IsNullOrWhiteSpace(_brokerOptions.Topic) || string.IsNullOrWhiteSpace(_brokerOptions.SchemaRegistryUrl))
        {
            throw new InvalidOperationException("Configuracao de mensageria incompleta (Topic ou SchemaRegistryUrl faltando).");
        }

        // SCENARIO:MSG-E-001
        // SCENARIO:PKG-002
        // SCENARIO:PKG-003
        await _eventBus.PublishAsync(new VendaConcluida(venda.Id, venda.Codigo, venda.ValorTotal, DateTime.UtcNow), cancellationToken);

        return new VendaResultadoDto(venda.Id, venda.Codigo, venda.ValorTotal, venda.Status);
    }
}

// SCENARIO:MSG-E-002
// SCENARIO:PKG-002
// SCENARIO:PKG-003
public record PedidoRecebido(Guid PedidoId, string Cliente, decimal ValorTotal, DateTime DataCriacao);

public class PedidoRecebidoIntegrationEventHandler : IIntegrationEventHandler<PedidoRecebido>
{
    private readonly OrquestradorDeVenda _orquestrador;

    public PedidoRecebidoIntegrationEventHandler(OrquestradorDeVenda orquestrador)
    {
        _orquestrador = orquestrador;
    }

    // SCENARIO:MSG-E-002
    public async Task HandleAsync(PedidoRecebido @event, CancellationToken cancellationToken = default)
    {
        var request = new CriarVendaRequest(
            $"VENDA-{@event.PedidoId.ToString()[..8]}",
            @event.Cliente,
            new List<ItemOfertaDto> { new("PROD-AUTOMATICO", @event.ValorTotal, 1) }
        );

        await _orquestrador.ProcessarVendaAsync(request, cancellationToken);
    }
}
