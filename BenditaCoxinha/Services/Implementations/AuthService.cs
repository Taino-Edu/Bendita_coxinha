// =============================================================================
// AuthService.cs â€” ImplementaÃ§Ã£o de AutenticaÃ§Ã£o
// =============================================================================
using BenditaCoxinha.Configuration;
using BenditaCoxinha.Data;
using BenditaCoxinha.DTOs;
using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BenditaCoxinha.Services.Implementations;

/// <summary>
/// ImplementaÃ§Ã£o do serviÃ§o de autenticaÃ§Ã£o.
/// ResponsÃ¡vel por: login completo, login rÃ¡pido (QR Code), refresh tokens e logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext          _db;
    private readonly JwtSettings           _jwt;
    private readonly ILogger<AuthService>  _logger;
    private readonly IComandaService       _comandaService;
    private readonly IEmailService         _email;

    public AuthService(
        AppDbContext db,
        IOptions<JwtSettings> jwt,
        ILogger<AuthService> logger,
        IComandaService comandaService,
        IEmailService email)
    {
        _db             = db;
        _jwt            = jwt.Value;
        _logger         = logger;
        _comandaService = comandaService;
        _email          = email;
    }

    // =========================================================================
    // LOGIN COMPLETO â€” Admin e jogadores de campeonato
    // =========================================================================
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        // PasswordHash pode ser null para clientes de quick-login.
        // Verificar null antes de chamar BCrypt.Verify evita NullReferenceException.
        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("E-mail ou senha invÃ¡lidos.");

        return await GenerateAuthResponseAsync(user);
    }

    // =========================================================================
    // LOGIN RÃPIDO â€” Customer via QR Code (CPF + WhatsApp)
    // =========================================================================
    public async Task<AuthResponse> QuickLoginAsync(QuickLoginRequest request)
    {
        // Busca por CPF primeiro
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == request.Cpf);

        if (user == null)
        {
            // Cria o cliente automaticamente na primeira visita
            user = new User
            {
                Name      = request.Name,
                Cpf       = request.Cpf,
                WhatsApp  = request.WhatsApp,
                Role      = UserRole.Customer,
                IsActive  = true
            };
            _db.Users.Add(user);
            // LGPD: CPF nÃ£o Ã© logado â€” dado sensÃ­vel do titular
            _logger.LogInformation("Novo cliente criado via QR Code: {Name}", request.Name);
        }
        else
        {
            // Atualiza dados se necessÃ¡rio
            user.Name     = request.Name;
            user.WhatsApp = request.WhatsApp;
            user.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // CPF duplicado por requisiÃ§Ã£o concorrente â€” descarta a entidade em estado invÃ¡lido
            // e busca o usuÃ¡rio jÃ¡ criado pela outra thread.
            _db.ChangeTracker.Clear();
            user = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == request.Cpf && u.IsActive);
            if (user == null) throw;
        }

        // Abre (ou reutiliza) a comanda para esta mesa/sessÃ£o
        var comanda = await _comandaService.OpenComandaAsync(user.Id, request.TableIdentifier);
        _logger.LogInformation("Comanda {ComandaId} associada ao quick-login de {Name}", comanda.Id, user.Name);

        return await GenerateAuthResponseAsync(user, comanda.Id);
    }

    // =========================================================================
    // REFRESH TOKEN
    // =========================================================================
    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var hashedToken = HashRefreshToken(request.RefreshToken);
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.RefreshToken == hashedToken && u.IsActive
        );

        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token invÃ¡lido ou expirado.");

        return await GenerateAuthResponseAsync(user);
    }

    // =========================================================================
    // LOGOUT
    // =========================================================================
    public async Task LogoutAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.RefreshToken       = null;
            user.RefreshTokenExpiry = null;
            user.UpdatedAt          = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // =========================================================================
    // HELPERS PRIVADOS
    // =========================================================================

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user, Guid? comandaId = null)
    {
        // Carrega perfil do Operator para incluir permissÃµes no JWT
        string[]? permissions = null;
        if (user.Role == UserRole.Operator && user.PerfilId.HasValue)
        {
            var perfil = await _db.Perfis.FindAsync(user.PerfilId.Value);
            if (perfil != null)
            {
                try { permissions = System.Text.Json.JsonSerializer.Deserialize<string[]>(perfil.PermissoesJson); }
                catch { permissions = []; }
            }
        }

        var accessToken  = GenerateJwt(user, permissions);
        var refreshToken = GenerateRefreshToken();
        var expiresAt    = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes);

        // Armazena somente o hash SHA-256 â€” o token bruto sÃ³ sai no cookie HttpOnly.
        // Se o banco for comprometido, os hashes nÃ£o sÃ£o diretamente utilizÃ¡veis.
        user.RefreshToken       = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays);
        user.UpdatedAt          = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshToken, expiresAt, user.Role, user.Name, user.Id, comandaId, permissions);
    }

    private string GenerateJwt(User user, string[]? permissions = null)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name,  user.Name),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(ClaimTypes.Role,               user.Role)
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));

        if (permissions != null && permissions.Length > 0)
            claims.Add(new("permissions", System.Text.Json.JsonSerializer.Serialize(permissions)));

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Gera um refresh token aleatÃ³rio e seguro (256 bits).</summary>
    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Retorna SHA-256 hex do token â€” o que Ã© persistido no banco.
    /// O token bruto trafega apenas no cookie HttpOnly.
    /// </summary>
    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // =========================================================================
    // ACESSO DO CLIENTE PELO SITE
    // =========================================================================

    public async Task<CpfLookupResponse> LookupByCpfAsync(string cpf)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == cpf && u.IsActive);
        if (user == null)
            throw new KeyNotFoundException("CPF nÃ£o encontrado. Acesse a loja e escaneie o QR Code para criar sua conta.");

        return new CpfLookupResponse(user.Name, user.PasswordHash != null);
    }

    public async Task<AuthResponse> SetupAccountAsync(SetupAccountRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == request.Cpf && u.IsActive);
        if (user == null)
            throw new KeyNotFoundException("CPF nÃ£o encontrado.");

        var emailInUse = await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant() && u.Id != user.Id);
        if (emailInUse)
            throw new InvalidOperationException("Este e-mail jÃ¡ estÃ¡ em uso por outra conta.");

        user.Email        = request.Email.ToLowerInvariant();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.UpdatedAt    = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Conta ativada para cliente {Name}", user.Name);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> ClientLoginAsync(ClientLoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == request.Email.ToLowerInvariant() && u.IsActive && u.Role == UserRole.Customer);

        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("E-mail ou senha invÃ¡lidos.");

        return await GenerateAuthResponseAsync(user);
    }

    // =========================================================================
    // RECUPERAÃ‡ÃƒO DE SENHA
    // =========================================================================

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        // Sempre retorna sem erro â€” nÃ£o revelar se email existe (evita user enumeration)
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && u.IsActive);

        if (user == null)
        {
            await Task.Delay(Random.Shared.Next(200, 500)); // timing equalization
            return;
        }

        // Gera token seguro e salva com expiraÃ§Ã£o de 2h
        var tokenBytes = new byte[32];
        using var rng  = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);

        user.PasswordResetToken       = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);
        user.UpdatedAt                = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _email.SendPasswordResetAsync(user.Email!, user.Name, token);
        _logger.LogInformation("SolicitaÃ§Ã£o de reset de senha para {Email}", MaskEmail(request.Email));
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow &&
            u.IsActive);

        if (user == null)
            throw new UnauthorizedAccessException("Token invÃ¡lido ou expirado.");

        user.PasswordHash             = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken       = null;
        user.PasswordResetTokenExpiry = null;
        user.RefreshToken             = null; // invalida sessÃµes ativas
        user.RefreshTokenExpiry       = null;
        user.UpdatedAt                = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Senha redefinida para usuÃ¡rio {UserId}", user.Id);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var visible = local.Length > 1 ? local[0] + new string('*', Math.Min(local.Length - 1, 3)) : "*";
        return visible + email[at..];
    }
}

