// =============================================================================
// RelatoriosDtos.cs â€” DTOs para relatÃ³rios de vendas e crediÃ¡rio
// =============================================================================

namespace BenditaCoxinha.DTOs;

// â”€â”€ Vendas por categoria â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public class RelatorioVendasDto
{
    public int     Mes                { get; set; }
    public int     Ano                { get; set; }
    public decimal TotalGeralEmReais  { get; set; }
    public int     TotalItensVendidos { get; set; }
    public List<RelatorioCategoria> PorCategoria { get; set; } = new();
}

public class RelatorioCategoria
{
    public string  Categoria         { get; set; } = string.Empty;
    public string  Emoji             { get; set; } = "ðŸ“¦";
    public int     QuantidadeVendida { get; set; }
    public decimal TotalEmReais      { get; set; }
    public List<RelatorioProduto> Produtos { get; set; } = new();
}

public class RelatorioProduto
{
    public string  Nome              { get; set; } = string.Empty;
    public int     QuantidadeVendida { get; set; }
    public decimal TotalEmReais      { get; set; }
}

// â”€â”€ CrediÃ¡rio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public class RelatorioCrediarioDto
{
    public int     Mes  { get; set; }
    public int     Ano  { get; set; }

    // SituaÃ§Ã£o atual (todos os crediÃ¡rios abertos â€” independe do mÃªs)
    public decimal TotalEmAbertoEmReais { get; set; }
    public decimal TotalVencidoEmReais  { get; set; }
    public int     QtdAbertos           { get; set; }
    public int     QtdVencidos          { get; set; }

    // Movimento do mÃªs selecionado
    public decimal RecebidoNoMesEmReais { get; set; }
    public int     QtdPagamentosNoMes   { get; set; }

    public List<DevedorDto>      Devedores       { get; set; } = new();
    public List<PagamentoMesDto> PagamentosNoMes { get; set; } = new();
}

public class DevedorDto
{
    public Guid     UserId         { get; set; }
    public string   Nome           { get; set; } = string.Empty;
    public string?  Email          { get; set; }
    public string?  WhatsApp       { get; set; }
    public decimal  SaldoEmReais   { get; set; }
    public bool     Vencido        { get; set; }
    public int      DiasAtraso     { get; set; }   // positivo = dias em atraso
    public DateTime DataVencimento { get; set; }
}

public class PagamentoMesDto
{
    public string   ClienteNome    { get; set; } = string.Empty;
    public decimal  ValorEmReais   { get; set; }
    public string   FormaPagamento { get; set; } = string.Empty;
    public string?  Observacao     { get; set; }
    public DateTime CreatedAt      { get; set; }
}

