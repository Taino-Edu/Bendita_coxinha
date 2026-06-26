// =============================================================================
// ComandaHub.cs â€” Hub SignalR para comunicaÃ§Ã£o em tempo real de Comandas
//
// GRUPOS:
//   AdminDashboard       â†’ Admin (Maikon) â€” recebe TUDO
//   User_{userId}        â†’ Cliente sempre entra aqui (mesmo sem comanda ativa)
//   Comanda_{comandaId}  â†’ Grupo especÃ­fico de uma comanda aberta
//
// EVENTOS emitidos pelo servidor:
//   ComandaOpened        â†’ User_{userId}        quando admin abre comanda pro cliente
//   ComandaUpdated       â†’ Comanda_{id}+Admin   quando item Ã© adicionado/removido/alterado
//   ItemAddedByAdmin     â†’ Comanda_{id}         quando admin adiciona item manualmente
//   ComandaClosed        â†’ Comanda_{id}+Admin   quando comanda Ã© fechada
//   ComandaCancelled     â†’ Comanda_{id}+Admin   quando comanda Ã© cancelada
// =============================================================================

using BenditaCoxinha.DTOs;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BenditaCoxinha.Hubs;

[Authorize]
public class ComandaHub : Hub
{
    private readonly IComandaService _comandaService;
    private readonly ILogger<ComandaHub> _logger;

    public const string AdminGroup = "AdminDashboard";

    public ComandaHub(IComandaService comandaService, ILogger<ComandaHub> logger)
    {
        _comandaService = comandaService;
        _logger         = logger;
    }

    // =========================================================================
    // CICLO DE VIDA
    // =========================================================================

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var role   = GetUserRole();

        _logger.LogInformation("UsuÃ¡rio {UserId} ({Role}) conectado ao ComandaHub", userId, role);

        if (role == "Admin")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }
        else
        {
            // Cliente SEMPRE entra no grupo pessoal â€” recebe ComandaOpened mesmo sem comanda
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));

            // Se jÃ¡ tem comanda ativa, entra no grupo dela tambÃ©m
            var comandaId = await _comandaService.GetActiveComandaIdByUserAsync(userId);
            if (comandaId.HasValue)
                await Groups.AddToGroupAsync(Context.ConnectionId, GetComandaGroup(comandaId.Value));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("UsuÃ¡rio {UserId} desconectado do ComandaHub", GetUserId());
        await base.OnDisconnectedAsync(exception);
    }

    // =========================================================================
    // CLIENTE â†’ HUB: adicionar item
    // =========================================================================

    public async Task AddItemToComanda(AddItemToComandaRequest request)
    {
        var userId = GetUserId();
        try
        {
            var updated = await _comandaService.AddItemAsync(userId, request);
            // O serviÃ§o emite ComandaUpdated para o AdminGroup automaticamente

            await Clients.Caller.SendAsync("ItemAddedConfirmation", new
            {
                Success         = true,
                ComandaId       = updated.Id,
                NewTotalInReais = updated.TotalInReais,
                Message         = $"'{request.ItemName}' adicionado com sucesso!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar item para usuÃ¡rio {UserId}", userId);
            await Clients.Caller.SendAsync("Error", new { Message = "NÃ£o foi possÃ­vel adicionar o item." });
        }
    }

    // =========================================================================
    // ADMIN â†’ HUB: fechar comanda (via Hub â€” mantido para compatibilidade)
    // =========================================================================

    [Authorize(Roles = "Admin")]
    public async Task CloseComanda(Guid comandaId)
    {
        var adminId = GetUserId();
        try
        {
            await _comandaService.CloseComandaAsync(comandaId, adminId);
            // O serviÃ§o agora emite os eventos SignalR automaticamente
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fechar comanda {ComandaId} via Hub", comandaId);
            await Clients.Caller.SendAsync("Error", new { Message = "Erro ao fechar comanda." });
        }
    }

    // =========================================================================
    // ADMIN â†’ HUB: adicionar item manualmente (via Hub â€” mantido para compatibilidade)
    // =========================================================================

    [Authorize(Roles = "Admin")]
    public async Task AdminAddItemToComanda(Guid comandaId, AddItemToComandaRequest request)
    {
        var adminId = GetUserId();
        await _comandaService.AdminAddItemAsync(comandaId, adminId, request);
        // O serviÃ§o emite os eventos SignalR automaticamente
    }

    // =========================================================================
    // CLIENTE â†’ HUB: entrar no grupo de uma comanda apÃ³s receber ComandaOpened
    // =========================================================================

    public async Task JoinComandaGroup(Guid comandaId)
    {
        var userId = GetUserId();
        // Valida que a comanda pertence ao usuÃ¡rio antes de adicionar ao grupo
        var comanda = await _comandaService.GetByIdAsync(comandaId);
        if (comanda != null && comanda.UserId == userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetComandaGroup(comandaId));
            _logger.LogInformation("Cliente {UserId} entrou no grupo Comanda_{ComandaId}", userId, comandaId);
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst("sub")
                 ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var id))
            throw new HubException("UsuÃ¡rio nÃ£o autenticado.");
        return id;
    }

    private string GetUserRole() =>
        Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Customer";

    public static string GetComandaGroup(Guid comandaId) => $"Comanda_{comandaId}";
    public static string GetUserGroup(Guid userId)       => $"User_{userId}";
}

// =============================================================================
// DTOs de eventos SignalR
// =============================================================================

public class ComandaUpdateEvent
{
    public Guid     ComandaId       { get; set; }
    public Guid?    UserId          { get; set; }
    public string   UserName        { get; set; } = string.Empty;
    public string?  TableIdentifier { get; set; }
    public decimal  TotalInReais    { get; set; }
    public string   Status          { get; set; } = string.Empty;
    public string?  LastItemAdded   { get; set; }
    public DateTime UpdatedAt       { get; set; }
}

