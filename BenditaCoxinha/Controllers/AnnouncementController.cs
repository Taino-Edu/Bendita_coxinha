// =============================================================================
// AnnouncementController.cs â€” AnÃºncios e banners da loja
//
// GET  /api/announcements         â†’ AnÃºncios visÃ­veis (pÃºblico, sem auth)
// GET  /api/announcements/all     â†’ Todos os anÃºncios, ativos e inativos (Admin)
// POST /api/announcements         â†’ Criar anÃºncio (Admin)
// PUT  /api/announcements/{id}    â†’ Atualizar (Admin)
// DELETE /api/announcements/{id}  â†’ Remover (Admin)
// =============================================================================

using BenditaCoxinha.DTOs;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/announcements")]
[Produces("application/json")]
public class AnnouncementController : ControllerBase
{
    private readonly IAnnouncementService _service;

    public AnnouncementController(IAnnouncementService service) => _service = service;

    /// <summary>Retorna os anÃºncios visÃ­veis (ativos e dentro do prazo). PÃºblico.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<AnnouncementDto>), 200)]
    public async Task<IActionResult> GetVisible()
        => Ok(await _service.GetVisibleAsync());

    /// <summary>Retorna todos os anÃºncios (ativos e inativos). Admin only.</summary>
    [HttpGet("all")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<AnnouncementDto>), 200)]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Cria um novo anÃºncio.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AnnouncementDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminId = GetUserId();
        var result  = await _service.CreateAsync(request, adminId);
        return StatusCode(201, result);
    }

    /// <summary>Atualiza tÃ­tulo, corpo, imagem, expiraÃ§Ã£o ou status ativo/inativo.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AnnouncementDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAnnouncementRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>Remove permanentemente um anÃºncio.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token invÃ¡lido: identificador de usuÃ¡rio ausente.");
        return id;
    }
}

