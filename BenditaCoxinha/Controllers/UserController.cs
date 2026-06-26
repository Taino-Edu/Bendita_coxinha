// =============================================================================
// UserController.cs â€” Endpoints de UsuÃ¡rios e Pontos
// GET    /api/user                  â†’ lista clientes (Admin)
// POST   /api/user                  â†’ Admin cria conta de cliente
// GET    /api/user/me               â†’ perfil do usuÃ¡rio logado
// PUT    /api/user/me               â†’ titular corrige seus dados (LGPD retificaÃ§Ã£o)
// DELETE /api/user/me               â†’ titular solicita exclusÃ£o/anonimizaÃ§Ã£o (LGPD Art. 18)
// GET    /api/user/{id}             â†’ detalhe de um cliente (Admin)
// POST   /api/user/{id}/points      â†’ adiciona pontos (Admin)
// POST   /api/user/{id}/balance     â†’ ajusta saldo (Admin)
// PUT    /api/user/{id}/reset-password â†’ Admin redefine senha do cliente
// PUT    /api/user/{id}/perfil         â†’ Admin atribui/remove perfil de operador
// DELETE /api/user/{id}               â†’ Admin exclui operador
// =============================================================================

using System.Text.Json;
using BenditaCoxinha.Data;
using BenditaCoxinha.DTOs;
using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Implementations;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService        _service;
    private readonly AppDbContext        _db;
    private readonly IVendaAvulsaService _vendaService;

    public UserController(IUserService service, AppDbContext db, IVendaAvulsaService vendaService)
    {
        _service      = service;
        _db           = db;
        _vendaService = vendaService;
    }

    /// <summary>Lista todos os clientes ativos. Admin pode buscar por nome/CPF/WhatsApp.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<UserSummaryDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? role)
    {
        var users = await _service.GetAllAsync(search, role);
        return Ok(users);
    }

    /// <summary>Admin cria diretamente uma conta de cliente.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AdminCreate([FromBody] AdminCreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AdminCreateUserAsync(request, adminId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Perfil completo do usuÃ¡rio logado (pontos, dados pessoais).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMe()
    {
        var userId  = GetUserId();
        var profile = await _service.GetProfileAsync(userId);
        return profile == null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Permite ao titular corrigir seus prÃ³prios dados pessoais.
    /// LGPD â€” Direito de retificaÃ§Ã£o (Art. 18, IV).
    /// </summary>
    [HttpPut("me")]
    [Authorize(Policy = "CustomerOrAdmin")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetUserId();
            var result = await _service.UpdateMeAsync(userId, request);

            // Audit log â€” LGPD: retificaÃ§Ã£o de dados pelo titular
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Anonimiza os dados do titular (exclusÃ£o lÃ³gica).
    /// O registro Ã© mantido para preservar o histÃ³rico de comandas e crediÃ¡rios,
    /// mas todos os dados pessoais identificÃ¡veis sÃ£o removidos.
    /// LGPD â€” Direito de exclusÃ£o (Art. 18, VI).
    /// </summary>
    [HttpDelete("me")]
    [Authorize(Policy = "CustomerOrAdmin")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteMe()
    {
        try
        {
            var userId = GetUserId();

            // Audit log ANTES da anonimizaÃ§Ã£o â€” depois o userId ainda existe no banco
            await _service.AnonimizarAsync(userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>Detalhes de um cliente especÃ­fico (Admin).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _service.GetByIdAsync(id);
        if (user == null)
            return NotFound(new { Message = "UsuÃ¡rio nÃ£o encontrado." });

        // Audit log â€” Admin visualizando dados pessoais de cliente (LGPD rastreabilidade)
        return Ok(user);
    }

    /// <summary>Adiciona pontos ao saldo de um cliente (Admin).</summary>
    [HttpPost("{id:guid}/points")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddPoints(Guid id, [FromBody] AddPointsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AddPointsAsync(id, request, adminId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Ajusta o saldo monetÃ¡rio de um cliente (Admin).
    /// Positivo = crÃ©dito (recarga), negativo = dÃ©bito (uso).
    /// </summary>
    [HttpPost("{id:guid}/balance")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdjustBalance(Guid id, [FromBody] AdjustBalanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            var result  = await _service.AdjustBalanceAsync(id, request, adminId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Admin redefine a senha de um cliente (sem e-mail, imediato).</summary>
    [HttpPut("{id:guid}/reset-password")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = GetUserId();
            await _service.AdminResetPasswordAsync(id, request, adminId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// HistÃ³rico completo de um cliente: comandas, vendas avulsas, crediÃ¡rios e campeonatos.
    /// GET /api/user/{id}/historico
    /// </summary>
    [HttpGet("{id:guid}/historico")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ClienteHistoricoDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetHistorico(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { Message = "UsuÃ¡rio nÃ£o encontrado." });

        // Stats agregados diretamente no banco â€” nÃ£o carrega todas as comandas em memÃ³ria
        var statsQuery = _db.Comandas
            .Where(c => c.UserId == id && c.Status == ComandaStatus.Fechada);

        var totalVisitas   = await statsQuery.CountAsync();
        var totalGastoCmds = await statsQuery.SumAsync(c => (long)c.TotalInCents) / 100m;
        var primeiraVisita = await statsQuery.MinAsync(c => (DateTime?)c.ClosedAt);
        var ultimaVisita   = await statsQuery.MaxAsync(c => (DateTime?)c.ClosedAt);

        var totalComandas = await _db.Comandas.CountAsync(c => c.UserId == id);

        // Comandas paginadas
        var comandas = await _db.Comandas
            .Include(c => c.Items)
            .Where(c => c.UserId == id)
            .OrderByDescending(c => c.OpenedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // CrediÃ¡rios
        var crediarios = await _db.Crediarios
            .Where(c => c.UserId == id)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        // Campeonatos
        var campeonatos = await _db.ChampionshipParticipants
            .Include(p => p.Championship)
            .Where(p => p.UserId == id)
            .OrderByDescending(p => p.Championship.StartDate)
            .ToListAsync();

        // Vendas avulsas (apenas as que tÃªm UserId â€” vendas com cliente identificado a partir de agora)
        var vendasAvulsas = await _vendaService.GetByUserAsync(id);

        var totalGasto = totalGastoCmds + vendasAvulsas.Sum(v => v.TotalInReais);

        var historico = new ClienteHistoricoDto
        {
            UserId         = user.Id,
            UserName       = user.Name,
            TotalVisitas   = totalVisitas,
            TotalGasto     = totalGasto,
            PrimeiraVisita = primeiraVisita,
            UltimaVisita   = ultimaVisita,
            TotalComandas  = totalComandas,
            Page           = page,
            PageSize       = pageSize,

            Comandas = comandas.Select(c => new ComandaHistoricoDto
            {
                Id              = c.Id,
                Status          = c.Status.ToString(),
                TotalInReais    = c.TotalInCents / 100m,
                PaymentMethod   = c.PaymentMethod,
                SecondPaymentMethod = c.SecondPaymentMethod,
                OpenedAt        = c.OpenedAt,
                ClosedAt        = c.ClosedAt,
                TableIdentifier = c.TableIdentifier,
                Items           = c.Items.Select(i => new ComandaItemHistoricoDto
                {
                    ItemName         = i.ItemNameSnapshot,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInCents / 100m,
                    SubtotalInReais  = i.SubtotalInCents / 100m,
                }).ToList(),
            }).ToList(),

            VendasAvulsas = vendasAvulsas.Select(v => new VendaAvulsaHistoricoDto
            {
                Id           = v.Id,
                TotalInReais = v.TotalInReais,
                PaymentMethod = v.PaymentMethod,
                SoldAt       = v.SoldAt,
                Items        = v.Items.Select(i => new VendaAvulsaItemHistoricoDto
                {
                    ProductName      = i.ProductName,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInReais,
                    SubtotalInReais  = i.SubtotalInReais,
                }).ToList(),
            }).ToList(),

            Crediarios = crediarios.Select(c => new CrediariosHistoricoDto
            {
                Id             = c.Id,
                ValorEmReais   = c.ValorEmReais,
                SaldoRestante  = c.SaldoRestanteEmReais,
                Status         = c.Status.ToString(),
                Vencido        = c.Vencido,
                DataAbertura   = c.DataAbertura,
                DataVencimento = c.DataVencimento,
                DataPagamento  = c.DataPagamento,
                Observacao     = c.Observacao,
            }).ToList(),

            Campeonatos = campeonatos.Select(p => new CampeonatoHistoricoDto
            {
                ChampionshipId   = p.ChampionshipId,
                ChampionshipName = p.Championship.Name,
                Game             = p.Championship.Game,
                Status           = p.Championship.Status.ToString(),
                StartDate        = p.Championship.StartDate,
                PlayerNumber     = p.PlayerNumber,
                DeckName         = p.DeckName,
                Placement        = p.Placement,
                RegisteredAt     = p.RegisteredAt,
            }).ToList(),
        };

        return Ok(historico);
    }

    // =========================================================================
    // PreferÃªncias pessoais
    // =========================================================================

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Retorna as preferÃªncias do usuÃ¡rio logado.</summary>
    [HttpGet("me/preferences")]
    [ProducesResponseType(typeof(UserPreferencesDto), 200)]
    public async Task<IActionResult> GetPreferences()
    {
        var user = await _db.Users.FindAsync(GetUserId());
        if (user == null) return NotFound();

        var prefs = string.IsNullOrWhiteSpace(user.PreferencesJson)
            ? new UserPreferencesDto()
            : JsonSerializer.Deserialize<UserPreferencesDto>(user.PreferencesJson, _jsonOpts)
              ?? new UserPreferencesDto();

        return Ok(prefs);
    }

    /// <summary>Salva as preferÃªncias do usuÃ¡rio logado.</summary>
    [HttpPut("me/preferences")]
    [ProducesResponseType(typeof(UserPreferencesDto), 200)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        var user = await _db.Users.FindAsync(GetUserId());
        if (user == null) return NotFound();

        user.PreferencesJson = JsonSerializer.Serialize(request, _jsonOpts);
        user.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(request);
    }

    // =========================================================================
    // ADMIN â€” Atribuir / remover perfil de operador
    // =========================================================================

    /// <summary>Muda o perfil de acesso de um operador. Envie perfilId=null para desatribuir.</summary>
    [HttpPut("{id:guid}/perfil")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AtualizarPerfil(Guid id, [FromBody] AtualizarPerfilOperadorRequest request)
    {
        var user = await _db.Users.Include(u => u.Perfil).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return NotFound(new { Message = "UsuÃ¡rio nÃ£o encontrado." });

        if (user.Role != "Operator")
            return BadRequest(new { Message = "Apenas operadores podem ter perfil atribuÃ­do." });

        if (request.PerfilId.HasValue)
        {
            var perfil = await _db.Perfis.FindAsync(request.PerfilId.Value);
            if (perfil == null)
                return NotFound(new { Message = "Perfil nÃ£o encontrado." });
            user.PerfilId  = perfil.Id;
            user.Perfil    = perfil;
        }
        else
        {
            user.PerfilId = null;
            user.Perfil   = null;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(UserService.MapToSummary(user));
    }

    // =========================================================================
    // ADMIN â€” Excluir operador
    // =========================================================================

    /// <summary>
    /// Remove ou anonimiza um usuÃ¡rio.
    /// â€” Customer: anonimizaÃ§Ã£o LGPD (preserva histÃ³rico financeiro).
    /// â€” Operator: exclusÃ£o permanente.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var adminId = GetUserId();
        if (adminId == id)
            return BadRequest(new { Message = "VocÃª nÃ£o pode excluir a prÃ³pria conta por aqui." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { Message = "UsuÃ¡rio nÃ£o encontrado." });

        if (user.Role == "Admin")
            return BadRequest(new { Message = "Contas de administrador nÃ£o podem ser excluÃ­das." });

        if (user.Role == "Customer")
        {
            // LGPD Art. 18 VI â€” dados pessoais removidos, histÃ³rico financeiro preservado
            await _service.AnonimizarAsync(id);
            return NoContent();
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token invÃ¡lido: identificador de usuÃ¡rio ausente.");
        return id;
    }
}


