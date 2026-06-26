// =============================================================================
// CreditarioDtos.cs â€” DTOs do mÃ³dulo de CrediÃ¡rio
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace BenditaCoxinha.DTOs;

/// <summary>Resposta de um crediÃ¡rio (admin e cliente).</summary>
public class CrediariosDto
{
    public Guid      Id                    { get; set; }
    public Guid      UserId                { get; set; }
    public string    UserName              { get; set; } = string.Empty;
    public string?   UserEmail             { get; set; }
    public Guid?     ComandaId             { get; set; }
    public decimal   ValorEmReais          { get; set; }
    public decimal   ValorPagoEmReais      { get; set; }
    public decimal   SaldoRestanteEmReais  { get; set; }
    public DateTime  DataAbertura          { get; set; }
    public DateTime  DataVencimento        { get; set; }
    public DateTime? DataPagamento         { get; set; }
    public string    Status                { get; set; } = string.Empty;
    public string?   Observacao            { get; set; }

    /// <summary>True se Status == Aberto e DataVencimento &lt; agora.</summary>
    public bool Vencido { get; set; }

    /// <summary>Dias restantes para vencer (negativo se jÃ¡ venceu).</summary>
    public int DiasRestantes { get; set; }

    /// <summary>HistÃ³rico de pagamentos parciais registrados.</summary>
    public List<PagamentoCrediarioDto> Pagamentos { get; set; } = new();

    /// <summary>Itens da comanda de origem (null = dÃ­vida manual sem comanda).</summary>
    public List<ItemCrediarioDto> ItensComanda { get; set; } = new();
}

/// <summary>Item da comanda vinculada ao crediÃ¡rio (somente leitura).</summary>
public class ItemCrediarioDto
{
    public string  ItemName        { get; set; } = string.Empty;
    public int     Quantity        { get; set; }
    public decimal UnitPriceInReais { get; set; }
    public decimal SubtotalInReais  { get; set; }
}

/// <summary>DTO de um pagamento parcial do crediÃ¡rio.</summary>
public class PagamentoCrediarioDto
{
    public Guid     Id             { get; set; }
    public decimal  ValorEmReais   { get; set; }
    public string   FormaPagamento { get; set; } = string.Empty;
    public string?  Observacao     { get; set; }
    public DateTime CreatedAt      { get; set; }
}

/// <summary>DÃ­vidas abertas de um cliente especÃ­fico â€” usado em GET /api/crediarios/por-cliente.</summary>
public class CrediariosClienteDto
{
    public Guid     UserId            { get; set; }
    public string   UserName          { get; set; } = string.Empty;
    public string?  UserEmail         { get; set; }
    public string?  UserWhatsApp      { get; set; }
    public decimal  SaldoTotal        { get; set; }
    public int      TotalDividas      { get; set; }
    public bool     TemVencido        { get; set; }
    public DateTime ProximoVencimento { get; set; }
    public List<CrediariosDto> Dividas { get; set; } = new();
}

/// <summary>Body do endpoint PUT /api/crediarios/{id}/pagar (quitaÃ§Ã£o total).</summary>
public class MarcarPagoRequest
{
    /// <summary>ObservaÃ§Ã£o opcional (ex: "Pago em dinheiro no balcÃ£o").</summary>
    public string? Observacao { get; set; }
}

/// <summary>Body do endpoint POST /api/crediarios (criaÃ§Ã£o manual â€” dÃ­vidas anteriores ao sistema).</summary>
public class CriarCrediarioManualRequest
{
    /// <summary>ID do cliente que tem a dÃ­vida.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Valor da dÃ­vida em centavos.</summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
    public int ValorEmCentavos { get; set; }

    /// <summary>ObservaÃ§Ã£o (ex: "DÃ­vida de torneio 12/04/2025").</summary>
    [MaxLength(500)]
    public string? Observacao { get; set; }

    /// <summary>Data de abertura da dÃ­vida. Se null, usa a data atual.</summary>
    public DateTime? DataAbertura { get; set; }

    /// <summary>Vencimento customizado. Se null, usa DataAbertura + 30 dias.</summary>
    public DateTime? DataVencimento { get; set; }

    /// <summary>
    /// Lista de itens que compÃµem a dÃ­vida (opcional).
    /// Serializada como JSON no campo ItensJson da entidade.
    /// </summary>
    public List<ItemCrediarioDto>? Itens { get; set; }
}

/// <summary>Body do endpoint PATCH /api/crediarios/{id} (ediÃ§Ã£o de crediÃ¡rio em aberto).</summary>
public class EditarCrediarioRequest
{
    /// <summary>Novo valor total em centavos. Se null, mantÃ©m o atual.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
    public int? ValorEmCentavos { get; set; }

    /// <summary>Nova observaÃ§Ã£o. Se null, mantÃ©m a atual.</summary>
    [MaxLength(500)]
    public string? Observacao { get; set; }

    /// <summary>Nova data de vencimento. Se null, mantÃ©m a atual.</summary>
    public DateTime? DataVencimento { get; set; }

    /// <summary>
    /// Quando true, limpa o ItensJson forÃ§ando o MapToDto a rebuscar os itens
    /// das comandas via date-range (Ãºtil para corrigir dados incompletos de migraÃ§Ãµes antigas).
    /// </summary>
    public bool LimparItens { get; set; } = false;

    /// <summary>
    /// Lista de itens editada manualmente pelo admin. Quando nÃ£o-null, substitui o ItensJson inteiro.
    /// Lista vazia [] = remove todos os itens. Null = nÃ£o altera itens.
    /// </summary>
    public List<ItemCrediarioDto>? Itens { get; set; }
}

/// <summary>Body do endpoint POST /api/crediarios/{id}/pagamento (pagamento parcial).</summary>
public class RegistrarPagamentoRequest
{
    /// <summary>Valor pago nesta parcela, em centavos.</summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "O valor do pagamento deve ser maior que zero.")]
    public int ValorEmCentavos { get; set; }

    /// <summary>Forma de pagamento usada (Dinheiro, Pix, CartaoCredito, CartaoDebito, Pontos, Cashback).</summary>
    [MaxLength(50)]
    public string FormaPagamento { get; set; } = "Dinheiro";

    /// <summary>Segundo mÃ©todo de pagamento (split). Null = nÃ£o tem split.</summary>
    [MaxLength(50)]
    public string? SecondFormaPagamento { get; set; }

    /// <summary>Valor do segundo mÃ©todo em centavos. Zero = sem split.</summary>
    [Range(0, int.MaxValue)]
    public int SecondValorEmCentavos { get; set; } = 0;

    /// <summary>ObservaÃ§Ã£o opcional.</summary>
    [MaxLength(500)]
    public string? Observacao { get; set; }
}

