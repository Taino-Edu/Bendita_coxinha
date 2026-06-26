// =============================================================================
// AuthController.cs â€” Endpoints de AutenticaÃ§Ã£o
//
// POST /api/auth/login         â†’ Login do Admin (email + senha)
// POST /api/auth/quick-login   â†’ Login do Cliente via QR Code (CPF + WhatsApp)
// POST /api/auth/refresh       â†’ Renovar o access token usando o refresh token
// POST /api/auth/logout        â†’ Invalidar o refresh token (encerrar sessÃ£o)
// =============================================================================

using BenditaCoxinha.DTOs;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using BenditaCoxinha.Configuration;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService            _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly JwtSettings             _jwt;
    private readonly IWebHostEnvironment     _env;
    private readonly IEmailService           _emailService;

    public AuthController(
        IAuthService        authService,
        ILogger<AuthController> logger,
        IOptions<JwtSettings>   jwt,
        IWebHostEnvironment     env,
        IEmailService       emailService)
    {
        _authService  = authService;
        _logger       = logger;
        _jwt          = jwt.Value;
        _env          = env;
        _emailService = emailService;
    }

    // =========================================================================
    // HELPERS â€” Cookies HttpOnly (LGPD / SeguranÃ§a)
    // =========================================================================

    /// <summary>
    /// Grava accessToken e refreshToken como cookies HttpOnly,
    /// impedindo acesso via JavaScript (proteÃ§Ã£o contra XSS).
    /// </summary>
    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        // Secure = true em produÃ§Ã£o (HTTPS via Cloudflare). Em desenvolvimento HTTP local, false.
        var secureCookies = !_env.IsDevelopment();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure   = secureCookies,
            SameSite = SameSiteMode.Lax, // Lax permite redirecionamentos cross-page
            Path     = "/",
        };

        Response.Cookies.Append("accessToken", accessToken, new CookieOptions
        {
            HttpOnly = cookieOptions.HttpOnly,
            Secure   = cookieOptions.Secure,
            SameSite = cookieOptions.SameSite,
            Path     = cookieOptions.Path,
            MaxAge   = TimeSpan.FromMinutes(_jwt.AccessTokenExpirationMinutes)
        });

        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = cookieOptions.HttpOnly,
            Secure   = cookieOptions.Secure,
            SameSite = cookieOptions.SameSite,
            Path     = cookieOptions.Path,
            MaxAge   = TimeSpan.FromDays(_jwt.RefreshTokenExpirationDays)
        });
    }

    /// <summary>Remove os cookies de autenticaÃ§Ã£o no logout.</summary>
    private void ClearAuthCookies()
    {
        Response.Cookies.Delete("accessToken");
        Response.Cookies.Delete("refreshToken");
    }

    // =========================================================================
    // LOGIN COMPLETO â€” Admin (email + senha)
    // =========================================================================

    /// <summary>
    /// Login com e-mail e senha. Utilizado pelo Admin (Maikon).
    /// Retorna um access token JWT (60 min) e um refresh token (30 dias).
    /// </summary>
    /// <response code="200">Login realizado com sucesso.</response>
    /// <response code="401">Credenciais invÃ¡lidas.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await _authService.LoginAsync(request);
            _logger.LogInformation("Login bem-sucedido para {Email}", request.Email);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId, Permissions: response.Permissions));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Tentativa de login invÃ¡lida para {Email}: {Msg}", request.Email, ex.Message);
            return Unauthorized(new { Message = "E-mail ou senha incorretos." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no login para {Email}", request.Email);
            return StatusCode(500, new { Message = "Erro interno. Tente novamente." });
        }
    }

    // =========================================================================
    // LOGIN RÃPIDO â€” Cliente via QR Code (CPF + WhatsApp)
    // =========================================================================

    /// <summary>
    /// Login rÃ¡pido para clientes via QR Code nas mesas.
    /// Cria o usuÃ¡rio automaticamente se for a primeira visita (identificado pelo CPF).
    /// Abre (ou reutiliza) a comanda da mesa automaticamente.
    /// Retorna o access token + o ID da comanda aberta.
    /// </summary>
    /// <response code="200">Login e comanda abertura realizados com sucesso.</response>
    /// <response code="400">Dados invÃ¡lidos.</response>
    [HttpPost("quick-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> QuickLogin([FromBody] QuickLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await _authService.QuickLoginAsync(request);
            // LGPD: CPF removido do log â€” apenas nome e mesa sÃ£o necessÃ¡rios para auditoria
            _logger.LogInformation(
                "Quick-login realizado: {Name} | Mesa: {Table} | Comanda: {ComandaId}",
                request.Name, request.TableIdentifier, response.ComandaId);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId, response.ComandaId, response.Permissions));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Quick-login com dados invÃ¡lidos: {Msg}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Erro de operaÃ§Ã£o no quick-login: {Msg}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no quick-login");
            return StatusCode(500, new { Message = "Erro interno. Tente novamente." });
        }
    }

    // =========================================================================
    // REFRESH TOKEN â€” Renovar o access token sem novo login
    // =========================================================================

    /// <summary>
    /// Renova o access token usando o refresh token.
    /// Use quando o access token expirar (apÃ³s 60 minutos).
    /// O refresh token Ã© vÃ¡lido por 30 dias.
    /// </summary>
    /// <response code="200">Novo access token gerado.</response>
    /// <response code="401">Refresh token invÃ¡lido ou expirado.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request)
    {
        // Cookie HttpOnly tem prioridade; fallback para o body (compatibilidade)
        var refreshToken = Request.Cookies["refreshToken"]
                           ?? request?.RefreshToken;

        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { Message = "Refresh token nÃ£o encontrado." });

        try
        {
            var tokenRequest = new RefreshTokenRequest(refreshToken);
            var response = await _authService.RefreshTokenAsync(tokenRequest);
            // Renova os cookies com os novos tokens
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId, Permissions: response.Permissions));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Refresh token invÃ¡lido ou expirado: {Msg}", ex.Message);
            ClearAuthCookies();
            return Unauthorized(new { Message = "Refresh token invÃ¡lido ou expirado. FaÃ§a login novamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no refresh de token");
            return StatusCode(500, new { Message = "Erro interno. Tente novamente." });
        }
    }

    // =========================================================================
    // ACESSO DO CLIENTE PELO SITE
    // =========================================================================

    [HttpPost("cpf-lookup")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> CpfLookup([FromBody] CpfLookupRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _authService.LookupByCpfAsync(request.Cpf);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { Message = ex.Message }); }
    }

    [HttpPost("setup-account")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SetupAccount([FromBody] SetupAccountRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var response = await _authService.SetupAccountAsync(request);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId));
        }
        catch (KeyNotFoundException ex)    { return NotFound(new { Message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { Message = ex.Message }); }
    }

    [HttpPost("client-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ClientLogin([FromBody] ClientLoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var response = await _authService.ClientLoginAsync(request);
            SetAuthCookies(response.AccessToken, response.RefreshToken);
            return Ok(new SafeAuthResponse(response.ExpiresAt, response.Role, response.UserName, response.UserId));
        }
        catch (UnauthorizedAccessException) { return Unauthorized(new { Message = "E-mail ou senha invÃ¡lidos." }); }
    }

    // =========================================================================
    // FORGOT PASSWORD â€” Solicitar reset por email
    // =========================================================================

    /// <summary>
    /// Envia email com link de redefiniÃ§Ã£o de senha.
    /// Sempre retorna 204 para nÃ£o revelar se o email existe.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _authService.ForgotPasswordAsync(request);
        return NoContent();
    }

    // =========================================================================
    // RESET PASSWORD â€” Redefinir senha com token do email
    // =========================================================================

    /// <summary>
    /// Redefine a senha usando o token recebido por email.
    /// O token expira em 2 horas e Ã© de uso Ãºnico.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _authService.ResetPasswordAsync(request);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Message = ex.Message });
        }
    }

    // =========================================================================
    // LOGOUT â€” Invalidar o refresh token
    // =========================================================================

    /// <summary>
    /// Encerra a sessÃ£o do usuÃ¡rio autenticado.
    /// Invalida o refresh token â€” o prÃ³ximo acesso exige novo login.
    /// </summary>
    /// <response code="204">Logout realizado com sucesso.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout()
    {
        var claim  = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            return Unauthorized();

        await _authService.LogoutAsync(userId);
        ClearAuthCookies();
        _logger.LogInformation("Logout realizado para usuÃ¡rio {UserId}", userId);
        return NoContent();
    }

    // =========================================================================
    // DIAGNÃ“STICO â€” Teste de Email
    // =========================================================================

    /// <summary>
    /// Envia um email de teste para verificar as configuraÃ§Ãµes de SMTP.
    /// Apenas Admin pode disparar este teste.
    /// </summary>
    [HttpPost("test-email")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
    {
        var success = await _emailService.SendDiagnosticEmailAsync(request.Email);
        
        return success 
            ? Ok(new { Message = $"Email de teste enviado com sucesso para {request.Email}. Verifique sua caixa de entrada e SPAM." })
            : BadRequest(new { Message = "Falha ao enviar email. Verifique os logs do servidor para detalhes do erro de SMTP." });
    }
}

