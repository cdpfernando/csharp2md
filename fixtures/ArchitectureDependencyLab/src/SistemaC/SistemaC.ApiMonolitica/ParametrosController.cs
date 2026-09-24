using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SistemaC.ApiMonolitica;

public record ParametroResponseDto(string Chave, string Valor, string Fonte);
public record ParametroNaoEncontradoDto(string Mensagem, string LookalikeLocal);

[ApiController]
[Route("v1/parametros")]
public class ParametrosController : ControllerBase
{
    // Acesso direto ao DbContext (padrão monolítico deliberado)
    private readonly SistemaCDbContext _dbContext;
    private readonly ServicoDeCache _servicoDeCache;
    private readonly ServicoAutorizacaoComFallback _autorizacao;
    private readonly ServicoIdentidadeClient _identidadeClient;

    public ParametrosController(
        SistemaCDbContext dbContext,
        ServicoDeCache servicoDeCache,
        ServicoAutorizacaoComFallback autorizacao,
        ServicoIdentidadeClient identidadeClient)
    {
        _dbContext = dbContext;
        _servicoDeCache = servicoDeCache;
        _autorizacao = autorizacao;
        _identidadeClient = identidadeClient;
    }

    // SCENARIO:CALL-C-001
    // SCENARIO:DATA-001
    [HttpGet("{chave}")]
    public async Task<IActionResult> ObterParametro(string chave, CancellationToken cancellationToken)
    {
        // SCENARIO:CACHE-D-001
        var autorizado = await _autorizacao.VerificarAutorizacaoAsync("usuario-anonimo", cancellationToken);
        if (!autorizado)
        {
            return Forbid();
        }

        // SCENARIO:CALL-C-001
        // SCENARIO:CACHE-C-001
        var valorCache = await _servicoDeCache.ObterParametroComCacheAsync(chave, cancellationToken);
        if (valorCache != null)
        {
            return Ok(new ParametroResponseDto(chave, valorCache, "Cache"));
        }

        // SCENARIO:DATA-001
        var parametroBanco = await _dbContext.Parametros
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Chave == chave, cancellationToken);

        if (parametroBanco == null)
        {
            // SCENARIO:PKG-004
            // SCENARIO:NEG-004
            var lookalike = new PacotePrivadoDeParametrosLocal();
            return NotFound(new ParametroNaoEncontradoDto("Parametro nao encontrado", lookalike.NomeIdentificador));
        }

        return Ok(new ParametroResponseDto(parametroBanco.Chave, parametroBanco.Valor, "Banco"));
    }

    // SCENARIO:DATA-005
    [HttpPost("stored-proc/{chave}")]
    public async Task<IActionResult> ExecutarStoredProcedure(string chave, CancellationToken cancellationToken)
    {
        // SCENARIO:DATA-005
        await _dbContext.ExecutarStoredProcedureParametrizadaAsync(chave, cancellationToken);
        return Accepted();
    }
}
