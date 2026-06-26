// =============================================================================
// AuditLog.cs â€” Registro de auditoria de aÃ§Ãµes sobre dados pessoais
// Trilha de auditoria exigida pela LGPD para demonstrar conformidade.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

/// <summary>
/// Registra todas as aÃ§Ãµes relevantes sobre dados pessoais realizadas no sistema.
/// Permite auditorias, rastreabilidade e comprovaÃ§Ã£o de conformidade com a LGPD.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // -------------------------------------------------------------------------
    // Ator (quem realizou a aÃ§Ã£o)
    // -------------------------------------------------------------------------

    /// <summary>ID do usuÃ¡rio que realizou a aÃ§Ã£o. Nulo para aÃ§Ãµes anÃ´nimas/sistema.</summary>
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    /// <summary>Nome do usuÃ¡rio que realizou a aÃ§Ã£o (snapshot no momento).</summary>
    [MaxLength(200)]
    [Column("actor_user_name")]
    public string? ActorUserName { get; set; }

    // -------------------------------------------------------------------------
    // AÃ§Ã£o
    // -------------------------------------------------------------------------

    /// <summary>
    /// DescriÃ§Ã£o da aÃ§Ã£o realizada.
    /// Exemplos: "Visualizou", "Editou", "Exportou", "Deletou", "Respondeu", "ConsentimentoRegistrado"
    /// </summary>
    [Required, MaxLength(50)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da entidade afetada.
    /// Exemplos: "User", "Comanda", "LgpdRequest", "CookieConsent"
    /// </summary>
    [Required, MaxLength(50)]
    [Column("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>ID da entidade afetada.</summary>
    [MaxLength(100)]
    [Column("entity_id")]
    public string? EntityId { get; set; }

    /// <summary>JSON com informaÃ§Ãµes adicionais de contexto.</summary>
    [Column("details")]
    public string? Details { get; set; }

    // -------------------------------------------------------------------------
    // IdentificaÃ§Ã£o da origem
    // -------------------------------------------------------------------------

    /// <summary>Hash SHA-256 do IP de origem â€” nunca armazenar o IP puro.</summary>
    [MaxLength(64)]
    [Column("ip_hash")]
    public string IpHash { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

