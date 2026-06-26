// =============================================================================
// Comanda.cs â€” Entidade de Comanda (PostgreSQL)
// Agregado central do sistema: representa o "pedido em aberto" de um cliente.
// Qualquer alteraÃ§Ã£o dispara evento SignalR para o painel do Maikon.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

/// <summary>
/// Comanda de um cliente na loja.
/// Ciclo de vida: Aberta â†’ EmAndamento â†’ Fechada | Cancelada
/// </summary>
[Table("comandas")]
public class Comanda
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Relacionamento com o usuÃ¡rio
    // -------------------------------------------------------------------------

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    // -------------------------------------------------------------------------
    // Contexto de abertura (mesa / QR Code)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Identificador da mesa ou espaÃ§o onde o cliente estÃ¡.
    /// Gerado pelo QR Code fixado na mesa (ex: "Mesa-03").
    /// </summary>
    [MaxLength(50)]
    [Column("table_identifier")]
    public string? TableIdentifier { get; set; }

    // -------------------------------------------------------------------------
    // Status e ciclo de vida
    // -------------------------------------------------------------------------

    /// <summary>Status atual da comanda.</summary>
    [Required]
    [Column("status")]
    public ComandaStatus Status { get; set; } = ComandaStatus.Aberta;

    [Column("opened_at")]
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Preenchido quando o Admin fecha ou cancela a comanda.</summary>
    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    /// <summary>Forma de pagamento usada no fechamento (Dinheiro, Pix, CartaoCredito, CartaoDebito, Crediario).</summary>
    [MaxLength(30)]
    [Column("payment_method")]
    public string? PaymentMethod { get; set; }

    /// <summary>Segundo mÃ©todo de pagamento (quando hÃ¡ split: ex. Cashback + Dinheiro).</summary>
    [MaxLength(30)]
    [Column("second_payment_method")]
    public string? SecondPaymentMethod { get; set; }

    /// <summary>Valor pago pelo segundo mÃ©todo, em centavos. Zero quando nÃ£o hÃ¡ split.</summary>
    [Column("second_payment_amount_in_cents")]
    public int SecondPaymentAmountInCents { get; set; }

    // -------------------------------------------------------------------------
    // Campeonato (opcional) â€” comanda pode estar vinculada a um torneio
    // -------------------------------------------------------------------------

    [Column("championship_id")]
    public Guid? ChampionshipId { get; set; }

    [ForeignKey(nameof(ChampionshipId))]
    public Championship? Championship { get; set; }

    // -------------------------------------------------------------------------
    // Totalizadores (calculados e sincronizados a cada item adicionado)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Total em centavos. Recalculado sempre que um ComandaItem Ã© inserido/removido.
    /// Armazenado aqui para performance nas queries do dashboard.
    /// </summary>
    [Column("total_in_cents")]
    public int TotalInCents { get; set; }

    /// <summary>Pontos usados pelo cliente para abater o total desta comanda.</summary>
    [Column("points_applied")]
    public int PointsApplied { get; set; } = 0;

    /// <summary>ObservaÃ§Ãµes do Admin (ex: "cliente solicitou desconto").</summary>
    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    // -------------------------------------------------------------------------
    // Propriedade calculada
    // -------------------------------------------------------------------------

    [NotMapped]
    public decimal TotalInReais => TotalInCents / 100m;

    // -------------------------------------------------------------------------
    // NavegaÃ§Ã£o
    // -------------------------------------------------------------------------

    public ICollection<ComandaItem> Items { get; set; } = new List<ComandaItem>();
}

// -------------------------------------------------------------------------
// Enum de status da comanda
// -------------------------------------------------------------------------

/// <summary>
/// Estados possÃ­veis de uma comanda.
/// Armazenado como string no banco (HasConversion) para legibilidade nas queries.
/// </summary>
public enum ComandaStatus
{
    /// <summary>RecÃ©m-criada via QR Code, aguardando primeiro item.</summary>
    Aberta,

    /// <summary>JÃ¡ possui itens adicionados.</summary>
    EmAndamento,

    /// <summary>Pagamento confirmado pelo Admin.</summary>
    Fechada,

    /// <summary>Cancelada pelo Admin (sem cobranÃ§a).</summary>
    Cancelada
}

