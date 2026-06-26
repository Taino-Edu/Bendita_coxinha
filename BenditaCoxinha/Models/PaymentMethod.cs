namespace BenditaCoxinha.Models;

public static class PaymentMethod
{
    public const string Pix       = "Pix";
    public const string Dinheiro  = "Dinheiro";
    public const string Credito   = "Credito";
    public const string Debito    = "Debito";
    public const string Crediario = "Crediario";
    public const string Pontos    = "Pontos";
    public const string Cashback  = "Cashback";

    private static readonly HashSet<string> _valid =
    [
        Pix, Dinheiro, Credito, Debito, Crediario, Pontos, Cashback
    ];

    public static bool IsValid(string? method) =>
        !string.IsNullOrWhiteSpace(method) && _valid.Contains(method);
}
