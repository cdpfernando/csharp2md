using Microsoft.AspNetCore.Mvc;
using SistemaA.Aplicacao;

namespace SistemaA.Api.Controllers;

[ApiController]
[Route("v1/cotacoes")]
public class CotacoesController : ControllerBase
{
    private readonly SimularCotacaoHandler _handler;

    public CotacoesController(SimularCotacaoHandler handler)
    {
        _handler = handler;
    }

    // SCENARIO:CALL-A-001
    [HttpPost("simular")]
    public async Task<ActionResult<CotacaoResultadoDto>> Simular([FromBody] SimularCotacaoCommand command, CancellationToken cancellationToken)
    {
        var resultado = await _handler.HandleAsync(command, cancellationToken);
        return Ok(resultado);
    }
}
