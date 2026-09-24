namespace SistemaB.Transversal;

public record ContratoDto(Guid Id, string Numero, decimal ValorTotal, List<ItemContratoDto> Itens);

public record ItemContratoDto(Guid Id, string Descricao, decimal Valor);

public record CriarContratoRequest(string Numero, decimal ValorTotal, List<ItemContratoDto> Itens);

public record AvaliacaoRiscoDto(string Id, string NivelRisco, bool Aprovado);

// SCENARIO:MSG-B-001
public record PedidoRecebido(Guid PedidoId, string Cliente, decimal ValorTotal, DateTime DataCriacao);

// SCENARIO:MSG-GAP-001
// SCENARIO:NEG-006
public record EventoSemConsumidor(Guid EventoId, string Motivo, DateTime Timestamp);

// SCENARIO:CALL-B-002
public interface IContratacaoGateway
{
    Task<bool> VerificarPreliminarAsync(string numeroContrato, CancellationToken cancellationToken = default);
    Task<AvaliacaoRiscoDto> ValidarAsync(decimal valorTotal, CancellationToken cancellationToken = default);
    Task AuditarAsync(Guid contratoId, string status, CancellationToken cancellationToken = default);
}

// SCENARIO:NEG-005
// Lookalike de URL em comentário que não possui call site HTTP real: http://fake-unused-endpoint.local

