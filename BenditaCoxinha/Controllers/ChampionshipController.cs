// =============================================================================
// ChampionshipController.cs â€” Endpoints REST de Campeonatos/Torneios
// GET  /api/championship              â†’ lista campeonatos (Planejado/InscriÃ§Ãµes)
// GET  /api/championship/{id}         â†’ detalhes de um campeonato
// GET  /api/championship/{id}/participants â†’ lista participantes
// POST /api/championship              â†’ cria campeonato (Admin)
// POST /api/championship/{id}/register â†’ inscreve usuÃ¡rio logado
// PUT  /api/championship/{id}         â†’ edita campeonato (Admin)
// PUT  /api/championship/{id}/status  â†’ muda status (Admin)
// PUT  /api/championship/{id}/participants/{pid}/placement â†’ define colocaÃ§Ã£o (Admin)
// =============================================================================

using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChampionshipController : ControllerBase
{
    private readonly IChampionshipService _service;
    private readonly ILogger<ChampionshipController> _logger;

    public ChampionshipController(IChampionshipService service, ILogger<ChampionshipController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // LEITURA â€” acessÃ­vel por qualquer pessoa autenticada (ou anÃ´nima para listagem)
    // -------------------------------------------------------------------------

    /// <summary>Lista campeonatos planejados e com inscriÃ§Ãµes abertas (pÃºblico).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ChampionshipDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var list = await _service.GetUpcomingAsync();
        return Ok(list.Select(ToDto));
    }

    /// <summary>Lista TODOS os campeonatos incluindo finalizados (Admin).</summary>
    [HttpGet("admin/all")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<ChampionshipDto>), 200)]
    public async Task<IActionResult> GetAllAdmin([FromQuery] string? search = null)
    {
        var list = await _service.GetAllAsync(search);
        return Ok(list.Select(ToDto));
    }

    /// <summary>Exclui um campeonato (Admin). SÃ³ permite excluir Finalizados ou Cancelados.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            _logger.LogInformation("Campeonato {Id} excluÃ­do pelo admin", id);
            return Ok(new { Message = "Campeonato excluÃ­do." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>Busca um campeonato pelo ID com lista de participantes.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ChampionshipDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ch = await _service.GetByIdAsync(id);
        return ch == null ? NotFound(new { Message = "Campeonato nÃ£o encontrado." }) : Ok(ToDto(ch));
    }

    /// <summary>Lista todos os campeonatos em que o usuÃ¡rio autenticado estÃ¡ inscrito.</summary>
    [HttpGet("my-participations")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<MyParticipationDto>), 200)]
    public async Task<IActionResult> GetMyParticipations()
    {
        var userId       = GetUserId();
        var participations = await _service.GetUserParticipationsAsync(userId);
        return Ok(participations.Select(p => new MyParticipationDto
        {
            ParticipationId  = p.Id,
            ChampionshipId   = p.ChampionshipId,
            ChampionshipName = p.Championship?.Name ?? string.Empty,
            Game             = p.Championship?.Game ?? string.Empty,
            StartDate        = p.Championship?.StartDate ?? default,
            Status           = p.Championship?.Status.ToString() ?? string.Empty,
            EntryFeeInReais  = (p.Championship?.EntryFeeInCents ?? 0) / 100m,
            PlayerNumber     = p.PlayerNumber,
            DeckName         = p.DeckName,
            Placement        = p.Placement,
            RegisteredAt     = p.RegisteredAt,
        }));
    }

    /// <summary>Lista os participantes inscritos em um campeonato.</summary>
    [HttpGet("{id:guid}/participants")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ParticipantDto>), 200)]
    public async Task<IActionResult> GetParticipants(Guid id)
    {
        var participants = await _service.GetParticipantsAsync(id);
        return Ok(participants.Select(p => new ParticipantDto
        {
            Id             = p.Id,
            UserId         = p.UserId,
            UserName       = p.User?.Name ?? string.Empty,
            PlayerNumber   = p.PlayerNumber,
            DeckName       = p.DeckName,
            Placement      = p.Placement,
            RegisteredAt   = p.RegisteredAt
        }));
    }

    // -------------------------------------------------------------------------
    // CRIAÃ‡ÃƒO â€” apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Cria um novo campeonato. Apenas Admin.</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ChampionshipDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateChampionshipRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var adminId = GetUserId();

        var championship = new Championship
        {
            Name                 = request.Name,
            Description          = request.Description,
            Game                 = request.Game,
            StartDate            = request.StartDate,
            EndDate              = request.EndDate,
            RegistrationDeadline = request.RegistrationDeadline,
            MaxParticipants      = request.MaxParticipants,
            EntryFeeInCents      = request.EntryFeeInCents,
            ImageUrl             = request.ImageUrl,
            Status               = ChampionshipStatus.Planejado,
            CreatedByAdminId     = adminId
        };

        var created = await _service.CreateAsync(championship);
        _logger.LogInformation("Campeonato {Id} criado pelo admin {AdminId}", created.Id, adminId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDto(created));
    }

    // -------------------------------------------------------------------------
    // EDIÃ‡ÃƒO â€” apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Atualiza os campos editÃ¡veis de um campeonato. Apenas Admin.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ChampionshipDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChampionshipRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ch = await _service.GetByIdAsync(id);
        if (ch == null)
            return NotFound(new { Message = "Campeonato nÃ£o encontrado." });

        ch.Name                 = request.Name;
        ch.Description          = request.Description;
        ch.Game                 = request.Game;
        ch.StartDate            = request.StartDate;
        ch.EndDate              = request.EndDate;
        ch.RegistrationDeadline = request.RegistrationDeadline;
        ch.MaxParticipants      = request.MaxParticipants;
        ch.EntryFeeInCents      = request.EntryFeeInCents;
        ch.ImageUrl             = request.ImageUrl;

        var updated = await _service.UpdateAsync(ch);
        _logger.LogInformation("Campeonato {Id} editado pelo admin", id);
        return Ok(ToDto(updated));
    }

    // -------------------------------------------------------------------------
    // INSCRIÃ‡ÃƒO â€” qualquer usuÃ¡rio autenticado
    // -------------------------------------------------------------------------

    /// <summary>Inscreve o usuÃ¡rio autenticado em um campeonato.</summary>
    [HttpPost("{id:guid}/register")]
    [Authorize]
    [ProducesResponseType(typeof(ParticipantDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Register(Guid id, [FromBody] RegisterChampionshipRequest? request)
    {
        var userId = GetUserId();

        // Verifica se o campeonato existe e aceita inscriÃ§Ãµes
        var ch = await _service.GetByIdAsync(id);
        if (ch == null)
            return NotFound(new { Message = "Campeonato nÃ£o encontrado." });

        if (ch.Status != ChampionshipStatus.Inscricoes)
            return BadRequest(new { Message = "As inscriÃ§Ãµes para este campeonato nÃ£o estÃ£o abertas." });

        if (ch.RegistrationDeadline.HasValue && ch.RegistrationDeadline.Value < DateTime.UtcNow)
            return BadRequest(new { Message = "O prazo de inscriÃ§Ã£o para este campeonato jÃ¡ encerrou." });

        if (ch.MaxParticipants.HasValue && ch.Participants.Count >= ch.MaxParticipants.Value)
            return BadRequest(new { Message = "O campeonato atingiu o nÃºmero mÃ¡ximo de participantes." });

        ChampionshipParticipant participant;
        try
        {
            participant = await _service.RegisterParticipantAsync(id, userId, request?.DeckName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        _logger.LogInformation("UsuÃ¡rio {UserId} inscrito no campeonato {ChampionshipId}", userId, id);

        return StatusCode(201, new ParticipantDto
        {
            Id           = participant.Id,
            UserId       = participant.UserId,
            UserName     = string.Empty,   // sem eager load aqui
            PlayerNumber = participant.PlayerNumber,
            DeckName     = participant.DeckName,
            RegisteredAt = participant.RegisteredAt
        });
    }

    // -------------------------------------------------------------------------
    // INSCRIÃ‡ÃƒO MANUAL â€” Admin adiciona/remove participante
    // -------------------------------------------------------------------------

    /// <summary>Admin inscreve qualquer usuÃ¡rio em um campeonato.</summary>
    [HttpPost("{id:guid}/admin-register")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ParticipantDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdminRegister(Guid id, [FromBody] AdminRegisterRequest request)
    {
        var ch = await _service.GetByIdAsync(id);
        if (ch == null)
            return NotFound(new { Message = "Campeonato nÃ£o encontrado." });

        if (ch.MaxParticipants.HasValue && ch.Participants.Count >= ch.MaxParticipants.Value)
            return BadRequest(new { Message = "O campeonato atingiu o nÃºmero mÃ¡ximo de participantes." });

        ChampionshipParticipant participant;
        try
        {
            participant = await _service.RegisterParticipantAsync(id, request.UserId, request.DeckName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }

        _logger.LogInformation("Admin inscreveu usuÃ¡rio {UserId} no campeonato {ChampionshipId}", request.UserId, id);

        // Busca o nome do usuÃ¡rio para retornar no DTO
        var user = await _service.GetParticipantsAsync(id);
        var registered = user.FirstOrDefault(p => p.UserId == request.UserId);

        return StatusCode(201, new ParticipantDto
        {
            Id           = participant.Id,
            UserId       = participant.UserId,
            UserName     = registered?.User?.Name ?? string.Empty,
            PlayerNumber = participant.PlayerNumber,
            DeckName     = participant.DeckName,
            RegisteredAt = participant.RegisteredAt
        });
    }

    /// <summary>Admin remove participante de um campeonato.</summary>
    [HttpDelete("{id:guid}/participants/{participantId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId)
    {
        try
        {
            await _service.RemoveParticipantAsync(participantId);
            _logger.LogInformation("Participante {ParticipantId} removido do campeonato {Id}", participantId, id);
            return Ok(new { Message = "Participante removido." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    // -------------------------------------------------------------------------
    // ATUALIZAÃ‡ÃƒO DE STATUS â€” apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Muda o status de um campeonato. Apenas Admin.</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ChampionshipDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        if (!Enum.TryParse<ChampionshipStatus>(request.Status, ignoreCase: true, out var newStatus))
            return BadRequest(new { Message = $"Status invÃ¡lido: '{request.Status}'. Valores vÃ¡lidos: Planejado, Inscricoes, EmAndamento, Finalizado, Cancelado" });

        try
        {
            var updated = await _service.UpdateStatusAsync(id, newStatus);
            _logger.LogInformation("Status do campeonato {Id} alterado para {Status}", id, newStatus);
            return Ok(ToDto(updated));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    // -------------------------------------------------------------------------
    // COLOCAÃ‡ÃƒO FINAL â€” apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Define a colocaÃ§Ã£o final de um participante. Apenas Admin.</summary>
    [HttpPut("{id:guid}/participants/{participantId:guid}/placement")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SetPlacement(Guid id, Guid participantId, [FromBody] SetPlacementRequest request)
    {
        if (request.Placement <= 0)
            return BadRequest(new { Message = "A colocaÃ§Ã£o deve ser maior que zero." });

        await _service.SetPlacementAsync(participantId, request.Placement);
        return Ok(new { Message = $"ColocaÃ§Ã£o {request.Placement}Âº definida com sucesso." });
    }

    // -------------------------------------------------------------------------
    // IMAGEM â€” apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Atualiza a URL da imagem de capa de um campeonato.</summary>
    [HttpPut("{id:guid}/image")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ChampionshipDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetImage(Guid id, [FromBody] SetImageRequest request)
    {
        var ch = await _service.GetByIdAsync(id);
        if (ch == null) return NotFound(new { Message = "Campeonato nÃ£o encontrado." });

        ch.ImageUrl = request.ImageUrl;
        await _service.UpdateAsync(ch);
        return Ok(ToDto(ch));
    }

    // -------------------------------------------------------------------------
    // PRÃ‰-INSCRIÃ‡ÃƒO (landing page, sem login)
    // -------------------------------------------------------------------------

    /// <summary>Registra prÃ©-inscriÃ§Ã£o pÃºblica (nome + WhatsApp) em um campeonato.</summary>
    [HttpPost("{id:guid}/preinscricoes")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PreInscricaoDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddPreInscricao(Guid id, [FromBody] PreInscricaoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ch = await _service.GetByIdAsync(id);
        if (ch == null) return NotFound(new { Message = "Campeonato nÃ£o encontrado." });

        if (ch.Status != ChampionshipStatus.Inscricoes && ch.Status != ChampionshipStatus.Planejado)
            return BadRequest(new { Message = "Este campeonato nÃ£o estÃ¡ aceitando inscriÃ§Ãµes." });

        var pi = await _service.AddPreInscricaoAsync(id, request.Nome, request.WhatsApp);
        _logger.LogInformation("PrÃ©-inscriÃ§Ã£o recebida para campeonato {Id}: {Nome}", id, request.Nome);
        return StatusCode(201, new PreInscricaoDto { Id = pi.Id, Nome = pi.Nome, WhatsApp = pi.WhatsApp, IsListaEspera = pi.IsListaEspera, CreatedAt = pi.CreatedAt });
    }

    /// <summary>Lista prÃ©-inscriÃ§Ãµes de um campeonato (Admin).</summary>
    [HttpGet("{id:guid}/preinscricoes")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<PreInscricaoDto>), 200)]
    public async Task<IActionResult> GetPreInscricoes(Guid id)
    {
        var list = await _service.GetPreInscricoesAsync(id);
        return Ok(list.Select(p => new PreInscricaoDto { Id = p.Id, Nome = p.Nome, WhatsApp = p.WhatsApp, IsListaEspera = p.IsListaEspera, CreatedAt = p.CreatedAt }));
    }

    /// <summary>Remove uma prÃ©-inscriÃ§Ã£o (Admin â€” recusar ou apÃ³s confirmar manualmente).</summary>
    [HttpDelete("{id:guid}/preinscricoes/{preInscricaoId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeletePreInscricao(Guid id, Guid preInscricaoId)
    {
        await _service.DeletePreInscricaoAsync(preInscricaoId);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // PÃ“DIO â€” apenas Admin
    // -------------------------------------------------------------------------

    /// <summary>Salva o pÃ³dio do campeonato como JSON. Apenas Admin.</summary>
    [HttpPatch("{id:guid}/podio")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetPodio(Guid id, [FromBody] SetPodioRequest request)
    {
        try
        {
            await _service.SetPodioAsync(id, request.PodioJson);
            return Ok(new { Message = "PÃ³dio salvo com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token invÃ¡lido: identificador de usuÃ¡rio ausente.");
        return id;
    }

    /// <summary>Mapeia Championship â†’ ChampionshipDto (evita circular reference).</summary>
    private static ChampionshipDto ToDto(Championship ch) => new()
    {
        Id                   = ch.Id,
        Name                 = ch.Name,
        Description          = ch.Description,
        Game                 = ch.Game,
        StartDate            = ch.StartDate,
        EndDate              = ch.EndDate,
        RegistrationDeadline = ch.RegistrationDeadline,
        MaxParticipants      = ch.MaxParticipants,
        EntryFeeInCents      = ch.EntryFeeInCents,
        EntryFeeInReais      = ch.EntryFeeInCents / 100m,
        Status               = ch.Status.ToString(),
        ParticipantCount     = ch.Participants?.Count ?? 0,
        PreInscricaoCount    = ch.PreInscricoes?.Count(p => !p.IsListaEspera)  ?? 0,
        ListaEsperaCount     = ch.PreInscricoes?.Count(p =>  p.IsListaEspera) ?? 0,
        CreatedAt            = ch.CreatedAt,
        ImageUrl             = ch.ImageUrl,
        PodioJson            = ch.PodioJson,
    };
}

// =============================================================================
// DTOs e Request Records â€” definidos no mesmo arquivo para simplicidade
// =============================================================================

/// <summary>Request para atualizar campos editÃ¡veis de um campeonato.</summary>
public class UpdateChampionshipRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string    Name                 { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string?   Description          { get; init; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string    Game                 { get; init; } = string.Empty;

    public DateTime  StartDate            { get; init; }
    public DateTime? EndDate              { get; init; }
    public DateTime? RegistrationDeadline { get; init; }
    public int?      MaxParticipants      { get; init; }
    public int       EntryFeeInCents      { get; init; }
    public string?   ImageUrl             { get; init; }
}

/// <summary>DTO de resposta de campeonato (sem circular reference).</summary>
public class ChampionshipDto
{
    public Guid      Id                   { get; init; }
    public string    Name                 { get; init; } = string.Empty;
    public string?   Description          { get; init; }
    public string    Game                 { get; init; } = string.Empty;
    public DateTime  StartDate            { get; init; }
    public DateTime? EndDate              { get; init; }
    public DateTime? RegistrationDeadline { get; init; }
    public int?      MaxParticipants      { get; init; }
    public int       EntryFeeInCents      { get; init; }
    public decimal   EntryFeeInReais      { get; init; }
    public string    Status               { get; init; } = string.Empty;
    public int       ParticipantCount     { get; init; }
    public int       PreInscricaoCount    { get; init; }
    public int       ListaEsperaCount     { get; init; }
    public DateTime  CreatedAt            { get; init; }
    public string?   ImageUrl             { get; init; }
    public string?   PodioJson            { get; init; }
}

/// <summary>DTO de participaÃ§Ã£o do prÃ³prio usuÃ¡rio (GET /api/championship/my-participations).</summary>
public class MyParticipationDto
{
    public Guid      ParticipationId  { get; init; }
    public Guid      ChampionshipId   { get; init; }
    public string    ChampionshipName { get; init; } = string.Empty;
    public string    Game             { get; init; } = string.Empty;
    public DateTime  StartDate        { get; init; }
    public string    Status           { get; init; } = string.Empty;
    public decimal   EntryFeeInReais  { get; init; }
    public int       PlayerNumber     { get; init; }
    public string?   DeckName         { get; init; }
    public int?      Placement        { get; init; }
    public DateTime  RegisteredAt     { get; init; }
}

/// <summary>DTO de participante em um campeonato.</summary>
public class ParticipantDto
{
    public Guid      Id           { get; init; }
    public Guid      UserId       { get; init; }
    public string    UserName     { get; init; } = string.Empty;
    public int       PlayerNumber { get; init; }
    public string?   DeckName     { get; init; }
    public int?      Placement    { get; init; }
    public DateTime  RegisteredAt { get; init; }
}

/// <summary>Request para criar campeonato.</summary>
public class CreateChampionshipRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string    Name                 { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string?   Description          { get; init; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string    Game                 { get; init; } = string.Empty;

    public DateTime  StartDate            { get; init; }
    public DateTime? EndDate              { get; init; }
    public DateTime? RegistrationDeadline { get; init; }
    public int?      MaxParticipants      { get; init; }
    public int       EntryFeeInCents      { get; init; }
    public string?   ImageUrl             { get; init; }
}

/// <summary>Request para alterar status do campeonato.</summary>
public record UpdateStatusRequest(string Status);

/// <summary>Request para o admin inscrever um usuÃ¡rio especÃ­fico.</summary>
public class AdminRegisterRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public Guid UserId { get; init; }
    public string? DeckName { get; init; }
}

/// <summary>Request para inscriÃ§Ã£o em campeonato (deck Ã© opcional).</summary>
public record RegisterChampionshipRequest(
    [property: System.ComponentModel.DataAnnotations.MaxLength(200)] string? DeckName);

/// <summary>Request para definir colocaÃ§Ã£o de participante.</summary>
public record SetPlacementRequest(int Placement);

/// <summary>Request para definir/atualizar a imagem de capa do campeonato.</summary>
public record SetImageRequest(string? ImageUrl);

/// <summary>Request de prÃ©-inscriÃ§Ã£o pÃºblica (sem login).</summary>
public class PreInscricaoRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string Nome     { get; init; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string WhatsApp { get; init; } = string.Empty;
}

/// <summary>DTO de prÃ©-inscriÃ§Ã£o retornado ao frontend.</summary>
public class PreInscricaoDto
{
    public Guid     Id            { get; init; }
    public string   Nome          { get; init; } = string.Empty;
    public string   WhatsApp      { get; init; } = string.Empty;
    public bool     IsListaEspera { get; init; }
    public DateTime CreatedAt     { get; init; }
}

/// <summary>Request para salvar o pÃ³dio de um campeonato.</summary>
public record SetPodioRequest(string PodioJson);

