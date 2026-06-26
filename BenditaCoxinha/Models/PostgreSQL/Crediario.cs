// =============================================================================
// Crediario.cs â€” Entidade de CrediÃ¡rio (PostgreSQL)
// Criado automaticamente quando o Admin fecha uma comanda com pagamento
// no crediÃ¡rio. Um cliente sÃ³ pode ter UM crediÃ¡rio aberto por vez.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

[Table("crediarios")]
public class Crediario
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // â”€â”€ UsuÃ¡rio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    // â”€â”€ Comanda de origem â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Null para crediÃ¡rios criados manualmente (dÃ­vidas anteriores ao sistema).</summary>
    [Column("comanda_id")]
    public Guid? ComandaId { get; set; }

    [ForeignKey(nameof(ComandaId))]
    public Comanda? Comanda { get; set; }

    // â”€â”€ Valor e datas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Valor total a ser pago, em centavos (copiado do total da comanda).</summary>
    [Column("valor_em_centavos")]
    public int ValorEmCentavos { get; set; }

    /// <summary>Soma de todos os pagamentos parciais registrados, em centavos.</summary>
    [Column("valor_pago_em_centavos")]
    public int ValorPagoEmCentavos { get; set; } = 0;

    [Column("data_abertura")]
    public DateTime DataAbertura { get; set; } = DateTime.UtcNow;

    /// <summary>Vencimento automÃ¡tico: DataAbertura + 30 dias.</summary>
    [Column("data_vencimento")]
    public DateTime DataVencimento { get; set; }

    /// <summary>Preenchido quando o Admin marca como pago.</summary>
    [Column("data_pagamento")]
    public DateTime? DataPagamento { get; set; }

    // â”€â”€ Status â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Required]
    [Column("status")]
    public CrediariosStatus Status { get; set; } = CrediariosStatus.Aberto;

    // â”€â”€ ObservaÃ§Ãµes e responsÃ¡veis â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [MaxLength(500)]
    [Column("observacao")]
    public string? Observacao { get; set; }

    /// <summary>Admin que criou o crediÃ¡rio (fechou a comanda).</summary>
    [Column("aberto_por_admin_id")]
    public Guid AbertoPorAdminId { get; set; }

    /// <summary>Admin que registrou o pagamento.</summary>
    [Column("pago_por_admin_id")]
    public Guid? PagoPorAdminId { get; set; }

    // â”€â”€ Itens de venda avulsa (JSON snapshot) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// JSON serializado dos itens quando o crediÃ¡rio Ã© originado de uma venda avulsa
    /// (ComandaId == null). Cada acumulaÃ§Ã£o acrescenta novos lanÃ§amentos ao array.
    /// Null quando nÃ£o hÃ¡ itens registrados (crediÃ¡rio manual ou comanda vinculada).
    /// </summary>
    [Column("itens_json", TypeName = "text")]
    public string? ItensJson { get; set; }

    // â”€â”€ Pagamentos parciais â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public ICollection<PagamentoCrediario> Pagamentos { get; set; } = new List<PagamentoCrediario>();

    // â”€â”€ Calculado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [NotMapped]
    public decimal ValorEmReais => ValorEmCentavos / 100m;

    [NotMapped]
    public decimal ValorPagoEmReais => ValorPagoEmCentavos / 100m;

    [NotMapped]
    public int SaldoRestanteEmCentavos => Math.Max(0, ValorEmCentavos - ValorPagoEmCentavos);

    [NotMapped]
    public decimal SaldoRestanteEmReais => SaldoRestanteEmCentavos / 100m;

    /// <summary>True se estÃ¡ Aberto e jÃ¡ passou do vencimento.</summary>
    [NotMapped]
    public bool Vencido => Status == CrediariosStatus.Aberto && DataVencimento < DateTime.UtcNow;
}

public enum CrediariosStatus
{
    Aberto,
    Pago
}

