using Microsoft.AspNetCore.Mvc;
using SistemaB.Aplicacao;
using SistemaB.Infraestrutura;
using SistemaB.Transversal;

namespace SistemaB.Api.Controllers;

[ApiController]
[Route("v1")]
public class ContratosController : ControllerBase
{
    private readonly IContratacaoUseCase _useCase;
    private readonly ContratacaoRepository _repository;

    public ContratosController(IContratacaoUseCase useCase, ContratacaoRepository repository)
    {
        _useCase = useCase;
        _repository = repository;
    }

    // SCENARIO:CALL-B-001
    [HttpPost("contratos")]
    public async Task<ActionResult<ContratoDto>> CriarContrato([FromBody] CriarContratoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _useCase.ExecutarAsync(request, cancellationToken);
        return Ok(resultado);
    }

    // SCENARIO:HOST-006
    // Uma ação de controller com duas rotas para o mesmo método
    [HttpGet("contratos/{id:guid}")]
    [HttpGet("v2/contratos/{id:guid}")]
    public async Task<ActionResult<ContratoDto>> ObterContrato(Guid id, CancellationToken cancellationToken)
    {
        var contrato = await _repository.ObterPorIdAsync(id, cancellationToken);
        if (contrato == null)
        {
            return NotFound();
        }

        return Ok(new ContratoDto(
            contrato.Id,
            contrato.Numero,
            contrato.ValorTotal,
            contrato.Itens.Select(i => new ItemContratoDto(i.Id, i.Descricao, i.Valor)).ToList()
        ));
    }
}
