// =============================================================================
// IAuthService.cs â€” Interface do serviÃ§o de AutenticaÃ§Ã£o
// =============================================================================

using BenditaCoxinha.DTOs;

namespace BenditaCoxinha.Services.Interfaces;

/// <summary>Contrato para autenticaÃ§Ã£o, geraÃ§Ã£o e renovaÃ§Ã£o de tokens JWT.</summary>
public interface IAuthService
{
    /// <summary>Login completo (Admin / jogadores de campeonato).</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Login rÃ¡pido via QR Code (Customer).
    /// Cria o usuÃ¡rio se ainda nÃ£o existir (baseado no CPF).
    /// </summary>
    Task<AuthResponse> QuickLoginAsync(QuickLoginRequest request);

    /// <summary>Renova o AccessToken usando o RefreshToken armazenado.</summary>
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);

    /// <summary>Invalida o RefreshToken (logout).</summary>
    Task LogoutAsync(Guid userId);

    /// <summary>
    /// Gera token de reset, persiste no banco e dispara email.
    /// NÃ£o revela se o email existe (evita user enumeration).
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>Valida o token e redefine a senha.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>Busca cliente por CPF â€” retorna nome e se jÃ¡ tem senha.</summary>
    Task<CpfLookupResponse> LookupByCpfAsync(string cpf);

    /// <summary>Ativa conta de cliente existente: define email + senha.</summary>
    Task<AuthResponse> SetupAccountAsync(SetupAccountRequest request);

    /// <summary>Login de cliente pelo site (email + senha).</summary>
    Task<AuthResponse> ClientLoginAsync(ClientLoginRequest request);
}

