using GestorTI.Data;
using GestorTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.Services;

/// <summary>Regras de cobrança: geração recorrente (assinaturas) e recebimento.</summary>
public sealed class CobrancaService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CobrancaService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    /// <summary>
    /// Gera as cobranças do mês de <paramref name="referencia"/> para todos os contratos ativos que
    /// devem cobrar nele. Idempotente: o índice único (Contrato, Competência) impede duplicatas.
    /// </summary>
    public async Task<int> GerarRecorrentesAsync(DateTime referencia, CancellationToken ct = default)
    {
        var competencia = new DateTime(referencia.Year, referencia.Month, 1);
        await using var db = await _factory.CreateDbContextAsync(ct);

        var contratos = await db.Contratos
            .Include(c => c.Cliente)
            .Where(c => c.Status == StatusContrato.Ativo)
            .ToListAsync(ct);

        var jaGeradas = (await db.Cobrancas
                .Where(c => c.ContratoId != null && c.Competencia == competencia)
                .Select(c => c.ContratoId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        var novas = 0;
        foreach (var contrato in contratos)
        {
            if (jaGeradas.Contains(contrato.Id) || !contrato.DeveGerarEm(competencia)) continue;

            db.Cobrancas.Add(new Cobranca
            {
                ClienteId = contrato.ClienteId,
                ContratoId = contrato.Id,
                Competencia = competencia,
                Descricao = $"{contrato.Descricao} — {competencia:MM/yyyy}",
                Valor = contrato.Valor,
                Vencimento = contrato.VencimentoEm(competencia),
                Status = StatusCobranca.Pendente
            });
            novas++;
        }

        if (novas > 0) await db.SaveChangesAsync(ct);
        return novas;
    }

    /// <summary>Marca como paga e lança a entrada no caixa (na mesma transação).</summary>
    public async Task ReceberAsync(int cobrancaId, FormaPagamento forma, DateTime? data = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var cobranca = await db.Cobrancas.FirstOrDefaultAsync(c => c.Id == cobrancaId, ct)
            ?? throw new InvalidOperationException("Cobrança não encontrada.");

        if (cobranca.Status != StatusCobranca.Pendente)
            throw new InvalidOperationException("Somente cobranças pendentes podem ser recebidas.");

        var dia = (data ?? DateTime.Today).Date;
        cobranca.Status = StatusCobranca.Paga;
        cobranca.DataPagamento = dia;
        cobranca.Forma = forma;

        db.Lancamentos.Add(new LancamentoCaixa
        {
            Tipo = TipoLancamento.Entrada,
            Descricao = $"Recebimento: {cobranca.Descricao}",
            Valor = cobranca.Valor,
            Data = dia,
            Categoria = "Recebimento de clientes",
            CobrancaId = cobranca.Id
        });

        await db.SaveChangesAsync(ct); // SaveChanges é transacional: cobrança + lançamento juntos
    }
}
