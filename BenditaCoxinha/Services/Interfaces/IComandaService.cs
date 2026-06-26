// =============================================================================
// IComandaService.cs â€” Interface do serviÃ§o de Comandas
// =============================================================================

using BenditaCoxinha.DTOs;

namespace BenditaCoxinha.Services.Interfaces;

/// <summary>Contrato para todas as operaÃ§Ãµes de negÃ³cio relacionadas a Comandas.</summary>
public interface IComandaService
{
    /// <summary>Cria e abre uma nova comanda para o usuÃ¡rio (chamado apÃ³s login rÃ¡pido).</summary>
    Task<ComandaDto> OpenComandaAsync(Guid userId, string? tableIdentifier = null);

    /// <summary>Retorna a comanda ativa (Aberta ou EmAndamento) de um usuÃ¡rio.</summary>
    Task<ComandaDto?> GetActiveComandaAsync(Guid userId);

    /// <summary>Retorna apenas o ID da comanda ativa (para o Hub SignalR).</summary>
    Task<Guid?> GetActiveComandaIdByUserAsync(Guid userId);

    /// <summary>Retorna uma comanda especÃ­fica pelo ID (Admin).</summary>
    Task<ComandaDto?> GetByIdAsync(Guid comandaId);

    /// <summary>Adiciona um item Ã  comanda do usuÃ¡rio e recalcula o total.</summary>
    Task<ComandaDto> AddItemAsync(Guid userId, AddItemToComandaRequest request);

    /// <summary>Admin adiciona item manualmente a uma comanda de cliente.</summary>
    Task<ComandaDto> AdminAddItemAsync(Guid comandaId, Guid adminId, AddItemToComandaRequest request);

    /// <summary>Remove um item de uma comanda (apenas Admin ou o prÃ³prio cliente).</summary>
    Task<ComandaDto> RemoveItemAsync(Guid comandaId, Guid itemId, Guid requestingUserId);

    /// <summary>
    /// Fecha a comanda (pagamento recebido).
    /// Se paymentMethod == "Crediario", cria um Crediario e envia email ao cliente.
    /// Suporta split payment: secondPaymentMethod + secondPaymentAmountInCents.
    /// </summary>
    Task<ComandaDto> CloseComandaAsync(Guid comandaId, Guid adminId, string paymentMethod = "Dinheiro", string? observacao = null, string? secondPaymentMethod = null, int secondPaymentAmountInCents = 0, Guid? crediarioExistenteId = null);

    /// <summary>Cancela a comanda sem cobranÃ§a.</summary>
    Task<ComandaDto> CancelComandaAsync(Guid comandaId, Guid adminId);

    /// <summary>Lista todas as comandas abertas/em andamento para o dashboard do Admin.</summary>
    Task<IEnumerable<ComandaDto>> GetActiveCommandasForDashboardAsync();

    /// <summary>Lista comandas fechadas e canceladas do dia especificado (padrÃ£o: hoje).</summary>
    Task<IEnumerable<ComandaDto>> GetTodayHistoryAsync(DateTime? data = null);

    /// <summary>Atualiza a quantidade de um item. Quantity=0 remove o item.</summary>
    Task<ComandaDto> UpdateItemAsync(Guid comandaId, Guid itemId, int newQuantity, Guid adminId);

    /// <summary>Aplica pontos do cliente Ã  comanda, abatendo do total a pagar.</summary>
    Task<ComandaDto> ApplyPointsAsync(Guid comandaId, Guid userId, int points);

    /// <summary>
    /// Remove os pontos aplicados Ã  comanda, devolvendo-os ao saldo do cliente.
    /// Pode ser chamado pelo prÃ³prio cliente ou por um Admin.
    /// </summary>
    Task<ComandaDto> RemovePointsAsync(Guid comandaId, Guid requestingUserId);

    /// <summary>Retorna as Ãºltimas comandas fechadas/canceladas do prÃ³prio usuÃ¡rio autenticado.</summary>
    Task<IEnumerable<ComandaDto>> GetUserHistoryAsync(Guid userId, int limit = 20);

    /// <summary>Edita uma comanda fechada (Admin only): pagamento, itens, desconto, cliente.</summary>
    Task<ComandaDto> EditarComandaFechadaAsync(Guid comandaId, Guid adminId, EditarComandaRequest request);
}

