using Microsoft.AspNetCore.Mvc;
using SistemaD.Aplicacao;

namespace SistemaD.Api.Controllers;

[ApiController]
[Route("v1/calculos")]
public class CalculosController : ControllerBase
{
    private readonly CalcularPrecoUseCase _useCase;

    public CalculosController(CalcularPrecoUseCase useCase)
    {
        _useCase = useCase;
    }

    // SCENARIO:CALL-D-001
    [HttpPost("processar")]
    public async Task<ActionResult<CalculoResultadoDto>> Calcular([FromBody] CalcularPrecoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _useCase.ExecutarAsync(request, cancellationToken);
        return Ok(resultado);
    }
}
