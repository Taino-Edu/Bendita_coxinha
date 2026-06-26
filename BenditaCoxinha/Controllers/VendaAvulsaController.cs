// =============================================================================
// VendaAvulsaController.cs â€” Endpoints de Venda Avulsa (caixa do balcÃ£o)
//
// POST /api/venda-avulsa          â†’ Registra venda no balcÃ£o (Admin)
//                                    Valida estoque, decrementa PostgreSQL,
//                                    persiste evento imutÃ¡vel no MongoDB.
// GET  /api/venda-avulsa/recent   â†’ Ãšltimas N vendas (dashboard/histÃ³rico)
//
// Separado do ComandaController intencionalmente:
//   VendaAvulsa = evento de caixa, sem usuÃ¡rio cadastrado, sem comanda.
//   Comanda     = pedido de mesa via QR Code, com ciclo de vida.
// =============================================================================

using BenditaCoxinha.DTOs;
using BenditaCoxinha.Models;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/venda-avulsa")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class VendaAvulsaController : ControllerBase
{
    private readonly IVendaAvulsaService _service;

    public VendaAvulsaController(IVendaAvulsaService service) => _service = service;

    /// <summary>
    /// Registra uma venda avulsa no balcÃ£o.
    /// Decrementa estoque (PostgreSQL) e persiste o evento no MongoDB.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VendaAvulsaDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] VendaAvulsaRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!request.IsPaymentMethodValid())
            return BadRequest(new { Message = $"Forma de pagamento invÃ¡lida. Use: {string.Join(", ", new[]{"Pix","Dinheiro","Credito","Debito","Crediario","Pontos","Cashback"})}" });

        try
        {
            var adminId   = GetUserId();
            var adminName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                         ?? User.FindFirst("name")?.Value
                         ?? "Admin";

            var result = await _service.RegisterAsync(request, adminId, adminName);
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Retorna as vendas avulsas mais recentes para exibiÃ§Ã£o no dashboard.</summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IEnumerable<VendaAvulsaDto>), 200)]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 50)
    {
        if (limit is < 1 or > 200)
            limit = 50;

        var result = await _service.GetRecentAsync(limit);
        return Ok(result);
    }

    /// <summary>Retorna todas as vendas avulsas de uma data especÃ­fica (YYYY-MM-DD, fuso de BrasÃ­lia). PadrÃ£o: hoje.</summary>
    [HttpGet("by-date")]
    [ProducesResponseType(typeof(IEnumerable<VendaAvulsaDto>), 200)]
    public async Task<IActionResult> GetByDate([FromQuery] string? date = null)
    {
        // Quando nÃ£o hÃ¡ ?date=, passa null â†’ serviÃ§o calcula "hoje" no fuso BR.
        // Quando hÃ¡ data explÃ­cita, repassa como DateTime para o serviÃ§o converter corretamente.
        DateTime? day = null;
        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
            day = parsed.Date;

        var result = await _service.GetByDateAsync(day);
        return Ok(result);
    }

    /// <summary>
    /// Preenche o custo (UnitCostInCents) em itens de vendas avulsas antigas que ficaram com custo = 0.
    /// Usa o custo atual de cada produto no PostgreSQL como referÃªncia.
    /// </summary>
    /// <summary>Corrige a forma de pagamento de uma venda avulsa jÃ¡ registrada (Admin only).</summary>
    [HttpPatch("{id}/pagamento")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(VendaAvulsaDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EditarPagamento(string id, [FromBody] EditarPagamentoVendaAvulsaRequest request)
    {
        try
        {
            var result = await _service.EditarPagamentoAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)   { return NotFound(new { Message = ex.Message }); }
        catch (ArgumentException ex)      { return BadRequest(new { Message = ex.Message }); }
    }

    [HttpPost("backfill-costs")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> BackfillCosts()
    {
        var total = await _service.BackfillCostsAsync();
        return Ok(new { itensAtualizados = total, mensagem = $"{total} item(s) de venda avulsa atualizados com custo." });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token invÃ¡lido: identificador de usuÃ¡rio ausente.");
        return id;
    }
}



