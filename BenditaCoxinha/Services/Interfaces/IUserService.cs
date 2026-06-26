// =============================================================================
// IUserService.cs â€” Interface do serviÃ§o de usuÃ¡rios e pontos
// =============================================================================

using BenditaCoxinha.DTOs;

namespace BenditaCoxinha.Services.Interfaces;

public interface IUserService
{
    /// <summary>Lista todos os usuÃ¡rios ativos (Admin).</summary>
    Task<IEnumerable<UserSummaryDto>> GetAllAsync(string? search = null, string? role = null);

    /// <summary>Retorna um usuÃ¡rio pelo ID (Admin).</summary>
    Task<UserSummaryDto?> GetByIdAsync(Guid id);

    /// <summary>Perfil completo do usuÃ¡rio logado.</summary>
    Task<UserProfileDto?> GetProfileAsync(Guid userId);

    /// <summary>Adiciona pontos ao saldo de um usuÃ¡rio. Redefine a validade para +30 dias.</summary>
    Task<UserSummaryDto> AddPointsAsync(Guid userId, AddPointsRequest request, Guid adminId);

    /// <summary>Deduz pontos do saldo do usuÃ¡rio (usado ao resgatar na comanda).</summary>
    Task DeductPointsAsync(Guid userId, int points);

    /// <summary>
    /// Ajusta o saldo monetÃ¡rio do cliente.
    /// Positivo = crÃ©dito (recarga). Negativo = dÃ©bito (uso/desconto na comanda).
    /// LanÃ§a InvalidOperationException se o dÃ©bito ultrapassar o saldo disponÃ­vel.
    /// </summary>
    Task<UserSummaryDto> AdjustBalanceAsync(Guid userId, AdjustBalanceRequest request, Guid adminId);

    // â”€â”€ LGPD â€” Direitos do titular â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Atualiza a URL da foto de perfil do usuÃ¡rio.</summary>
    Task<UserProfileDto> UpdateProfileImageAsync(Guid userId, string? imageUrl);

    /// <summary>
    /// Permite ao titular corrigir seus prÃ³prios dados (nome, e-mail, WhatsApp).
    /// Direito de retificaÃ§Ã£o conforme Art. 18, IV da LGPD.
    /// </summary>
    Task<UserProfileDto> UpdateMeAsync(Guid userId, UpdateMeRequest request);

    /// <summary>
    /// Anonimiza os dados do titular (exclusÃ£o lÃ³gica).
    /// Substitui dados pessoais por valores neutros em vez de deletar fisicamente
    /// o registro, preservando a integridade referencial das comandas e crediÃ¡rios.
    /// Direito de exclusÃ£o conforme Art. 18, VI da LGPD.
    /// </summary>
    Task AnonimizarAsync(Guid userId);

    // â”€â”€ Admin â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Admin cria diretamente uma conta de cliente.</summary>
    Task<UserSummaryDto> AdminCreateUserAsync(AdminCreateUserRequest request, Guid adminId);

    /// <summary>Admin redefine a senha de um cliente (sem e-mail de confirmaÃ§Ã£o).</summary>
    Task AdminResetPasswordAsync(Guid userId, AdminResetPasswordRequest request, Guid adminId);
}

