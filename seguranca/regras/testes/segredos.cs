// Casos de teste das regras de segredos em código (semgrep --test). Valores falsos.
public class Config
{
    // ruleid: hakari.segredo.literal-em-codigo
    private const string ChaveJwt = "nao-use-isso-123456";
    // ok: hakari.segredo.literal-em-codigo
    public const string Mensagem = "A senha precisa ter pelo menos 8 caracteres, com letras e números";
    // ok: hakari.segredo.literal-em-codigo
    private const string WIFI_SENHA = "";
    // ruleid: hakari.segredo.formato-conhecido, hakari.segredo.literal-em-codigo
    var chave = "AKIAIOSFODNN7EXAMPLE"; // chave de exemplo da documentação da AWS
}

public class ChavesDeArmazenamento
{
    // ok: hakari.segredo.literal-em-codigo
    private const string CHAVE_SESSAO = "hakari.sessao";
}
