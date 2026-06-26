// =============================================================================
// ComandaItem.cs â€” Linha de item dentro de uma Comanda (PostgreSQL)
// Suporta dois tipos: produto fÃ­sico (FK para Product) ou carta TCG (referÃªncia MongoDB)
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

/// <summary>
/// Representa um item dentro de uma comanda.
/// Pode ser um produto fÃ­sico do estoque OU uma carta TCG (via cache MongoDB).
/// </summary>
[Table("comanda_items")]
public class ComandaItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Relacionamento com a Comanda pai
    // -------------------------------------------------------------------------

    [Required]
    [Column("comanda_id")]
    public Guid ComandaId { get; set; }

    [ForeignKey(nameof(ComandaId))]
    public Comanda Comanda { get; set; } = null!;

    // -------------------------------------------------------------------------
    // Produto do cardápio
    // -------------------------------------------------------------------------

    [Column("product_id")]
    public Guid? ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product? Product { get; set; }

    // -------------------------------------------------------------------------
    // Assento (pessoa que pediu este item)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pessoa da mesa que pediu este item.
    /// Nullable: itens podem ser "da mesa" sem vinculação a pessoa específica.
    /// Usado para divisão de conta por pessoa.
    /// </summary>
    [Column("seat_id")]
    public Guid? SeatId { get; set; }

    [ForeignKey(nameof(SeatId))]
    public ComandaSeat? Seat { get; set; }

    // -------------------------------------------------------------------------
    // Observações do pedido
    // -------------------------------------------------------------------------

    /// <summary>Modificações solicitadas: "sem cebola", "bem passado", "com gelo".</summary>
    [MaxLength(300)]
    [Column("observacoes")]
    public string? Observacoes { get; set; }

    // -------------------------------------------------------------------------
    // Snapshot do item no momento da adiÃ§Ã£o
    // -------------------------------------------------------------------------

    /// <summary>
    /// Nome do produto/carta no momento da adiÃ§Ã£o.
    /// Snapshot para nÃ£o perder o histÃ³rico se o produto for renomeado.
    /// </summary>
    [Required, MaxLength(200)]
    [Column("item_name_snapshot")]
    public string ItemNameSnapshot { get; set; } = string.Empty;

    /// <summary>PreÃ§o unitÃ¡rio em centavos no momento da adiÃ§Ã£o.</summary>
    [Column("unit_price_in_cents")]
    public int UnitPriceInCents { get; set; }

    /// <summary>
    /// Custo unitÃ¡rio do produto no momento da adiÃ§Ã£o (snapshot imutÃ¡vel).
    /// Congelado para que alteraÃ§Ãµes futuras de custo nÃ£o distorÃ§am o histÃ³rico financeiro.
    /// </summary>
    [Column("cost_price_snapshot_in_cents")]
    public int CostPriceSnapshotInCents { get; set; }

    // -------------------------------------------------------------------------
    // Quantidade e total
    // -------------------------------------------------------------------------

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>Total desta linha (UnitPrice Ã— Quantity), em centavos.</summary>
    [Column("subtotal_in_cents")]
    public int SubtotalInCents { get; set; }

    // -------------------------------------------------------------------------
    // Auditoria
    // -------------------------------------------------------------------------

    [Column("added_at")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Quem adicionou o item: o prÃ³prio cliente ou o Admin.</summary>
    [Column("added_by_user_id")]
    public Guid AddedByUserId { get; set; }

    // -------------------------------------------------------------------------
    // Propriedade calculada
    // -------------------------------------------------------------------------

    [NotMapped]
    public decimal SubtotalInReais => SubtotalInCents / 100m;
}

