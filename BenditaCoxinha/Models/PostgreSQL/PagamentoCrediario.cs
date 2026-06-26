// =============================================================================
// PagamentoCrediario.cs â€” Registro de pagamento parcial ou total de crediÃ¡rio
// Cada linha representa um pagamento feito pelo cliente, registrado pelo Admin.
// O crediÃ¡rio Ã© quitado automaticamente quando ValorPago >= ValorTotal.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

[Table("pagamentos_crediario")]
public class PagamentoCrediario
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // â”€â”€ CrediÃ¡rio de origem â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Required]
    [Column("crediario_id")]
    public Guid CrediarioId { get; set; }

    [ForeignKey(nameof(CrediarioId))]
    public Crediario Crediario { get; set; } = null!;

    // â”€â”€ Valor e forma de pagamento â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Valor pago nesta parcela, em centavos.</summary>
    [Column("valor_em_centavos")]
    public int ValorEmCentavos { get; set; }

    /// <summary>Forma de pagamento usada nesta parcela.</summary>
    [MaxLength(50)]
    [Column("forma_pagamento")]
    public string FormaPagamento { get; set; } = "Dinheiro";

    [MaxLength(500)]
    [Column("observacao")]
    public string? Observacao { get; set; }

    // â”€â”€ Auditoria â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Admin que registrou este pagamento.</summary>
    [Column("admin_id")]
    public Guid AdminId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // â”€â”€ Calculado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [NotMapped]
    public decimal ValorEmReais => ValorEmCentavos / 100m;
}

