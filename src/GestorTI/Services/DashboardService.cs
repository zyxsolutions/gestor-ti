using System.Globalization;
using GestorTI.Data;
using GestorTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.Services;

public sealed record BarraMes(string Mes, decimal Entradas, decimal Saidas, double AlturaEntrada, double AlturaSaida);

public sealed record ContratoAlerta(string Cliente, string Descricao, DateTime DataFim, int Dias)
{
    public string Texto => Dias < 0 ? $"Venceu há {-Dias} dia(s)" : Dias == 0 ? "Vence hoje" : $"Vence em {Dias} dia(s)";
    public bool Critico => Dias <= 15;
}

public sealed record DashboardDados(
    decimal SaldoCaixa, decimal Mrr,
    decimal InadimplenciaValor, decimal InadimplenciaPercentual,
    decimal AReceberMes, decimal RecebidoMes,
    IReadOnlyList<BarraMes> Barras, IReadOnlyList<ContratoAlerta> ContratosVencendo);

/// <summary>
/// Calcula os indicadores do dashboard.
/// Nota: o provedor SQLite do EF Core não traduz SUM/ORDER BY sobre decimal (grava como TEXT),
/// então os dados são lidos e agregados em memória — para o volume de um MEI isso é instantâneo.
/// </summary>
public sealed class DashboardService
{
    private const double AlturaMaxBarra = 160;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DashboardService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<DashboardDados> ObterAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var hoje = DateTime.Today;
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1);

        var lancamentos = await db.Lancamentos.AsNoTracking().ToListAsync(ct);
        var cobrancas = await db.Cobrancas.AsNoTracking()
            .Where(c => c.Status != StatusCobranca.Cancelada).ToListAsync(ct);
        var contratos = await db.Contratos.AsNoTracking().Include(c => c.Cliente)
            .Where(c => c.Status == StatusContrato.Ativo).ToListAsync(ct);

        // Saldo em caixa: tudo que já aconteceu (até hoje).
        var saldo = lancamentos.Where(l => l.Data.Date <= hoje).Sum(l => l.ValorComSinal);

        // MRR: soma dos contratos ativos normalizados por mês.
        var mrr = contratos.Sum(c => c.ValorMensalizado);

        // Inadimplência: pendentes vencidas ÷ tudo que já deveria ter sido pago.
        var vencidas = cobrancas.Where(c => c.EstaVencida).ToList();
        var valorVencido = vencidas.Sum(c => c.Valor);
        var baseCobrada = cobrancas.Where(c => c.Vencimento.Date <= hoje).Sum(c => c.Valor);
        var pct = baseCobrada > 0 ? valorVencido / baseCobrada * 100m : 0m;

        var aReceber = cobrancas
            .Where(c => c.Status == StatusCobranca.Pendente && c.Vencimento >= inicioMes && c.Vencimento < fimMes)
            .Sum(c => c.Valor);
        var recebido = cobrancas
            .Where(c => c.Status == StatusCobranca.Paga && c.DataPagamento >= inicioMes && c.DataPagamento < fimMes)
            .Sum(c => c.Valor);

        // Gráfico: últimos 6 meses
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        var meses = Enumerable.Range(0, 6).Select(i => inicioMes.AddMonths(i - 5)).ToList();
        var brutas = meses.Select(m =>
        {
            var doMes = lancamentos.Where(l => l.Data.Year == m.Year && l.Data.Month == m.Month).ToList();
            var ent = doMes.Where(l => l.Tipo == TipoLancamento.Entrada).Sum(l => l.Valor);
            var sai = doMes.Where(l => l.Tipo == TipoLancamento.Saida).Sum(l => l.Valor);
            var rotulo = pt.DateTimeFormat.GetAbbreviatedMonthName(m.Month).TrimEnd('.') + "/" + (m.Year % 100).ToString("00");
            return (rotulo, ent, sai);
        }).ToList();

        var maximo = Math.Max(1m, brutas.Max(b => Math.Max(b.ent, b.sai)));
        double Altura(decimal v) => v <= 0 ? 0 : Math.Max(3, (double)(v / maximo) * AlturaMaxBarra);
        var barras = brutas.Select(b => new BarraMes(b.rotulo, b.ent, b.sai, Altura(b.ent), Altura(b.sai))).ToList();

        // Contratos vencendo nos próximos 60 dias (ou já vencidos e ainda ativos).
        var alertas = contratos
            .Where(c => c.DataFim.HasValue && c.DataFim.Value.Date <= hoje.AddDays(60))
            .OrderBy(c => c.DataFim)
            .Select(c => new ContratoAlerta(c.Cliente.Nome, c.Descricao, c.DataFim!.Value, (c.DataFim.Value.Date - hoje).Days))
            .ToList();

        return new DashboardDados(saldo, mrr, valorVencido, pct, aReceber, recebido, barras, alertas);
    }
}
