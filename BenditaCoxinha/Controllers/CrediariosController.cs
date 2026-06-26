// =============================================================================
// CrediariosController.cs â€” GestÃ£o de crediÃ¡rios
//
// POST /api/crediarios                     â†’ Admin: cria crediÃ¡rio manual (dÃ­vida antiga)
// GET  /api/crediarios                     â†’ Admin: lista todos (filtro por status)
// GET  /api/crediarios/usuario/{userId}    â†’ Admin: crediÃ¡rios de um cliente
// GET  /api/crediarios/meu                 â†’ Cliente: seu crediÃ¡rio ativo
// PUT  /api/crediarios/{id}/pagar          â†’ Admin: quita 100% (legado)
// POST /api/crediarios/{id}/pagamento      â†’ Admin: registra pagamento parcial ou total
// =============================================================================

using System.Text.Json;
using BenditaCoxinha.Data;
using BenditaCoxinha.DTOs;
using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BenditaCoxinha.Controllers;

[ApiController]
[Route("api/crediarios")]
[Authorize]
public class CrediariosController : ControllerBase
{
    private readonly AppDbContext    _db;
    private readonly IEmailService   _email;
    private readonly ILogger<CrediariosController> _logger;

    public CrediariosController(AppDbContext db, IEmailService email, ILogger<CrediariosController> logger)
    {
        _db     = db;
        _email  = email;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /api/crediarios â€” criaÃ§Ã£o manual (dÃ­vidas anteriores ao sistema)
    // -------------------------------------------------------------------------
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CrediariosDto), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<CrediariosDto>> CriarManual([FromBody] CriarCrediarioManualRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Verifica se o cliente existe
        var usuario = await _db.Users.FindAsync(request.UserId);
        if (usuario == null)
            return BadRequest(new { Message = "Cliente nÃ£o encontrado." });

        var adminId    = GetUserId();
        var agora      = DateTime.UtcNow;
        var dataAbert  = request.DataAbertura.HasValue
                             ? request.DataAbertura.Value.ToUniversalTime()
                             : agora;

        // Serializa lista de itens se informada
        string? itensJson = null;
        if (request.Itens != null && request.Itens.Count > 0)
            itensJson = JsonSerializer.Serialize(request.Itens);

        var crediario = new Crediario
        {
            UserId           = request.UserId,
            ComandaId        = null, // dÃ­vida manual â€” sem comanda de origem
            ValorEmCentavos  = request.ValorEmCentavos,
            DataAbertura     = dataAbert,
            DataVencimento   = request.DataVencimento.HasValue
                                   ? request.DataVencimento.Value.ToUniversalTime()
                                   : dataAbert.AddDays(30),
            Status           = CrediariosStatus.Aberto,
            Observacao       = string.IsNullOrWhiteSpace(request.Observacao)
                                   ? "DÃ­vida anterior ao sistema"
                                   : request.Observacao,
            AbertoPorAdminId = adminId,
            ItensJson        = itensJson,
        };

        _db.Crediarios.Add(crediario);
        await _db.SaveChangesAsync();

        // Recarrega com includes para montar o DTO
        var saved = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstAsync(c => c.Id == crediario.Id);

        _logger.LogInformation(
            "CrediÃ¡rio manual {Id} criado pelo admin {AdminId} para usuÃ¡rio {UserId} â€” R$ {Valor:N2}",
            crediario.Id, adminId, request.UserId, request.ValorEmCentavos / 100m);

        return Ok(MapToDto(saved));
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/por-cliente â€” dÃ­vidas abertas agrupadas por pessoa
    // -------------------------------------------------------------------------
    [HttpGet("por-cliente")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<CrediariosClienteDto>>> GetPorCliente()
    {
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .Where(c => c.Status == CrediariosStatus.Aberto)
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

        // Carrega comandas de crediÃ¡rio de todos os usuÃ¡rios para MapToDto conseguir resolver itens
        var userIds        = crediarios.Select(c => c.UserId).Distinct().ToList();
        var comandasPorUser = await CarregarComandasCrediario(userIds);

        var agora = DateTime.UtcNow;
        var grupos = crediarios
            .GroupBy(c => c.UserId)
            .Select(g =>
            {
                var userComandas = comandasPorUser.GetValueOrDefault(g.Key);
                var dividas = g.Select(c => MapToDto(c, userComandas)).ToList();
                var user    = g.First().User;
                return new CrediariosClienteDto
                {
                    UserId          = g.Key,
                    UserName        = user?.Name   ?? string.Empty,
                    UserEmail       = user?.Email,
                    UserWhatsApp    = user?.WhatsApp,
                    SaldoTotal      = dividas.Sum(d => d.SaldoRestanteEmReais),
                    TotalDividas    = dividas.Count,
                    TemVencido      = dividas.Any(d => d.Vencido),
                    ProximoVencimento = g.Min(c => c.DataVencimento),
                    Dividas         = dividas,
                };
            })
            .OrderByDescending(g => g.TemVencido)
            .ThenBy(g => g.ProximoVencimento)
            .ToList();

        return Ok(grupos);
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios?status=Aberto
    // -------------------------------------------------------------------------
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<CrediariosDto>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<CrediariosStatus>(status, ignoreCase: true, out var s))
            query = query.Where(c => c.Status == s);

        var crediarios = await query
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        var userIds = crediarios.Select(c => c.UserId).Distinct().ToList();
        var comandas = await CarregarComandasCrediario(userIds);
        return Ok(crediarios.Select(c => MapToDto(c, comandas.GetValueOrDefault(c.UserId))).ToList());
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/usuario/{userId}
    // -------------------------------------------------------------------------
    [HttpGet("usuario/{userId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<CrediariosDto>>> GetByUser(Guid userId)
    {
        var crediarios = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        var comandas = await CarregarComandasCrediario(new List<Guid> { userId });
        var listaComandas = comandas.GetValueOrDefault(userId);
        return Ok(crediarios.Select(c => MapToDto(c, listaComandas)).ToList());
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/meu â€” crediÃ¡rio aberto do cliente
    // -------------------------------------------------------------------------
    [HttpGet("meu")]
    public async Task<ActionResult<CrediariosDto>> GetMeu()
    {
        var userId    = GetUserId();
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Where(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto)
            .FirstOrDefaultAsync();

        if (crediario == null)
            return NotFound(new { Message = "Nenhum crediÃ¡rio em aberto." });

        var comandas = await CarregarComandasCrediario(new List<Guid> { userId });
        return Ok(MapToDto(crediario, comandas.GetValueOrDefault(userId)));
    }

    // -------------------------------------------------------------------------
    // GET /api/crediarios/historico â€” todo o histÃ³rico de crediÃ¡rios do cliente
    // -------------------------------------------------------------------------
    [HttpGet("historico")]
    public async Task<ActionResult<List<CrediariosDto>>> GetMeuHistorico()
    {
        var userId = GetUserId();
        var lista  = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DataAbertura)
            .ToListAsync();

        var comandas = await CarregarComandasCrediario(new List<Guid> { userId });
        var listaComandas = comandas.GetValueOrDefault(userId);
        return Ok(lista.Select(c => MapToDto(c, listaComandas)).ToList());
    }

    // â”€â”€ Carrega todas as comandas pagas com crediÃ¡rio para uma lista de usuÃ¡rios â”€â”€
    private async Task<Dictionary<Guid, List<Comanda>>> CarregarComandasCrediario(List<Guid> userIds)
    {
        var all = await _db.Comandas
            .Include(c => c.Items)
            .Where(c => c.UserId != null
                     && userIds.Contains(c.UserId.Value)
                     && c.PaymentMethod == "Crediario"
                     && c.Status == ComandaStatus.Fechada
                     && c.ClosedAt != null)
            .ToListAsync();

        return all
            .GroupBy(c => c.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // -------------------------------------------------------------------------
    // PUT /api/crediarios/{id}/pagar
    // -------------------------------------------------------------------------
    [HttpPut("{id:guid}/pagar")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CrediariosDto>> MarcarPago(Guid id, [FromBody] MarcarPagoRequest? request)
    {
        var adminId   = GetUserId();
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "CrediÃ¡rio nÃ£o encontrado." });

        if (crediario.Status == CrediariosStatus.Pago)
            return BadRequest(new { Message = "CrediÃ¡rio jÃ¡ estÃ¡ quitado." });

        // Garante que ValorPago reflita a quitaÃ§Ã£o total
        crediario.ValorPagoEmCentavos = crediario.ValorEmCentavos;
        crediario.Status        = CrediariosStatus.Pago;
        crediario.DataPagamento = DateTime.UtcNow;
        crediario.PagoPorAdminId = adminId;

        if (!string.IsNullOrWhiteSpace(request?.Observacao))
            crediario.Observacao = (crediario.Observacao != null
                ? crediario.Observacao + " | " : "") + request.Observacao;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "CrediÃ¡rio {Id} quitado pelo admin {AdminId} â€” R$ {Valor:N2}",
            id, adminId, crediario.ValorEmReais);

        // Envia email de confirmaÃ§Ã£o (nÃ£o bloqueia)
        if (!string.IsNullOrWhiteSpace(crediario.User?.Email))
            _ = _email.SendCrediarioPagoAsync(
                crediario.User.Email, crediario.User.Name, crediario.ValorEmReais);

        return Ok(MapToDto(crediario));
    }

    // -------------------------------------------------------------------------
    // PATCH /api/crediarios/{id} â€” editar valor, observaÃ§Ã£o ou vencimento
    // -------------------------------------------------------------------------
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CrediariosDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CrediariosDto>> Editar(Guid id, [FromBody] EditarCrediarioRequest request)
    {
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "CrediÃ¡rio nÃ£o encontrado." });

        if (crediario.Status == CrediariosStatus.Pago)
            return BadRequest(new { Message = "NÃ£o Ã© possÃ­vel editar um crediÃ¡rio jÃ¡ quitado." });

        if (request.ValorEmCentavos.HasValue)
        {
            if (request.ValorEmCentavos.Value < crediario.ValorPagoEmCentavos)
                return BadRequest(new
                {
                    Message = $"O novo valor (R$ {request.ValorEmCentavos.Value / 100m:N2}) nÃ£o pode ser menor do que o valor jÃ¡ pago (R$ {crediario.ValorPagoEmCentavos / 100m:N2})."
                });
            crediario.ValorEmCentavos = request.ValorEmCentavos.Value;
        }

        if (request.Observacao != null)
            crediario.Observacao = request.Observacao;

        if (request.DataVencimento.HasValue)
        {
            if (request.DataVencimento.Value.ToUniversalTime().Date < DateTime.UtcNow.Date)
                return BadRequest(new { Message = "A data de vencimento nÃ£o pode ser no passado." });
            crediario.DataVencimento = request.DataVencimento.Value.ToUniversalTime();
        }

        // Itens editados manualmente tÃªm prioridade; caso contrÃ¡rio verifica flag de limpeza
        if (request.Itens != null)
            crediario.ItensJson = request.Itens.Count > 0
                ? JsonSerializer.Serialize(request.Itens)
                : null; // lista vazia = remove itens (deixa cair no date-range)
        else if (request.LimparItens)
            crediario.ItensJson = null;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "CrediÃ¡rio {Id} editado pelo admin {AdminId}", id, GetUserId());

        return Ok(MapToDto(crediario));
    }

    // -------------------------------------------------------------------------
    // POST /api/crediarios/{id}/pagamento
    // -------------------------------------------------------------------------
    [HttpPost("{id:guid}/pagamento")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CrediariosDto>> RegistrarPagamento(
        Guid id, [FromBody] RegistrarPagamentoRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var adminId   = GetUserId();
        var crediario = await _db.Crediarios
            .Include(c => c.User)
            .Include(c => c.Pagamentos)
            .Include(c => c.Comanda).ThenInclude(cmd => cmd!.Items)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "CrediÃ¡rio nÃ£o encontrado." });

        if (crediario.Status == CrediariosStatus.Pago)
            return BadRequest(new { Message = "CrediÃ¡rio jÃ¡ estÃ¡ quitado." });

        var saldoAtual = crediario.SaldoRestanteEmCentavos;
        if (request.ValorEmCentavos > saldoAtual)
            return BadRequest(new
            {
                Message = $"Pagamento de R$ {request.ValorEmCentavos / 100m:N2} excede o saldo restante de R$ {saldoAtual / 100m:N2}."
            });

        // Registra o pagamento parcial (mÃ©todo principal)
        var pagamento = new PagamentoCrediario
        {
            CrediarioId     = id,
            ValorEmCentavos = request.ValorEmCentavos,
            FormaPagamento  = request.FormaPagamento,
            Observacao      = request.Observacao,
            AdminId         = adminId,
        };
        _db.PagamentosCrediario.Add(pagamento);
        crediario.ValorPagoEmCentavos += request.ValorEmCentavos;

        // Segundo mÃ©todo (split) â€” registra como entrada separada
        if (!string.IsNullOrWhiteSpace(request.SecondFormaPagamento) && request.SecondValorEmCentavos > 0)
        {
            var pagamento2 = new PagamentoCrediario
            {
                CrediarioId     = id,
                ValorEmCentavos = request.SecondValorEmCentavos,
                FormaPagamento  = request.SecondFormaPagamento,
                Observacao      = request.Observacao,
                AdminId         = adminId,
            };
            _db.PagamentosCrediario.Add(pagamento2);
            crediario.ValorPagoEmCentavos += request.SecondValorEmCentavos;
        }

        // Quita automaticamente se saldo chegou a zero (tolerÃ¢ncia de 1 centavo para arredondamentos)
        if (crediario.SaldoRestanteEmCentavos <= 1)
        {
            crediario.Status         = CrediariosStatus.Pago;
            crediario.DataPagamento  = DateTime.UtcNow;
            crediario.PagoPorAdminId = adminId;

            _logger.LogInformation(
                "CrediÃ¡rio {Id} quitado via pagamento parcial pelo admin {AdminId} â€” R$ {Valor:N2}",
                id, adminId, crediario.ValorEmReais);

            if (!string.IsNullOrWhiteSpace(crediario.User?.Email))
                _ = _email.SendCrediarioPagoAsync(
                    crediario.User.Email, crediario.User.Name, crediario.ValorEmReais);
        }
        else
        {
            _logger.LogInformation(
                "CrediÃ¡rio {Id}: pagamento parcial de R$ {Valor:N2} registrado pelo admin {AdminId}. Saldo restante: R$ {Saldo:N2}",
                id, request.ValorEmCentavos / 100m, adminId, crediario.SaldoRestanteEmReais);
        }

        await _db.SaveChangesAsync();
        return Ok(MapToDto(crediario));
    }

