namespace SistemaC.ApiMonolitica;

// SCENARIO:PKG-004
// SCENARIO:NEG-004
// Tipo local apenas nominalmente parecido com o pacote, sem relacao de producao
public class PacotePrivadoDeParametrosLocal
{
    public string NomeIdentificador => "LookalikeLocal_NaoEPacotePrivado";

    public string ObterValorFalso() => "VALOR_SINTETICO_LOCAL";
}
