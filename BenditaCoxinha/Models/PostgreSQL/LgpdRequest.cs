// =============================================================================
// LgpdRequest.cs â€” SolicitaÃ§Ã£o de direitos LGPD do titular
// Cobre os direitos previstos no Art. 18 da Lei 13.709/2018.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BenditaCoxinha.Models.PostgreSQL;

/// <summary>
/// Representa uma solicitaÃ§Ã£o formal de exercÃ­cio de direitos pelo titular dos dados.
/// Prazo legal de resposta: 15 dias (LGPD Art. 18 Â§ 5Â°).
/// </summary>
[Table("lgpd_requests")]
public class LgpdRequest
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>UsuÃ¡rio vinculado, se encontrado pelo CPF. Pode ser nulo para nÃ£o cadastrados.</summary>
    [Column("user_id")]
    public Guid? UserId { get; set; }

    // -------------------------------------------------------------------------
    // Dados do solicitante
    // -------------------------------------------------------------------------

    /// <summary>Nome completo informado pelo solicitante.</summary>
    [Required, MaxLength(200)]
    [Column("requester_name")]
    public string RequesterName { get; set; } = string.Empty;

    /// <summary>E-mail para envio da resposta.</summary>
    [Required, MaxLength(255)]
    [Column("requester_email")]
    public string RequesterEmail { get; set; } = string.Empty;

    /// <summary>CPF para identificaÃ§Ã£o do titular (11 dÃ­gitos, sem formataÃ§Ã£o).</summary>
    [Required, MaxLength(11)]
    [Column("requester_cpf")]
    public string RequesterCpf { get; set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Tipo e descriÃ§Ã£o
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tipo da solicitaÃ§Ã£o conforme Art. 18 LGPD.
    /// Valores: "Acesso" | "Retificacao" | "Exclusao" | "Portabilidade" | "Oposicao"
    /// </summary>
    [Required, MaxLength(20)]
    [Column("request_type")]
    public string RequestType { get; set; } = string.Empty;

    /// <summary>Detalhamento opcional da solicitaÃ§Ã£o pelo titular.</summary>
    [MaxLength(2000)]
    [Column("description")]
    public string? Description { get; set; }

    // -------------------------------------------------------------------------
    // Status e resposta
    // -------------------------------------------------------------------------

    /// <summary>
    /// Status atual da solicitaÃ§Ã£o.
    /// Valores: "Recebido" | "EmAnalise" | "Concluido" | "Negado"
    /// </summary>
    [Required, MaxLength(15)]
    [Column("status")]
    public string Status { get; set; } = "Recebido";

    /// <summary>Resposta formal do responsÃ¡vel pelo tratamento de dados.</summary>
    [MaxLength(4000)]
    [Column("admin_response")]
    public string? AdminResponse { get; set; }

    // -------------------------------------------------------------------------
    // Datas
    // -------------------------------------------------------------------------

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }

    /// <summary>Prazo legal de 15 dias a partir do recebimento (LGPD Art. 18 Â§ 5Â°).</summary>
    [Column("deadline")]
    public DateTime Deadline { get; set; } = DateTime.UtcNow.AddDays(15);

    // -------------------------------------------------------------------------
    // Anexo (arquivo opcional vinculado Ã  resposta)
    // -------------------------------------------------------------------------

    /// <summary>Nome original do arquivo anexado pelo admin (null se nÃ£o houver).</summary>
    [MaxLength(255)]
    [Column("anexo_nome")]
    public string? AnexoNome { get; set; }

    /// <summary>ConteÃºdo binÃ¡rio do arquivo (BYTEA no PostgreSQL). Limite: 10 MB.</summary>
    [Column("anexo_dados")]
    public byte[]? AnexoDados { get; set; }

    // -------------------------------------------------------------------------
    // NavegaÃ§Ã£o
    // -------------------------------------------------------------------------

    public User? User { get; set; }
}

