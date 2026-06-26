// GET    /api/category       â†’ lista todas (pÃºblico)
// POST   /api/category       â†’ cria (Admin)
// PUT    /api/category/{id}  â†’ atualiza (Admin)
// DELETE /api/category/{id}  â†’ remove (Admin)

using System.ComponentModel.DataAnnotations;
using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BenditaCoxinha.Controllers;

public record CategoryRequest(
    [Required][MaxLength(100)] string Name,
    [MaxLength(10)]            string? Emoji,
    int  DisplayOrder,
    bool IsActive
);

[ApiController]
[Route("api/category")]
[Produces("application/json")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _service;
    public CategoryController(ICategoryService service) { _service = service; }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() =>
        Ok(await _service.GetAllAsync());

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CategoryRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var category = new ProductCategory
        {
            Id           = Guid.NewGuid(),
            Name         = req.Name.Trim(),
            Emoji        = req.Emoji?.Trim(),
            DisplayOrder = req.DisplayOrder,
            IsActive     = req.IsActive,
            CreatedAt    = DateTime.UtcNow,
        };
        return Ok(await _service.CreateAsync(category));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var category = new ProductCategory
        {
            Id           = id,
            Name         = req.Name.Trim(),
            Emoji        = req.Emoji?.Trim(),
            DisplayOrder = req.DisplayOrder,
            IsActive     = req.IsActive,
        };
        return Ok(await _service.UpdateAsync(category));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}

