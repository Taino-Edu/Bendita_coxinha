using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

public class VendaAvulsa
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(150)]
    public string? ClientName { get; set; }

    public Guid? UserId { get; set; }

    [Required, MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? SecondPaymentMethod { get; set; }

    public int SecondPaymentAmountInCents { get; set; }

    public int TotalInCents { get; set; }
    public int DiscountPercent { get; set; }
    public int DiscountInCents { get; set; }

    public DateTime SoldAt { get; set; } = DateTime.UtcNow;

    public Guid SoldByAdminId { get; set; }

    [Required, MaxLength(150)]
    public string SoldByAdminName { get; set; } = string.Empty;

    // Itens serializados como JSON (mesma abordagem do Crediario)
    public string ItensJson { get; set; } = "[]";

    [NotMapped]
    public decimal TotalInReais => TotalInCents / 100m;

    [NotMapped]
    public decimal DiscountInReais => DiscountInCents / 100m;
}

// DTO interno para serialização dos itens (não é entidade EF)
public class VendaAvulsaItem
{
    public Guid    ProductId        { get; set; }
    public string  ProductName      { get; set; } = string.Empty;
    public string? ProductCategory  { get; set; }
    public int     Quantity         { get; set; }
    public int     UnitPriceInCents { get; set; }
    public int     SubtotalInCents  { get; set; }
    public int     UnitCostInCents  { get; set; }

    public decimal SubtotalInReais  => SubtotalInCents / 100m;
}
