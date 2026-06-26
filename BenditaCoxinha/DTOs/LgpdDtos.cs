// =============================================================================
// LgpdDtos.cs â€” Objetos de transferÃªncia para endpoints LGPD
// =============================================================================

using System.ComponentModel.DataAnnotations;
using BenditaCoxinha.Validation;

namespace BenditaCoxinha.DTOs;

// â”€â”€ Entrada (solicitante) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Payload para abertura de uma nova solicitaÃ§Ã£o LGPD.</summary>
public class LgpdRequestCreate
{
    [Required(ErrorMessage = "O nome Ã© obrigatÃ³rio.")]
    [MaxLength(200)]
    public string RequesterName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail Ã© obrigatÃ³rio.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail vÃ¡lido.")]
    [MaxLength(255)]
    public string RequesterEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "O CPF Ã© obrigatÃ³rio.")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "CPF deve conter exatamente 11 dÃ­gitos numÃ©ricos.")]
    [CpfValid]
    public string RequesterCpf { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da solicitaÃ§Ã£o conforme Art. 18 LGPD.
    /// Valores aceitos: Acesso | Retificacao | Exclusao | Portabilidade | Oposicao
    /// </summary>
    [Required(ErrorMessage = "O tipo de solicitaÃ§Ã£o Ã© obrigatÃ³rio.")]
    public string RequestType { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }
}

// â”€â”€ SaÃ­da (para o solicitante) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Retorno ao solicitante apÃ³s abertura da solicitaÃ§Ã£o.</summary>
public class LgpdRequestReceived
{
    public string   Protocol  { get; set; } = string.Empty;
    public DateTime Deadline  { get; set; }
    public string   Message   { get; set; } = string.Empty;
}

/// <summary>Dados da solicitaÃ§Ã£o retornados ao consultar pelo protocolo.</summary>
public class LgpdRequestResponse
{
    public string    Id            { get; set; } = string.Empty;
    public string    RequestType   { get; set; } = string.Empty;
    public string    Status        { get; set; } = string.Empty;
    public string?   AdminResponse { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime  Deadline      { get; set; }
    public DateTime? RespondedAt   { get; set; }
}

// â”€â”€ Admin â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Payload para resposta do admin a uma solicitaÃ§Ã£o LGPD.</summary>
public class LgpdAdminResponse
{
    /// <summary>Novo status: "EmAnalise" | "Concluido" | "Negado"</summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    [Required(ErrorMessage = "A resposta Ã© obrigatÃ³ria.")]
    [MaxLength(4000)]
    public string AdminResponse { get; set; } = string.Empty;
}

/// <summary>Resumo de uma solicitaÃ§Ã£o LGPD para listagem no painel admin.</summary>
public class LgpdRequestAdminDto
{
    public string    Id             { get; set; } = string.Empty;
    public string    RequesterName  { get; set; } = string.Empty;
    public string    RequesterEmail { get; set; } = string.Empty;
    public string    RequesterCpf   { get; set; } = string.Empty;
    public string    RequestType    { get; set; } = string.Empty;
    public string?   Description    { get; set; }
    public string    Status         { get; set; } = string.Empty;
    public string?   AdminResponse  { get; set; }
    public DateTime  CreatedAt      { get; set; }
    public DateTime  Deadline       { get; set; }
    public DateTime? RespondedAt    { get; set; }
    public bool      IsOverdue      { get; set; }
    public bool      IsUrgent       { get; set; }
    public bool      TemAnexo       { get; set; }
    public string?   AnexoNome      { get; set; }
}

// â”€â”€ Audit Log â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Entrada de audit log para listagem paginada no painel admin.</summary>
public class AuditLogDto
{
    public string   Id            { get; set; } = string.Empty;
    public string?  ActorUserId   { get; set; }
    public string?  ActorUserName { get; set; }
    public string   Action        { get; set; } = string.Empty;
    public string   EntityType    { get; set; } = string.Empty;
    public string?  EntityId      { get; set; }
    public string?  Details       { get; set; }
    public DateTime CreatedAt     { get; set; }
}

/// <summary>Resposta paginada de audit logs.</summary>
public class AuditLogPagedResponse
{
    public IEnumerable<AuditLogDto> Items       { get; set; } = [];
    public int                      TotalCount  { get; set; }
    public int                      Page        { get; set; }
    public int                      PageSize    { get; set; }
    public int                      TotalPages  { get; set; }
}

// â”€â”€ Cookie Consent â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Payload para registro de consentimento de cookies.</summary>
public class CookieConsentCreate
{
    public bool Accepted { get; set; }
}

