namespace SistemaB.Dominio;

public record ContratoCriadoDominioEvent(Guid ContratoId, string Numero, decimal ValorTotal);

public class ContratoCriadoLocalHandler
{
    public Task HandleAsync(ContratoCriadoDominioEvent @event, CancellationToken cancellationToken = default)
    {
        // Processamento assíncrono local de evento de domínio
        return Task.CompletedTask;
    }
}
