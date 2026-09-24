using Microsoft.AspNetCore.Mvc;
using SistemaE.Aplicacao;

namespace SistemaE.Api.Controllers;

[ApiController]
[Route("v1/vendas")]
public class VendasController : ControllerBase
{
    private readonly OrquestradorDeVenda _orquestrador;

    public VendasController(OrquestradorDeVenda orquestrador)
    {
        _orquestrador = orquestrador;
    }

    // SCENARIO:CALL-E-001
    [HttpPost("processar")]
    public async Task<ActionResult<VendaResultadoDto>> ProcessarVenda([FromBody] CriarVendaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _orquestrador.ProcessarVendaAsync(request, cancellationToken);
        return Ok(resultado);
    }
}
