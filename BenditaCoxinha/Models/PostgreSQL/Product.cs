// =============================================================================
// Product.cs â€” Estoque fixo da loja (PostgreSQL)
// Representa itens fÃ­sicos: refrigerantes, salgadinhos, acessÃ³rios, etc.
// Cartas de TCG NÃƒO entram aqui â€” elas usam o CardCache (MongoDB) + serviÃ§o TCG.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

/// <summary>
/// Produto do estoque fÃ­sico da loja.
/// CRUD simples gerenciado pelo Admin (Maikon).
/// </summary>
[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // IdentificaÃ§Ã£o
    // -------------------------------------------------------------------------

    [Required, MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    // -------------------------------------------------------------------------
    // CategorizaÃ§Ã£o e identificaÃ§Ã£o fÃ­sica
    // -------------------------------------------------------------------------

    /// <summary>
    /// Categoria do produto.
    /// Exemplos: "Bebida", "Salgadinho", "AcessÃ³rio", "Carta Avulsa"
    /// </summary>
    [Required, MaxLength(100)]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>CÃ³digo de barras (EAN-8, EAN-13, QR etc.) â€” opcional, leitura via scanner USB ou cÃ¢mera.</summary>
    [MaxLength(100)]
    [Column("barcode")]
    public string? Barcode { get; set; }

    // -------------------------------------------------------------------------
    // PrecificaÃ§Ã£o e Estoque
    // -------------------------------------------------------------------------

    /// <summary>PreÃ§o de custo/aquisiÃ§Ã£o (em centavos). VisÃ­vel sÃ³ para o admin â€” para controle de margem.</summary>
    [Column("cost_price_in_cents")]
    public int CostPriceInCents { get; set; } = 0;

    /// <summary>PreÃ§o de venda ao cliente (em centavos, para evitar float).</summary>
    [Column("price_in_cents")]
    public int PriceInCents { get; set; }

    /// <summary>Quantidade atual no estoque.</summary>
    [Column("stock_quantity")]
    public int StockQuantity { get; set; }

    /// <summary>Quantidade mÃ­nima antes de alertar o Admin sobre reposiÃ§Ã£o.</summary>
    [Column("minimum_stock")]
    public int MinimumStock { get; set; } = 5;

    // -------------------------------------------------------------------------
    // Metadados
    // -------------------------------------------------------------------------

    /// <summary>URL da imagem do produto (pode ser local ou CDN).</summary>
    [MaxLength(500)]
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>Imagens adicionais do produto (galeria). Armazenadas como array de URLs.</summary>
    [Column("image_urls", TypeName = "text[]")]
    public string[] ImageUrls { get; set; } = Array.Empty<string>();

    /// <summary>DescriÃ§Ã£o longa do produto â€” exibida na pÃ¡gina de detalhe (estilo ML).</summary>
    [Column("full_description")]
    public string? FullDescription { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Se true, o produto aparece em destaque na landing page (escolha manual do admin).</summary>
    [Column("is_featured")]
    public bool IsFeatured { get; set; } = false;

    /// <summary>Se true, o produto aparece no site pÃºblico. ConsumÃ­veis internos devem ter false.</summary>
    [Column("show_on_site")]
    public bool ShowOnSite { get; set; } = true;

    /// <summary>Se true, o produto aparece na loja digital (marketplace). Independente do PDV/comandas.</summary>
    [Column("show_on_marketplace")]
    public bool ShowOnMarketplace { get; set; } = true;

    /// <summary>PreÃ§o promocional em centavos. Quando preenchido, exibe badge "PromoÃ§Ã£o" e preÃ§o riscado.</summary>
    [Column("discount_price_in_cents")]
    public int? DiscountPriceInCents { get; set; }

    /// <summary>Se true, exibe badge "PrÃ©-venda" â€” item disponÃ­vel para pedido mas entregue sÃ³ no lanÃ§amento.</summary>
    [Column("is_pre_venda")]
    public bool IsPreVenda { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // -------------------------------------------------------------------------
    // Propriedade calculada (nÃ£o mapeada no banco)
    // -------------------------------------------------------------------------

    /// <summary>PreÃ§o em reais para exibiÃ§Ã£o na interface.</summary>
    [NotMapped]
    public decimal PriceInReais => PriceInCents / 100m;

    /// <summary>PreÃ§o promocional em reais (null se nÃ£o houver promoÃ§Ã£o).</summary>
    [NotMapped]
    public decimal? DiscountPriceInReais => DiscountPriceInCents.HasValue ? DiscountPriceInCents.Value / 100m : null;

    /// <summary>True se o produto estiver em promoÃ§Ã£o.</summary>
    [NotMapped]
    public bool IsOnPromo => DiscountPriceInCents.HasValue && DiscountPriceInCents.Value > 0 && DiscountPriceInCents.Value < PriceInCents;

    /// <summary>PreÃ§o de custo em reais.</summary>
    [NotMapped]
    public decimal CostPriceInReais => CostPriceInCents / 100m;

    /// <summary>Margem de lucro em reais.</summary>
    [NotMapped]
    public decimal MarginInReais => PriceInReais - CostPriceInReais;

    /// <summary>Margem percentual.</summary>
    [NotMapped]
    public decimal MarginPercent => CostPriceInCents > 0
        ? Math.Round((MarginInReais / CostPriceInReais) * 100, 1)
        : 0;

    /// <summary>Verdadeiro se o estoque estiver abaixo do mÃ­nimo.</summary>
    [NotMapped]
    public bool IsLowStock => StockQuantity <= MinimumStock;

    // -------------------------------------------------------------------------
    // NavegaÃ§Ã£o
    // -------------------------------------------------------------------------

    public ICollection<ComandaItem> ComandaItems { get; set; } = new List<ComandaItem>();
}

