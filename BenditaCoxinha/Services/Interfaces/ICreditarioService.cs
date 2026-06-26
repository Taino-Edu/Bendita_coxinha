// =============================================================================
// ICreditarioService.cs â€” Interface do serviÃ§o de CrediÃ¡rio
// =============================================================================

using BenditaCoxinha.DTOs;

namespace BenditaCoxinha.Services.Interfaces;

public interface ICreditarioService
{
    /// <summary>
    /// Cria um novo crediÃ¡rio quando o Admin fecha uma comanda com pagamento em crediÃ¡rio.
    /// Valida se o cliente jÃ¡ tem um crediÃ¡rio aberto.
    /// </summary>
    Task<CrediariosDto> CreateAsync(Guid comandaId, Guid userId, int valorEmCentavos, Guid adminId);

    /// <summary>
    /// Retorna TODOS os crediÃ¡rios (abertos e pagos).
    /// </summary>
    Task<List<CrediariosDto>> GetAllAsync();

    /// <summary>
    /// Retorna todos os crediÃ¡rios de um usuÃ¡rio (abertos e pagos).
    /// </summary>
    Task<List<CrediariosDto>> GetByUserAsync(Guid userId);

    /// <summary>
    /// Retorna todos os crediÃ¡rios abertos (nÃ£o pagos) e vencidos.
    /// Ãštil para dashboard do admin.
    /// </summary>
    Task<List<CrediariosDto>> GetAbertoAsync();

    /// <summary>
    /// Retorna todos os crediÃ¡rios vencidos (abertos e alÃ©m da data de vencimento).
    /// </summary>
    Task<List<CrediariosDto>> GetVencidosAsync();

    /// <summary>
    /// Marca um crediÃ¡rio como pago.
    /// Usa o token do admin para rastrear quem pagou.
    /// </summary>
    Task<CrediariosDto> MarkAsPaidAsync(Guid creditarioId, Guid adminId, string? observacao = null);

    /// <summary>
    /// Retorna um crediÃ¡rio especÃ­fico por ID.
    /// </summary>
    Task<CrediariosDto?> GetByIdAsync(Guid creditarioId);

    /// <summary>
    /// Verifica se um usuÃ¡rio tem um crediÃ¡rio aberto (bloqueia nova comanda).
    /// </summary>
    Task<bool> HasOpenAsync(Guid userId);

    /// <summary>
    /// Retorna o crediÃ¡rio aberto de um usuÃ¡rio, ou null se nÃ£o houver.
    /// </summary>
    Task<CrediariosDto?> GetOpenAsync(Guid userId);

    /// <summary>
    /// Calcula o total devido por um usuÃ¡rio (todos os crediÃ¡rios abertos).
    /// </summary>
    Task<decimal> GetTotalDevidoAsync(Guid userId);
}

