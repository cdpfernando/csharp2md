namespace SistemaB.Dominio;

// SCENARIO:HOST-004
public interface IProfile<T>
{
    string NomePerfil { get; }
    void Configurar(T entidade);
}

// SCENARIO:HOST-004
public class ContratoProfile<T> : IProfile<T> where T : Contrato
{
    public string NomePerfil => "PerfilContratoPadrao";

    public void Configurar(T entidade)
    {
        entidade.ValorTotal = Math.Round(entidade.ValorTotal, 2);
    }
}

// SCENARIO:HOST-004
public class OperacaoProfile<T> : IProfile<T> where T : Operacao
{
    public string NomePerfil => "PerfilOperacaoPadrao";

    public void Configurar(T entidade)
    {
        if (string.IsNullOrWhiteSpace(entidade.Descricao))
        {
            entidade.Descricao = "Operacao Sem Descricao";
        }
    }
}
