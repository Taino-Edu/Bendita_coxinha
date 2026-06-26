// =============================================================================
// LgpdController.cs â€” Endpoints pÃºblicos e admin para compliance LGPD
//
// PÃºblico:
//   POST /api/lgpd/request         â†’ abre solicitaÃ§Ã£o de direitos (Art. 18 LGPD)
//   GET  /api/lgpd/request/{id}    â†’ consulta status pelo protocolo
//   POST /api/lgpd/consent         â†’ registra consentimento de cookies
//
// Admin (requer autenticaÃ§Ã£o + perfil Admin):
//   GET  /api/lgpd/requests        â†’ lista todas as solicitaÃ§Ãµes
//   PUT  /api/lgpd/requests/{id}/respond â†’ responde uma solicitaÃ§Ã£o
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BenditaCoxinha.Data;
using BenditaCoxinha.DTOs;
using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LgpdController : ControllerBase
{
    private readonly AppDbContext   _db;
    private readonly IEmailService  _email;
    private readonly ILogger<LgpdController> _logger;
    private readonly string         _ipSalt;

    private const string PrivacidadeEmail = "privacidade@benditacoxinha.com.br";

    public LgpdController(
        AppDbContext          db,
        IEmailService         email,
        ILogger<LgpdController> logger,
        IConfiguration        configuration)
    {
        _db     = db;
        _email  = email;
        _logger = logger;
        _ipSalt = configuration["Security:IpHashSalt"] ?? "softnerd-ip-salt-dev";
    }

    // =========================================================================
    // PÃšBLICO â€” Abertura de solicitaÃ§Ã£o
    // =========================================================================

    /// <summary>
    /// Abre uma solicitaÃ§Ã£o formal de exercÃ­cio de direitos pelo titular.
    /// LGPD Art. 18: Acesso, RetificaÃ§Ã£o, ExclusÃ£o, Portabilidade ou OposiÃ§Ã£o.
    /// </summary>
    [HttpPost("request")]
    [AllowAnonymous]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(LgpdRequestReceived), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateRequest([FromBody] LgpdRequestCreate dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Valida tipo de solicitaÃ§Ã£o
        var tiposValidos = new[] { "Acesso", "Retificacao", "Exclusao", "Portabilidade", "Oposicao" };
        if (!tiposValidos.Contains(dto.RequestType))
            return BadRequest(new { Message = "Tipo de solicitaÃ§Ã£o invÃ¡lido. Use: Acesso, Retificacao, Exclusao, Portabilidade ou Oposicao." });

        // Tenta vincular ao usuÃ¡rio cadastrado pelo CPF
        var cpfLimpo = dto.RequesterCpf.Trim();
        var usuario  = await _db.Users.FirstOrDefaultAsync(u => u.Cpf == cpfLimpo && u.IsActive);

        var request = new LgpdRequest
        {
            UserId         = usuario?.Id,
            RequesterName  = dto.RequesterName.Trim(),
            RequesterEmail = dto.RequesterEmail.Trim().ToLowerInvariant(),
            RequesterCpf   = cpfLimpo,
            RequestType    = dto.RequestType,
            Description    = dto.Description?.Trim(),
            Status         = "Recebido",
            CreatedAt      = DateTime.UtcNow,
            Deadline       = DateTime.UtcNow.AddDays(15),
        };

        _db.LgpdRequests.Add(request);
        await _db.SaveChangesAsync();

        // Registra no audit log
        // Email de confirmaÃ§Ã£o para o solicitante
        await _email.SendLgpdConfirmationAsync(
            toEmail:     request.RequesterEmail,
            toName:      request.RequesterName,
            protocol:    request.Id,
            requestType: request.RequestType,
            deadline:    request.Deadline
        );

        // Sanitiza campos de texto livre antes de inserir no HTML do e-mail
        var safeName  = HtmlEncoder.Default.Encode(request.RequesterName);
        var safeEmail = HtmlEncoder.Default.Encode(request.RequesterEmail);
        var safeDesc  = request.Description != null
            ? HtmlEncoder.Default.Encode(request.Description)
            : null;

        // Email de notificaÃ§Ã£o interna
        await _email.SendAnuncioAsync(
            destinatarios: [(PrivacidadeEmail, "softNerd â€” Privacidade")],
            titulo:        $"Nova solicitaÃ§Ã£o LGPD: {request.RequestType}",
            corpo: $"""
                <p>Uma nova solicitaÃ§Ã£o LGPD foi recebida.</p>
                <ul>
                  <li><strong>Protocolo:</strong> {request.Id}</li>
                  <li><strong>Tipo:</strong> {request.RequestType}</li>
                  <li><strong>Solicitante:</strong> {safeName} ({safeEmail})</li>
                  <li><strong>CPF:</strong> {request.RequesterCpf}</li>
                  <li><strong>Prazo:</strong> {request.Deadline:dd/MM/yyyy}</li>
                  {(safeDesc != null ? $"<li><strong>DescriÃ§Ã£o:</strong> {safeDesc}</li>" : "")}
                </ul>
                <p>Acesse o painel admin para responder: <a href="https://benditacoxinha.com.br/admin/lgpd">Admin â€º LGPD</a></p>
                """
        );

        _logger.LogInformation(
            "SolicitaÃ§Ã£o LGPD criada: id={Id} tipo={Tipo} email={Email}",
            request.Id, request.RequestType, request.RequesterEmail);

        return CreatedAtAction(nameof(GetRequest), new { id = request.Id },
            new LgpdRequestReceived
            {
                Protocol = request.Id,
                Deadline = request.Deadline,
                Message  = $"SolicitaÃ§Ã£o recebida. Prazo de resposta: atÃ© {request.Deadline:dd/MM/yyyy}. " +
                           $"Guarde o nÃºmero de protocolo para acompanhar: {request.Id}."
            });
    }

    // =========================================================================
    // PÃšBLICO â€” Consulta de protocolo
    // =========================================================================

    /// <summary>
    /// Permite ao solicitante consultar o status da sua solicitaÃ§Ã£o.
    /// Requer o nÃºmero de protocolo E o e-mail usado na abertura (evita enumeraÃ§Ã£o).
    /// </summary>
    [HttpGet("request/{id}")]
    [AllowAnonymous]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(LgpdRequestResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRequest(string id, [FromQuery] string? email)
    {
        // Sem e-mail de confirmaÃ§Ã£o: retorna 404 genÃ©rico (nÃ£o revela se o protocolo existe)
        if (string.IsNullOrWhiteSpace(email))
            return NotFound(new { Message = "Protocolo nÃ£o encontrado." });

        var req = await _db.LgpdRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        // Valida posse: e-mail deve coincidir com o registrado na abertura
        // Retorna 404 mesmo quando o protocolo existe mas o e-mail nÃ£o bate
        // (evita revelar que o protocolo Ã© vÃ¡lido â€” proteÃ§Ã£o contra enumeraÃ§Ã£o)
        if (req == null || !string.Equals(req.RequesterEmail,
                email.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            return NotFound(new { Message = "Protocolo nÃ£o encontrado." });

        return Ok(new LgpdRequestResponse
        {
            Id            = req.Id,
            RequestType   = req.RequestType,
            Status        = req.Status,
            AdminResponse = req.AdminResponse,
            CreatedAt     = req.CreatedAt,
            Deadline      = req.Deadline,
            RespondedAt   = req.RespondedAt,
        });
    }

    // =========================================================================
    // PÃšBLICO â€” Registro de consentimento de cookies
    // =========================================================================

    /// <summary>
    /// Registra o consentimento ou recusa de cookies pelo visitante.
    /// LGPD Art. 8Â°: consentimento deve ser livre, informado e inequÃ­voco.
    /// </summary>
    [HttpPost("consent")]
    [AllowAnonymous]
    [EnableRateLimiting("api")]
    [ProducesResponseType(201)]
    public async Task<IActionResult> RecordConsent([FromBody] CookieConsentCreate dto)
    {
        // IP resolvido pelo middleware UseForwardedHeaders (Program.cs)
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Extrai userId do JWT, se autenticado
        Guid? userId = null;
        var userIdClaim = User.FindFirst("sub")?.Value
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var parsed))
            userId = parsed;

        var consent = new CookieConsent
        {
            UserId        = userId,
            IpHash        = HashIp(ip),
            UserAgent     = HttpContext.Request.Headers.UserAgent.ToString()[..Math.Min(500,
                            HttpContext.Request.Headers.UserAgent.ToString().Length)],
            Accepted      = dto.Accepted,
            PolicyVersion = "1.0",
            ConsentAt     = DateTime.UtcNow,
        };

        _db.CookieConsents.Add(consent);
        await _db.SaveChangesAsync();

        return Created("", new { Message = dto.Accepted ? "Consentimento registrado." : "Recusa registrada." });
    }

    // =========================================================================
    // ADMIN â€” Listagem de solicitaÃ§Ãµes
    // =========================================================================

    /// <summary>Lista todas as solicitaÃ§Ãµes LGPD. Somente Admin.</summary>
    [HttpGet("requests")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<LgpdRequestAdminDto>), 200)]
    public async Task<IActionResult> ListRequests([FromQuery] string? status)
    {
        var query = _db.LgpdRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        var agora = DateTime.UtcNow;

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new LgpdRequestAdminDto
            {
                Id             = r.Id,
                RequesterName  = r.RequesterName,
                RequesterEmail = r.RequesterEmail,
                RequesterCpf   = r.RequesterCpf,
                RequestType    = r.RequestType,
                Description    = r.Description,
                Status         = r.Status,
                AdminResponse  = r.AdminResponse,
                CreatedAt      = r.CreatedAt,
                Deadline       = r.Deadline,
                RespondedAt    = r.RespondedAt,
                IsOverdue      = agora > r.Deadline && r.Status != "Concluido" && r.Status != "Negado",
                IsUrgent       = (r.Deadline - agora).TotalDays < 3 && r.Status != "Concluido" && r.Status != "Negado",
                TemAnexo       = r.AnexoDados != null,
                AnexoNome      = r.AnexoNome,
            })
            .ToListAsync();

        return Ok(requests);
    }

    // =========================================================================
    // ADMIN â€” Resposta a uma solicitaÃ§Ã£o
    // =========================================================================

    /// <summary>Responde formalmente a uma solicitaÃ§Ã£o LGPD. Somente Admin.</summary>
    [HttpPut("requests/{id}/respond")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(LgpdRequestAdminDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RespondRequest(string id, [FromBody] LgpdAdminResponse dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var statusValidos = new[] { "EmAnalise", "Concluido", "Negado" };
        if (!statusValidos.Contains(dto.Status))
            return BadRequest(new { Message = "Status invÃ¡lido. Use: EmAnalise, Concluido ou Negado." });

        var req = await _db.LgpdRequests.FindAsync(id);
        if (req == null)
            return NotFound(new { Message = "SolicitaÃ§Ã£o nÃ£o encontrada." });

        req.Status        = dto.Status;
        req.AdminResponse = dto.AdminResponse.Trim();
        req.RespondedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (dto.Status is "Concluido" or "Negado")
        {
            await _email.SendLgpdResponseAsync(
                toEmail:     req.RequesterEmail,
                toName:      req.RequesterName,
                protocol:    req.Id,
                requestType: req.RequestType,
                response:    HtmlEncoder.Default.Encode(dto.AdminResponse)
            );
        }

        var agora = DateTime.UtcNow;
        return Ok(new LgpdRequestAdminDto
        {
            Id             = req.Id,
            RequesterName  = req.RequesterName,
            RequesterEmail = req.RequesterEmail,
            RequesterCpf   = req.RequesterCpf,
            RequestType    = req.RequestType,
            Description    = req.Description,
            Status         = req.Status,
            AdminResponse  = req.AdminResponse,
            CreatedAt      = req.CreatedAt,
            Deadline       = req.Deadline,
            RespondedAt    = req.RespondedAt,
            IsOverdue      = agora > req.Deadline && req.Status != "Concluido" && req.Status != "Negado",
            IsUrgent       = (req.Deadline - agora).TotalDays < 3 && req.Status != "Concluido" && req.Status != "Negado",
            TemAnexo       = req.AnexoDados != null,
            AnexoNome      = req.AnexoNome,
        });
    }

    // =========================================================================
    // ADMIN â€” Upload de anexo
    // =========================================================================

    /// <summary>Anexa um arquivo (PDF, imagem, documento) a uma solicitaÃ§Ã£o LGPD. MÃ¡x 10 MB.</summary>
    [HttpPost("requests/{id}/attachment")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UploadAttachment(string id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Message = "Nenhum arquivo enviado." });

        const long maxBytes = 10L * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest(new { Message = "O arquivo nÃ£o pode exceder 10 MB." });

        var extensoesPermitidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".doc", ".txt" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!extensoesPermitidas.Contains(ext))
            return BadRequest(new { Message = "Tipo de arquivo nÃ£o permitido. Use PDF, imagem ou documento Word." });

        var req = await _db.LgpdRequests.FindAsync(id);
        if (req == null)
            return NotFound(new { Message = "SolicitaÃ§Ã£o nÃ£o encontrada." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        req.AnexoNome  = Path.GetFileName(file.FileName);
        req.AnexoDados = ms.ToArray();
        await _db.SaveChangesAsync();

        return Ok(new { AnexoNome = req.AnexoNome, Tamanho = req.AnexoDados.Length });
    }

    // =========================================================================
    // ADMIN â€” Download de anexo
    // =========================================================================

    /// <summary>Faz download do arquivo anexado a uma solicitaÃ§Ã£o LGPD.</summary>
    [HttpGet("requests/{id}/attachment")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadAttachment(string id)
    {
        var req = await _db.LgpdRequests
            .AsNoTracking()
            .Select(r => new { r.Id, r.AnexoNome, r.AnexoDados })
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null || req.AnexoDados == null)
            return NotFound(new { Message = "Nenhum anexo encontrado para esta solicitaÃ§Ã£o." });

        var ext         = Path.GetExtension(req.AnexoNome ?? "").ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf"  => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc"  => "application/msword",
            ".txt"  => "text/plain",
            _       => "application/octet-stream",
        };

        return File(req.AnexoDados, contentType, req.AnexoNome ?? "anexo");
    }

    // =========================================================================
    // ADMIN â€” RelatÃ³rio de dados do titular (para Acesso / Portabilidade)
    // =========================================================================

    /// <summary>
    /// Gera relatÃ³rio estruturado dos dados pessoais do titular para atender
    /// solicitaÃ§Ãµes de Acesso (Art. 18, II) ou Portabilidade (Art. 18, V).
    /// </summary>
    [HttpGet("requests/{id}/relatorio")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GerarRelatorio(string id)
    {
        var lgpdReq = await _db.LgpdRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (lgpdReq == null)
            return NotFound(new { Message = "SolicitaÃ§Ã£o nÃ£o encontrada." });

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Comandas)
            .FirstOrDefaultAsync(u => u.Cpf == lgpdReq.RequesterCpf);

        var geradoEm = DateTime.UtcNow;

        object dadosCadastrais;
        object[] historico;
        object saldos;

        if (user != null)
        {
            dadosCadastrais = new
            {
                nome           = user.Name,
                cpf            = lgpdReq.RequesterCpf,
                email          = user.Email,
                whatsapp       = user.WhatsApp,
                cadastradoEm   = user.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                ultimaAtualizacao = user.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
                consentimento  = user.ConsentAt.HasValue
                    ? user.ConsentAt.Value.ToString("dd/MM/yyyy HH:mm")
                    : "NÃ£o registrado",
                status         = user.IsActive ? "Ativo" : "Inativo",
            };

            var comandasFechadas = user.Comandas
                .Where(c => c.Status == Models.PostgreSQL.ComandaStatus.Fechada)
                .OrderByDescending(c => c.ClosedAt)
                .Take(50)
                .Select(c => new
                {
                    protocolo    = c.Id,
                    abertura     = c.OpenedAt.ToString("dd/MM/yyyy HH:mm"),
                    fechamento   = c.ClosedAt.HasValue ? c.ClosedAt.Value.ToString("dd/MM/yyyy HH:mm") : "-",
                    pagamento    = c.PaymentMethod ?? "-",
                })
                .ToArray();

            historico = (object[])comandasFechadas;

            saldos = new
            {
                pontos           = user.PointsBalance,
                pontosExpiraEm   = user.PointsExpiresAt.HasValue
                    ? user.PointsExpiresAt.Value.ToString("dd/MM/yyyy")
                    : "â€”",
                cashbackReais    = user.BalanceInReais,
            };
        }
        else
        {
            dadosCadastrais = new
            {
                nome     = lgpdReq.RequesterName,
                cpf      = lgpdReq.RequesterCpf,
                email    = lgpdReq.RequesterEmail,
                nota     = "Titular nÃ£o encontrado na base de dados com este CPF.",
            };
            historico = [];
            saldos    = new { nota = "Sem dados cadastrados." };
        }

        return Ok(new
        {
            protocolo       = lgpdReq.Id,
            tipoSolicitacao = lgpdReq.RequestType,
            geradoEm        = geradoEm.ToString("dd/MM/yyyy HH:mm"),
            dadosCadastrais,
            historicoComandasFechadas = historico,
            saldos,
        });
    }

    // â”€â”€ Interno â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>SHA-256 com salt â€” impede ataque de dicionÃ¡rio sobre espaÃ§o IPv4.</summary>
    private string HashIp(string ip)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_ipSalt + ip));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}



