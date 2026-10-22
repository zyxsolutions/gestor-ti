using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Data;
using GestorTI.Models;
using GestorTI.Services;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.ViewModels;

public partial class NotasFiscaisViewModel : ViewModelBase
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _semProntas = true;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly NfseService _nfse;
    private readonly SettingsService _settings;

    public NotasFiscaisViewModel(IDbContextFactory<AppDbContext> factory, NfseService nfse, SettingsService settings)
    {
        _factory = factory;
        _nfse = nfse;
        _settings = settings;
    }

    /// <summary>Cobranças pagas que ainda não têm NFS-e ativa.</summary>
    public ObservableCollection<Cobranca> ProntasParaEmitir { get; } = new();
    public ObservableCollection<NotaFiscal> Notas { get; } = new();

    public override Task CarregarAsync() => SafeAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var prontas = await db.Cobrancas.AsNoTracking().Include(c => c.Cliente)
            .Where(c => c.Status == StatusCobranca.Paga &&
                        !c.Notas.Any(n => n.Status != StatusNfse.Rejeitada && n.Status != StatusNfse.Cancelada))
            .ToListAsync();

        var notas = await db.Notas.AsNoTracking()
            .Include(n => n.Cobranca).ThenInclude(c => c.Cliente)
            .ToListAsync();

        ProntasParaEmitir.Clear();
        foreach (var c in prontas.OrderBy(c => c.DataPagamento)) ProntasParaEmitir.Add(c);

        SemProntas = ProntasParaEmitir.Count == 0;

        Notas.Clear();
        foreach (var n in notas.OrderByDescending(n => n.CriadaEm)) Notas.Add(n);
    }

    [RelayCommand]
    private Task EmitirAsync(Cobranca? cobranca) => SafeAsync(async () =>
    {
        if (cobranca is null) return;
        var nota = await _nfse.EmitirAsync(cobranca.Id);
        await RecarregarAsync();
        Mensagem = nota.Status switch
        {
            StatusNfse.Autorizada => "NFS-e autorizada e arquivos salvos.",
            StatusNfse.Rejeitada => "NFS-e rejeitada: " + nota.Mensagem,
            StatusNfse.PendenteEnvio => "Nota registrada. " + (nota.Mensagem ?? "Será enviada automaticamente."),
            _ => "NFS-e enviada. O status é atualizado automaticamente a cada minuto."
        };
    });

    [RelayCommand]
    private Task AtualizarAsync(NotaFiscal? nota) => SafeAsync(async () =>
    {
        if (nota is null) return;
        await _nfse.AtualizarAsync(nota.Id);
        await RecarregarAsync();
    });

    [RelayCommand]
    private Task SincronizarAsync() => SafeAsync(async () =>
    {
        var n = await _nfse.SincronizarPendentesAsync(ignorarIntervalo: true);
        await RecarregarAsync();
        Mensagem = n == 0 ? "Nenhuma nota pendente." : $"{n} nota(s) sincronizada(s).";
    });

    [RelayCommand]
    private void AbrirPdf(NotaFiscal? nota) => Abrir(nota?.CaminhoPdf);

    [RelayCommand]
    private void AbrirXml(NotaFiscal? nota) => Abrir(nota?.CaminhoXml);

    [RelayCommand]
    private void AbrirPasta()
    {
        var pasta = _settings.Current.PastaNotas;
        Directory.CreateDirectory(pasta);
        Abrir(pasta);
    }

    private void Abrir(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho)) return;
        try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
        catch (Exception ex) { Mensagem = "Não foi possível abrir: " + ex.Message; }
    }
}
