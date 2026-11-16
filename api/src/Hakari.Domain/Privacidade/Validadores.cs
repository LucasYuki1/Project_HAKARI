namespace Hakari.Domain.Privacidade;

/// <summary>
/// Validadores dos valores encontrados pelas regras. Um número com cara de CPF só conta como CPF se
/// os dígitos verificadores baterem; é isso que separa um CPF de um número de protocolo.
/// </summary>
public static class Validadores
{
    // DDDs válidos no Brasil (Anatel).
    private static readonly HashSet<int> Ddds =
    [
        11, 12, 13, 14, 15, 16, 17, 18, 19, 21, 22, 24, 27, 28, 31, 32, 33, 34, 35, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48, 49, 51, 53, 54, 55, 61, 62, 63, 64, 65, 66, 67, 68, 69,
        71, 73, 74, 75, 77, 79, 81, 82, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 96, 97, 98, 99
    ];

    public static bool Validar(string? validador, string valor) => validador switch
    {
        null or "" => true,
        "cpf" => Cpf(valor),
        "cns" => Cns(valor),
        "luhn" => Luhn(valor),
        "telefone" => Telefone(valor),
        _ => throw new InvalidOperationException($"Validador desconhecido em regras-pii.json: {validador}")
    };

    /// <summary>CPF: 11 dígitos, não todos iguais, com os dois dígitos verificadores corretos.</summary>
    public static bool Cpf(string valor)
    {
        var d = Digitos(valor);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;

        int Verificador(int quantidade)
        {
            var soma = 0;
            for (var i = 0; i < quantidade; i++) soma += (d[i] - '0') * (quantidade + 1 - i);
            var resto = soma * 10 % 11;
            return resto == 10 ? 0 : resto;
        }

        return Verificador(9) == d[9] - '0' && Verificador(10) == d[10] - '0';
    }

    /// <summary>Cartão Nacional de Saúde: 15 dígitos e soma ponderada (15 a 1) múltipla de 11.</summary>
    public static bool Cns(string valor)
    {
        var d = Digitos(valor);
        if (d.Length != 15 || "12789".IndexOf(d[0]) < 0) return false;

        var soma = 0;
        for (var i = 0; i < 15; i++) soma += (d[i] - '0') * (15 - i);
        return soma % 11 == 0;
    }

    /// <summary>Cartão de pagamento: 13 a 19 dígitos que passam no algoritmo de Luhn.</summary>
    public static bool Luhn(string valor)
    {
        var d = Digitos(valor);
        if (d.Length is < 13 or > 19 || d.Distinct().Count() == 1) return false;

        var soma = 0;
        var dobrar = false;
        for (var i = d.Length - 1; i >= 0; i--)
        {
            var n = d[i] - '0';
            if (dobrar)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            soma += n;
            dobrar = !dobrar;
        }
        return soma % 10 == 0;
    }

    /// <summary>Telefone brasileiro: DDD válido; celular com 9 na frente ou fixo começando de 2 a 5.</summary>
    public static bool Telefone(string valor)
    {
        var d = Digitos(valor);
        if (d.Length is 12 or 13 && d.StartsWith("55")) d = d[2..];
        if (d.Length is not (10 or 11)) return false;
        if (!Ddds.Contains(int.Parse(d[..2]))) return false;
        return d.Length == 11 ? d[2] == '9' : d[2] is >= '2' and <= '5';
    }

    private static string Digitos(string valor) => new(valor.Where(char.IsAsciiDigit).ToArray());
}
