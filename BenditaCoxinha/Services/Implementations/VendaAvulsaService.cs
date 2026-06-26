using System.Text.Json;
using BenditaCoxinha.Data;
using BenditaCoxinha.DTOs;
using BenditaCoxinha.Models;
using BenditaCoxinha.Models.PostgreSQL;
using BenditaCoxinha.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BenditaCoxinha.Services.Implementations;

public class VendaAvulsaService : IVendaAvulsaService
{
    private static readonly TimeZoneInfo BrazilZone = GetBrazilZone();
    private static TimeZoneInfo GetBrazilZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    private static (DateTime InicioUtc, DateTime FimUtc) DiaBrasil(DateTime? dia = null)
    {
        var agora     = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilZone);
        var dataBr    = dia.HasValue ? dia.Value.Date : agora.Date;
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(dataBr, DateTimeKind.Unspecified), BrazilZone);
        return (inicioUtc, inicioUtc.AddDays(1));
    }

    private readonly AppDbContext              _db;
    private readonly ILogger<VendaAvulsaService> _logger;

    public VendaAvulsaService(AppDbContext db, ILogger<VendaAvulsaService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<VendaAvulsaDto> RegisterAsync(VendaAvulsaRequest request, Guid adminId, string adminName)
    {
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products   = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Produto '{item.ProductId}' não encontrado ou inativo.");

            if (product.StockQuantity < item.Quantity)
                throw new InvalidOperationException(
                    $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity}, solicitado: {item.Quantity}.");
        }

        var vendaItems = new List<VendaAvulsaItem>();
        var total      = 0;

        foreach (var reqItem in request.Items)
        {
            var product = products.First(p => p.Id == reqItem.ProductId);

            var updated = await _db.Products
                .Where(p => p.Id == product.Id && p.StockQuantity >= reqItem.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.StockQuantity, p => p.StockQuantity - reqItem.Quantity));
            if (updated == 0)
                throw new InvalidOperationException($"Estoque insuficiente para '{product.Name}' (venda simultânea detectada).");

            var effectivePrice = product.IsOnPromo ? product.DiscountPriceInCents!.Value : product.PriceInCents;
            var subtotal = effectivePrice * reqItem.Quantity;
            total += subtotal;

            vendaItems.Add(new VendaAvulsaItem
            {
                ProductId        = product.Id,
                ProductName      = product.Name,
                ProductCategory  = product.Category,
                Quantity         = reqItem.Quantity,
                UnitPriceInCents = effectivePrice,
                SubtotalInCents  = subtotal,
                UnitCostInCents  = product.CostPriceInCents,
            });
        }

        var discountInCents = (int)Math.Round(total * request.DiscountPercent / 100.0);
        var finalTotal      = total - discountInCents;

        var secondPm  = string.IsNullOrWhiteSpace(request.SecondPaymentMethod) ? null : request.SecondPaymentMethod;
        var secondAmt = secondPm != null ? request.SecondPaymentAmountInCents : 0;

        if (secondPm != null)
        {
            if (secondAmt <= 0 || secondAmt >= finalTotal)
                throw new InvalidOperationException("Valor do segundo pagamento deve ser positivo e menor que o total.");
            if (secondPm == request.PaymentMethod)
                throw new InvalidOperationException("O segundo método de pagamento não pode ser igual ao principal.");
            if (secondPm is PaymentMethod.Cashback or PaymentMethod.Pontos && !request.UserId.HasValue)
                throw new InvalidOperationException("Cashback e Pontos como segundo pagamento exigem um cliente cadastrado selecionado.");
        }

        var primaryAmt = finalTotal - secondAmt;

        string? clientNameResolved = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim();
        if (clientNameResolved == null && request.UserId.HasValue)
        {
            var usr = await _db.Users.FindAsync(request.UserId.Value);
            clientNameResolved = usr?.Name;
        }

        var pm = request.PaymentMethod;
        if (pm is PaymentMethod.Crediario or PaymentMethod.Pontos or PaymentMethod.Cashback)
        {
            if (!request.UserId.HasValue)
                throw new InvalidOperationException(
                    "Crediário, Pontos e Cashback exigem um cliente cadastrado selecionado.");

            var userId = request.UserId.Value;
            var user   = await _db.Users.FindAsync(userId)
                ?? throw new InvalidOperationException("Cliente não encontrado.");

            if (pm == PaymentMethod.Crediario)
            {
                var crediarioExistente = await _db.Crediarios
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == CrediariosStatus.Aberto);
                var vencimento = DateTime.UtcNow.AddDays(30);

                var novosItens = vendaItems.Select(i => new ItemCrediarioDto
                {
                    ItemName         = i.ProductName,
                    Quantity         = i.Quantity,
                    UnitPriceInReais = i.UnitPriceInCents / 100m,
                    SubtotalInReais  = i.SubtotalInCents  / 100m,
                }).ToList();

                if (crediarioExistente != null)
                {
                    var itensAtuais = string.IsNullOrWhiteSpace(crediarioExistente.ItensJson)
                        ? new List<ItemCrediarioDto>()
                        : JsonSerializer.Deserialize<List<ItemCrediarioDto>>(crediarioExistente.ItensJson)
                          ?? new List<ItemCrediarioDto>();

                    itensAtuais.AddRange(novosItens);
                    crediarioExistente.ItensJson        = JsonSerializer.Serialize(itensAtuais);
                    crediarioExistente.ValorEmCentavos += primaryAmt;
                    crediarioExistente.DataVencimento   = vencimento;
                }
                else
                {
                    _db.Crediarios.Add(new Crediario
                    {
                        UserId           = userId,
                        ComandaId        = null,
                        ValorEmCentavos  = primaryAmt,
                        DataAbertura     = DateTime.UtcNow,
                        DataVencimento   = vencimento,
                        Status           = CrediariosStatus.Aberto,
                        AbertoPorAdminId = adminId,
                        Observacao       = "Venda avulsa no balcão",
                        ItensJson        = JsonSerializer.Serialize(novosItens),
                    });
                }
            }
            else if (pm == PaymentMethod.Pontos)
            {
                if (user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow)
                    throw new InvalidOperationException("Os pontos deste cliente estão expirados.");

                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.PointsBalance >= primaryAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.PointsBalance, u => u.PointsBalance - primaryAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente. Cliente tem {user.PointsBalance} pts, método principal custa {primaryAmt} pts.");
            }
            else if (pm == PaymentMethod.Cashback)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.BalanceInCents >= primaryAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.BalanceInCents, u => u.BalanceInCents - primaryAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo insuficiente. Cliente tem R$ {user.BalanceInCents / 100m:N2}, método principal custa R$ {primaryAmt / 100m:N2}.");
            }

            if (secondPm == PaymentMethod.Cashback)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.BalanceInCents >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.BalanceInCents, u => u.BalanceInCents - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo cashback insuficiente para o segundo pagamento. Disponível: R$ {user.BalanceInCents / 100m:N2}.");
            }
            else if (secondPm == PaymentMethod.Pontos)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.PointsBalance >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.PointsBalance, u => u.PointsBalance - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente para o segundo pagamento. Disponível: {user.PointsBalance} pts.");
            }

            await _db.SaveChangesAsync();
        }
        else if (request.UserId.HasValue)
        {
            var userId = request.UserId.Value;
            var user   = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new InvalidOperationException("Cliente não encontrado.");

            if (secondPm == PaymentMethod.Cashback)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.BalanceInCents >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.BalanceInCents, u => u.BalanceInCents - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo cashback insuficiente para o segundo pagamento. Disponível: R$ {user.BalanceInCents / 100m:N2}.");
            }
            else if (secondPm == PaymentMethod.Pontos)
            {
                var rows = await _db.Users
                    .Where(u => u.Id == userId && u.PointsBalance >= secondAmt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.PointsBalance, u => u.PointsBalance - secondAmt)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                if (rows == 0)
                    throw new InvalidOperationException(
                        $"Saldo de pontos insuficiente para o segundo pagamento. Disponível: {user.PointsBalance} pts.");
            }

            var pontosGanhos = finalTotal / 100;
            if (pontosGanhos > 0)
            {
                var expirado = user.PointsExpiresAt.HasValue && user.PointsExpiresAt.Value < DateTime.UtcNow;
                if (expirado)
                    await _db.Users
                        .Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(u => u.PointsBalance, pontosGanhos)
                            .SetProperty(u => u.PointsExpiresAt, DateTime.UtcNow.AddDays(30))
                            .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
                else
                    await _db.Users
                        .Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(u => u.PointsBalance, u => u.PointsBalance + pontosGanhos)
                            .SetProperty(u => u.PointsExpiresAt, DateTime.UtcNow.AddDays(30))
                            .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
            }
        }

        var venda = new VendaAvulsa
        {
            TotalInCents               = finalTotal,
            DiscountPercent            = request.DiscountPercent,
            DiscountInCents            = discountInCents,
            PaymentMethod              = request.PaymentMethod,
            SecondPaymentMethod        = secondPm,
            SecondPaymentAmountInCents = secondAmt,
            ClientName                 = clientNameResolved,
            UserId                     = request.UserId,
            SoldAt                     = DateTime.UtcNow,
            SoldByAdminId              = adminId,
            SoldByAdminName            = adminName,
            ItensJson                  = JsonSerializer.Serialize(vendaItems),
        };

        _db.VendasAvulsas.Add(venda);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Venda avulsa {Id} registrada por {Admin}: {Count} item(ns), R$ {Total:F2} ({Disc}%)",
            venda.Id, adminName, vendaItems.Count, finalTotal / 100m, request.DiscountPercent);

        return MapToDto(venda);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetRecentAsync(int limit = 50, DateTime? desde = null)
    {
        var query = _db.VendasAvulsas.AsNoTracking();
        if (desde.HasValue)
            query = query.Where(v => v.SoldAt >= desde.Value);

        var vendas = await query
            .OrderByDescending(v => v.SoldAt)
            .Take(limit)
            .ToListAsync();

        return vendas.Select(MapToDto);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetByDateAsync(DateTime? date = null)
    {
        var (inicio, fim) = DiaBrasil(date);

        var vendas = await _db.VendasAvulsas
            .AsNoTracking()
            .Where(v => v.SoldAt >= inicio && v.SoldAt < fim)
            .OrderByDescending(v => v.SoldAt)
            .ToListAsync();

        return vendas.Select(MapToDto);
    }

    public async Task<IEnumerable<VendaAvulsaDto>> GetByUserAsync(Guid userId)
    {
        var vendas = await _db.VendasAvulsas
            .AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.SoldAt)
            .ToListAsync();

        return vendas.Select(MapToDto);
    }

    public async Task<int> BackfillCostsAsync()
    {
        var produtos = await _db.Products
            .Where(p => p.CostPriceInCents > 0)
            .Select(p => new { p.Id, p.CostPriceInCents })
            .ToListAsync();

        var custoMap     = produtos.ToDictionary(p => p.Id, p => p.CostPriceInCents);
        var todasVendas  = await _db.VendasAvulsas.ToListAsync();
        var totalAtualizados = 0;

        foreach (var venda in todasVendas)
        {
            var itens = JsonSerializer.Deserialize<List<VendaAvulsaItem>>(venda.ItensJson) ?? [];
            var modificou = false;

            foreach (var item in itens)
            {
                if (custoMap.TryGetValue(item.ProductId, out var custo) && item.UnitCostInCents != custo)
                {
                    item.UnitCostInCents = custo;
                    totalAtualizados++;
                    modificou = true;
                }
            }

            if (modificou)
                venda.ItensJson = JsonSerializer.Serialize(itens);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("BackfillCosts: {N} item(s) de venda avulsa atualizados.", totalAtualizados);
        return totalAtualizados;
    }

    public async Task<VendaAvulsaDto> EditarPagamentoAsync(string id, EditarPagamentoVendaAvulsaRequest request)
    {
        if (!PaymentMethod.IsValid(request.PaymentMethod))
            throw new ArgumentException($"Forma de pagamento inválida: {request.PaymentMethod}");

        if (request.SecondPaymentMethod != null && !PaymentMethod.IsValid(request.SecondPaymentMethod))
            throw new ArgumentException($"Segundo pagamento inválido: {request.SecondPaymentMethod}");

        if (!Guid.TryParse(id, out var guid))
            throw new ArgumentException("ID inválido.");

        var venda = await _db.VendasAvulsas.FindAsync(guid)
            ?? throw new KeyNotFoundException($"Venda avulsa {id} não encontrada.");

        venda.PaymentMethod              = request.PaymentMethod;
        venda.SecondPaymentMethod        = request.SecondPaymentMethod;
        venda.SecondPaymentAmountInCents = request.SecondPaymentAmountInCents;

        if (request.ClearClientName)
            venda.ClientName = null;
        else if (!string.IsNullOrWhiteSpace(request.ClientName))
            venda.ClientName = request.ClientName.Trim();

        if (request.DiscountInCents.HasValue)
        {
            var originalTotal = venda.TotalInCents + venda.DiscountInCents;
            var newDiscount   = Math.Min(request.DiscountInCents.Value, originalTotal);
            venda.DiscountInCents = newDiscount;
            venda.DiscountPercent = 0;
            venda.TotalInCents    = originalTotal - newDiscount;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Venda avulsa {Id} atualizada: pagamento={PM}, cliente={CN}.",
            id, request.PaymentMethod, venda.ClientName);

        return MapToDto(venda);
    }

    private static VendaAvulsaDto MapToDto(VendaAvulsa v)
    {
        var itens = JsonSerializer.Deserialize<List<VendaAvulsaItem>>(v.ItensJson) ?? [];

        return new VendaAvulsaDto
        {
            Id                         = v.Id.ToString(),
            ClientName                 = v.ClientName,
            PaymentMethod              = v.PaymentMethod,
            SecondPaymentMethod        = v.SecondPaymentMethod,
            SecondPaymentAmountInCents = v.SecondPaymentAmountInCents,
            TotalInReais               = v.TotalInReais,
            DiscountPercent            = v.DiscountPercent,
            DiscountInReais            = v.DiscountInReais,
            SoldAt                     = v.SoldAt,
            SoldByAdminName            = v.SoldByAdminName,
            Items = itens.Select(i => new VendaAvulsaItemDto
            {
                ProductName      = i.ProductName,
                ProductCategory  = i.ProductCategory,
                Quantity         = i.Quantity,
                UnitPriceInReais = i.UnitPriceInCents / 100m,
                SubtotalInReais  = i.SubtotalInCents  / 100m,
                UnitCostInCents  = i.UnitCostInCents,
            }).ToList(),
        };
    }
}
