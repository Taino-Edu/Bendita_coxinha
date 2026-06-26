// =============================================================================
// IProductService.cs â€” Interface do CRUD de Produtos (estoque fÃ­sico)
// =============================================================================

using BenditaCoxinha.Models.PostgreSQL;

namespace BenditaCoxinha.Services.Interfaces;

/// <summary>Contrato para gestÃ£o do estoque fÃ­sico da loja.</summary>
public interface IProductService
{
    Task<IEnumerable<Product>> GetAllActiveAsync();
    Task<IEnumerable<Product>> GetAllForAdminAsync();
    Task<IEnumerable<Product>> GetByCategoryAsync(string category);
    Task<Product?>             GetByIdAsync(Guid id);
    Task<Product?>             GetByBarcodeAsync(string barcode);
    Task<Product>              CreateAsync(Product product);
    Task<Product>              UpdateAsync(Product product);
    Task                       DeactivateAsync(Guid id);
    Task<IEnumerable<Product>> GetLowStockAsync();
    Task<bool>                 AdjustStockAsync(Guid id, int quantityDelta); // Positivo = entrada, negativo = saÃ­da
}

