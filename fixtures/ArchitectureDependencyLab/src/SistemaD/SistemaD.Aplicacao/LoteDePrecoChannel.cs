using System.Threading.Channels;
using SistemaD.Nucleo;

namespace SistemaD.Aplicacao;

// SCENARIO:MSG-D-001
// SCENARIO:NEG-007
// Canal in-memory Channel<T> sem dependencia de fila ou broker externo
public class LoteDePrecoChannel
{
    private readonly Channel<LoteDePreco> _channel;

    public LoteDePrecoChannel()
    {
        _channel = Channel.CreateUnbounded<LoteDePreco>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async Task EscreverAsync(LoteDePreco lote, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(lote, cancellationToken);
    }

    public ChannelReader<LoteDePreco> Reader => _channel.Reader;

    public void Concluir() => _channel.Writer.Complete();
}
