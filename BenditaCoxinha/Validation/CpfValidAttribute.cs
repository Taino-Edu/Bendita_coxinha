// =============================================================================
// CpfValidAttribute.cs â€” ValidaÃ§Ã£o de CPF com algoritmo de dÃ­gito verificador
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace BenditaCoxinha.Validation;

/// <summary>
/// Valida o CPF usando o algoritmo oficial de dÃ­gitos verificadores (MÃ³dulo 11).
/// Rejeita: formato errado, sequÃªncias repetidas (000...000, 111...111 etc.)
/// e qualquer nÃºmero que nÃ£o passe nos dois dÃ­gitos verificadores.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class CpfValidAttribute : ValidationAttribute
{
    public CpfValidAttribute() : base("CPF invÃ¡lido.") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string cpf || string.IsNullOrWhiteSpace(cpf))
            return ValidationResult.Success; // deixa [Required] cuidar do vazio

        var limpo = cpf.Trim().Replace(".", "").Replace("-", "");

        if (!ValidarCpf(limpo))
            return new ValidationResult(ErrorMessage ?? "CPF invÃ¡lido.");

        return ValidationResult.Success;
    }

    /// <summary>
    /// ImplementaÃ§Ã£o do algoritmo oficial de CPF (Receita Federal).
    /// </summary>
    public static bool ValidarCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
            return false;

        if (!cpf.All(char.IsAsciiDigit))
            return false;

        // Rejeita sequÃªncias com todos os dÃ­gitos iguais (ex: 000.000.000-00)
        if (cpf.Distinct().Count() == 1)
            return false;

        // â”€â”€ Primeiro dÃ­gito verificador â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int soma = 0;
        for (int i = 0; i < 9; i++)
            soma += (cpf[i] - '0') * (10 - i);

        int d1 = 11 - (soma % 11);
        if (d1 >= 10) d1 = 0;

        if (cpf[9] - '0' != d1)
            return false;

        // â”€â”€ Segundo dÃ­gito verificador â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += (cpf[i] - '0') * (11 - i);

        int d2 = 11 - (soma % 11);
        if (d2 >= 10) d2 = 0;

        return cpf[10] - '0' == d2;
    }
}