    // -------------------------------------------------------------------------
    // DELETE /api/crediarios/{id}
    // -------------------------------------------------------------------------
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deletar(Guid id)
    {
        var crediario = await _db.Crediarios
            .Include(c => c.Pagamentos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (crediario == null)
            return NotFound(new { Message = "CrediÃ¡rio nÃ£o encontrado." });

        // Impede deleÃ§Ã£o se hÃ¡ qualquer pagamento registrado.
        // Um crediÃ¡rio com pagamento representa dinheiro jÃ¡ recebido â€” apagÃ¡-lo
        // removeria o histÃ³rico financeiro sem desfazer a receita original.
        if (crediario.ValorPagoEmCentavos > 0)
            return BadRequest(new
            {
                Message = $"NÃ£o Ã© possÃ­vel excluir este crediÃ¡rio pois jÃ¡ possui R$ {crediario.ValorPagoEmCentavos / 100m:N2} registrados como pagos. " +
                          "Exclua apenas crediÃ¡rios sem nenhum pagamento registrado."
            });

        _db.Crediarios.Remove(crediario);
        await _db.SaveChangesAsync();

        _logger.LogInformation("CrediÃ¡rio {Id} excluÃ­do pelo admin {AdminId}", id, GetUserId());
        return NoContent();
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // todasComandas: todas as comandas com PaymentMethod=Crediario deste usuÃ¡rio,
    // filtradas aqui pelo perÃ­odo do crediÃ¡rio â€” cobre histÃ³rico e novos acÃºmulos.
    private static CrediariosDto MapToDto(Crediario c, List<Comanda>? todasComandas = null)
    {
        var agora   = DateTime.UtcNow;
        var vencido = c.Status == CrediariosStatus.Aberto && c.DataVencimento < agora;
        var dias    = (int)Math.Round((c.DataVencimento - agora).TotalDays);

        // Se ItensJson tem dados (acumulaÃ§Ã£o manual ou multi-comanda), usa exclusivamente esses.
        // Isso evita duplicatas quando items foram migrados para ItensJson durante acumulaÃ§Ã£o.
        // Caso contrÃ¡rio, busca pelos itens via ComandaId ou date-range (dados legados).
        var fromJson = string.IsNullOrWhiteSpace(c.ItensJson)
            ? new List<ItemCrediarioDto>()
            : JsonSerializer.Deserialize<List<ItemCrediarioDto>>(c.ItensJson)
              ?? new List<ItemCrediarioDto>();

        List<ItemCrediarioDto> fromComanda;
        if (fromJson.Count > 0)
        {
            // ItensJson definido manualmente â€” nÃ£o faz lookup adicional
            fromComanda = new List<ItemCrediarioDto>();
        }
        else if (c.ComandaId != null && todasComandas != null)
        {
            // CrediÃ¡rio originado de comanda: busca itens pelo perÃ­odo
            var inicio = c.DataAbertura.AddSeconds(-60);
            var fim    = c.DataPagamento.HasValue ? c.DataPagamento.Value.AddDays(1) : DateTime.MaxValue;
            fromComanda = todasComandas
                .Where(cmd => cmd.ClosedAt.HasValue
                           && cmd.ClosedAt.Value >= inicio
                           && cmd.ClosedAt.Value <= fim)
                .SelectMany(cmd => cmd.Items)
                .OrderBy(i => i.AddedAt)
                .Select(i => new ItemCrediarioDto
                {
                    ItemName         = i.ItemNameSnapshot,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInCents / 100m,
                    SubtotalInReais  = i.SubtotalInCents  / 100m,
                })
                .ToList();
        }
        else if (c.ComandaId != null)
        {
            fromComanda = c.Comanda?.Items
                .OrderBy(i => i.AddedAt)
                .Select(i => new ItemCrediarioDto
                {
                    ItemName         = i.ItemNameSnapshot,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInCents / 100m,
                    SubtotalInReais  = i.SubtotalInCents  / 100m,
                })
                .ToList() ?? new List<ItemCrediarioDto>();
        }
        else
        {
            // CrediÃ¡rio manual (ComandaId = null, ItensJson = null) â€” sem itens atÃ© admin adicionar
            fromComanda = new List<ItemCrediarioDto>();
        }

        var todosItens = fromComanda.Concat(fromJson).ToList();

        return new CrediariosDto
        {
            Id                   = c.Id,
            UserId               = c.UserId,
            UserName             = c.User?.Name ?? string.Empty,
            UserEmail            = c.User?.Email,
            ComandaId            = c.ComandaId,
            ValorEmReais         = c.ValorEmReais,
            ValorPagoEmReais     = c.ValorPagoEmReais,
            SaldoRestanteEmReais = c.SaldoRestanteEmReais,
            DataAbertura         = c.DataAbertura,
            DataVencimento       = c.DataVencimento,
            DataPagamento        = c.DataPagamento,
            Status               = vencido ? "Vencido" : c.Status.ToString(),
            Observacao           = c.Observacao,
            Vencido              = vencido,
            DiasRestantes        = dias,
            Pagamentos           = c.Pagamentos
                .OrderBy(p => p.CreatedAt)
                .Select(p => new PagamentoCrediarioDto
                {
                    Id             = p.Id,
                    ValorEmReais   = p.ValorEmReais,
                    FormaPagamento = p.FormaPagamento,
                    Observacao     = p.Observacao,
                    CreatedAt      = p.CreatedAt,
                }).ToList(),
            ItensComanda = todosItens,
        };
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token invÃ¡lido: identificador de usuÃ¡rio ausente.");
        return id;
    }
}

