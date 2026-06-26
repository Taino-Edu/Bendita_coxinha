// =============================================================================
// ProductController.cs â€” CRUD de Produtos (Estoque Fixo)
// GET    /api/product            â†’ lista todos ativos
// GET    /api/product/{id}       â†’ busca por ID
// POST   /api/product            â†’ cria (Admin)
// PUT    /api/product/{id}       â†’ atualiza (Admin)
// DELETE /api/product/{id}       â†’ desativa (Admin)
// PATCH  /api/product/{id}/stock â†’ ajusta estoque (Admin)
// =============================================================================

using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductController : ControllerBase
{
    private readonly IProductService _service;

    public ProductController(IProductService service)
    {
        _service = service;
    }

    /// <summary>Lista todos os produtos ativos. AcessÃ­vel por todos.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? category)
    {
        var products = category != null
            ? await _service.GetByCategoryAsync(category)
            : await _service.GetAllActiveAsync();
        return Ok(products);
    }

    /// <summary>Lista todos os produtos ativos para comanda do cliente (sem filtro de marketplace).</summary>
    [HttpGet("store")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    public async Task<IActionResult> GetAllStore()
    {
        var products = await _service.GetAllForAdminAsync();
        return Ok(products);
    }

    /// <summary>Lista TODOS os produtos ativos (incluindo ocultos do site). SÃ³ Admin/Operator.</summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin,Operator")]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    public async Task<IActionResult> GetAllAdmin()
    {
        var products = await _service.GetAllForAdminAsync();
        return Ok(products);
    }

    /// <summary>Busca produto por ID.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _service.GetByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    /// <summary>Busca produto por cÃ³digo de barras. AcessÃ­vel por todos autenticados.</summary>
    [HttpGet("barcode/{code}")]
    [Authorize]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByBarcode(string code)
    {
        var product = await _service.GetByBarcodeAsync(code);
        return product == null ? NotFound(new { Message = "Produto nÃ£o encontrado para este cÃ³digo de barras." }) : Ok(product);
    }

    /// <summary>Produtos com estoque abaixo do mÃ­nimo. Apenas Admin.</summary>
    [HttpGet("low-stock")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetLowStock()
    {
        return Ok(await _service.GetLowStockAsync());
    }

    /// <summary>Cria um novo produto. Apenas Admin.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Product), 201)]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        var created = await _service.CreateAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Atualiza um produto. Apenas Admin.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Product), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Product product)
    {
        product.Id = id;
        return Ok(await _service.UpdateAsync(product));
    }

    /// <summary>Desativa um produto (soft delete). Apenas Admin.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _service.DeactivateAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Ajusta o estoque. Positivo = entrada, negativo = saÃ­da.
    /// Exemplo: { "delta": -1 } para vender 1 unidade.
    /// </summary>
    [HttpPatch("{id:guid}/stock")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdjustStock(Guid id, [FromBody] StockAdjustRequest req)
    {
        var ok = await _service.AdjustStockAsync(id, req.Delta);
        return ok ? Ok(new { Message = "Estoque ajustado." }) : BadRequest(new { Message = "Estoque insuficiente." });
    }
}

public record StockAdjustRequest(int Delta);

