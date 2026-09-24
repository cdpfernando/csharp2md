using SistemaD.Aplicacao;
using SistemaD.Nucleo;

namespace SistemaD.Api;

// SCENARIO:MSG-D-002
// SCENARIO:NEG-007
// BackgroundService consumindo o canal in-memory sem broker ou fila externa
public class LoteDePrecoBackgroundProcessor : BackgroundService
{
    private readonly LoteDePrecoChannel _channel;
    private readonly ILogger<LoteDePrecoBackgroundProcessor> _logger;

    public LoteDePrecoBackgroundProcessor(LoteDePrecoChannel channel, ILogger<LoteDePrecoBackgroundProcessor> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando consumidor de Channel<LoteDePreco> em background.");

        try
        {
            // Consome do canal in-memory
            await foreach (var lote in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                _logger.LogInformation("Processando lote de preco {LoteId} categoria {Categoria}", lote.LoteId, lote.Categoria);
                await Task.Delay(10, stoppingToken); // Simulacao de processamento rapido
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumo de canal cancelado.");
        }
    }
}
