using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

/// <summary>
/// Representa uma pessoa sentada à mesa (assento).
/// Criado quando o cliente escaneia o QR Code ou o garçom adiciona manualmente.
/// Vincula os itens da comanda a cada pessoa para divisão de conta.
/// </summary>
[Table("comanda_seats")]
public class ComandaSeat
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("comanda_id")]
    public Guid ComandaId { get; set; }

    [ForeignKey(nameof(ComandaId))]
    public Comanda Comanda { get; set; } = null!;

    [Required, MaxLength(100)]
    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("sobrenome")]
    public string? Sobrenome { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Itens pedidos por esta pessoa
    public ICollection<ComandaItem> Items { get; set; } = new List<ComandaItem>();

    [NotMapped]
    public string NomeCompleto => Sobrenome is null ? Nome : $"{Nome} {Sobrenome}";

    /// <summary>Total dos itens desta pessoa, em centavos.</summary>
    [NotMapped]
    public int TotalInCents => Items.Sum(i => i.SubtotalInCents);

    [NotMapped]
    public decimal TotalInReais => TotalInCents / 100m;
}
