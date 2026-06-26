using BenditaCoxinha.Data;
using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BenditaCoxinha.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    public CategoryService(AppDbContext db) { _db = db; }

    public async Task<IEnumerable<ProductCategory>> GetAllAsync() =>
        await _db.ProductCategories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

    public async Task<ProductCategory> CreateAsync(ProductCategory category)
    {
        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task<ProductCategory> UpdateAsync(ProductCategory category)
    {
        var existing = await _db.ProductCategories.FindAsync(category.Id)
            ?? throw new InvalidOperationException("Categoria nÃ£o encontrada.");

        existing.Name         = category.Name;
        existing.Emoji        = category.Emoji;
        existing.DisplayOrder = category.DisplayOrder;
        existing.IsActive     = category.IsActive;
        // CreatedAt nÃ£o Ã© atualizado â€” preserva a data de criaÃ§Ã£o original

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(Guid id)
    {
        var category = await _db.ProductCategories.FindAsync(id);
        if (category != null)
        {
            _db.ProductCategories.Remove(category);
            await _db.SaveChangesAsync();
        }
    }
}

