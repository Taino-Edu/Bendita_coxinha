// =============================================================================
// User.cs â€” Entidade de UsuÃ¡rio (PostgreSQL)
// Suporta dois perfis: Admin (Maikon) e Customer (clientes da loja)
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

/// <summary>
/// Representa um usuÃ¡rio do sistema.
/// Admin = dono da loja (Maikon), com acesso total ao painel.
/// Customer = cliente que entra via QR Code com login simplificado.
/// </summary>
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Dados bÃ¡sicos â€” preenchidos por todos os usuÃ¡rios
    // -------------------------------------------------------------------------

    /// <summary>Nome completo do usuÃ¡rio.</summary>
    [Required, MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// E-mail: obrigatÃ³rio para Admin e para registro em Campeonatos.
    /// Para o login rÃ¡pido de Customer, pode ser nulo.
    /// </summary>
    [MaxLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    /// <summary>Senha com hash BCrypt. Nula para clientes de login rÃ¡pido.</summary>
    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    // -------------------------------------------------------------------------
    // Dados do cliente (login rÃ¡pido via QR Code)
    // -------------------------------------------------------------------------

    /// <summary>WhatsApp do cliente (formato: 5511999999999).</summary>
    [MaxLength(20)]
    [Column("whatsapp")]
    public string? WhatsApp { get; set; }

    /// <summary>CPF do cliente (armazenado sem formataÃ§Ã£o: 11 dÃ­gitos).</summary>
    [MaxLength(11)]
    [Column("cpf")]
    public string? Cpf { get; set; }

    /// <summary>URL da foto de perfil do usuÃ¡rio (avatar).</summary>
    [MaxLength(500)]
    [Column("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    // -------------------------------------------------------------------------
    // Controle de acesso
    // -------------------------------------------------------------------------

    /// <summary>
    /// Perfil RBAC. Valores vÃ¡lidos: "Admin" | "Customer"
    /// Use a enum UserRole para evitar strings mÃ¡gicas no cÃ³digo.
    /// </summary>
    [Required, MaxLength(20)]
    [Column("role")]
    public string Role { get; set; } = UserRole.Customer;

    // -------------------------------------------------------------------------
    // "Lembre-se de mim" â€” Refresh Token
    // -------------------------------------------------------------------------

    /// <summary>Token opaco usado para renovar o JWT sem novo login.</summary>
    [Column("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>Data de expiraÃ§Ã£o do refresh token.</summary>
    [Column("refresh_token_expiry")]
    public DateTime? RefreshTokenExpiry { get; set; }

    // -------------------------------------------------------------------------
    // RecuperaÃ§Ã£o de senha
    // -------------------------------------------------------------------------

    /// <summary>Token de uso Ãºnico para redefiniÃ§Ã£o de senha (2h de validade).</summary>
    [Column("password_reset_token")]
    public string? PasswordResetToken { get; set; }

    [Column("password_reset_token_expiry")]
    public DateTime? PasswordResetTokenExpiry { get; set; }

    // -------------------------------------------------------------------------
    // Sistema de Pontos
    // -------------------------------------------------------------------------

    /// <summary>Saldo de pontos do cliente. Pontos sÃ£o adicionados pelo Admin.</summary>
    [Column("points_balance")]
    public int PointsBalance { get; set; } = 0;

    /// <summary>Data de expiraÃ§Ã£o dos pontos (30 dias apÃ³s a Ãºltima adiÃ§Ã£o).</summary>
    [Column("points_expires_at")]
    public DateTime? PointsExpiresAt { get; set; }

    // -------------------------------------------------------------------------
    // Sistema de Saldo MonetÃ¡rio
    // -------------------------------------------------------------------------

    /// <summary>
    /// Saldo monetÃ¡rio em centavos (crÃ©dito na loja, separado dos pontos).
    /// Pode ser carregado pelo Admin e usado no fechamento de comandas.
    /// </summary>
    [Column("balance_in_cents")]
    public int BalanceInCents { get; set; } = 0;

    /// <summary>Saldo em reais para exibiÃ§Ã£o.</summary>
    [NotMapped]
    public decimal BalanceInReais => BalanceInCents / 100m;

    // -------------------------------------------------------------------------
    // PreferÃªncias do usuÃ¡rio (JSON livre)
    // -------------------------------------------------------------------------

    /// <summary>ConfiguraÃ§Ãµes pessoais salvas como JSON (botÃ£o IA, sons, desconto padrÃ£o, etc.).</summary>
    [Column("preferences_json")]
    public string? PreferencesJson { get; set; }

    // -------------------------------------------------------------------------
    // Auditoria
    // -------------------------------------------------------------------------

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // -------------------------------------------------------------------------
    // LGPD â€” Ciclo de vida e consentimento
    // -------------------------------------------------------------------------

    /// <summary>
    /// Data em que o titular foi anonimizado (exclusÃ£o lÃ³gica por LGPD).
    /// Null enquanto a conta estÃ¡ ativa.
    /// </summary>
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Data em que o titular deu consentimento explÃ­cito ao uso de seus dados.
    /// Registrado no primeiro quick-login com checkbox de consentimento marcado.
    /// </summary>
    [Column("consent_at")]
    public DateTime? ConsentAt { get; set; }

    // -------------------------------------------------------------------------
    // NavegaÃ§Ã£o (relacionamentos)
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Perfil de operador (apenas para Role == Operator)
    // -------------------------------------------------------------------------

    /// <summary>Perfil de permissÃµes atribuÃ­do pelo Admin. Nulo para Admin e Customer.</summary>
    [Column("perfil_id")]
    public Guid? PerfilId { get; set; }

    [ForeignKey(nameof(PerfilId))]
    public Perfil? Perfil { get; set; }

    // -------------------------------------------------------------------------
    // NavegaÃ§Ã£o (relacionamentos)
    // -------------------------------------------------------------------------

    /// <summary>Comandas abertas ou histÃ³ricas deste usuÃ¡rio.</summary>
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();

    /// <summary>ParticipaÃ§Ãµes em campeonatos.</summary>
    public ICollection<ChampionshipParticipant> ChampionshipParticipants { get; set; } = new List<ChampionshipParticipant>();
}

/// <summary>
/// Constantes de perfil para evitar strings mÃ¡gicas no cÃ³digo.
/// Exemplo de uso: user.Role = UserRole.Admin
/// </summary>
public static class UserRole
{
    public const string Admin    = "Admin";
    public const string Operator = "Operator";
    public const string Customer = "Customer";
}

