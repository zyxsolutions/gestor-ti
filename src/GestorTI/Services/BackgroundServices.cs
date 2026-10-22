using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GestorTI.Services;

/// <summary>
/// Substitui o webhook: a cada minuto reenvia notas pendentes e consulta as "Processando".
/// Se não houver internet, a chamada falha silenciosamente e tenta de novo no próximo ciclo.
/// </summary>
public sealed class NfsePollingService : BackgroundService
{
    private readonly NfseService _nfse;
    private readonly ILogger<NfsePollingService> _log;

    public NfsePollingService(NfseService nfse, ILogger<NfsePollingService> log)
    {
        _nfse = nfse; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            do
            {
                try { await _nfse.SincronizarPendentesAsync(stop); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { _log.LogError(ex, "Falha no ciclo de sincronização de NFS-e"); }
            }
            while (await timer.WaitForNextTickAsync(stop));
        }
        catch (OperationCanceledException) { /* encerrando o app */ }
    }
}

/// <summary>Backup automático: verifica a cada 15 min se o último backup já passou do intervalo configurado.</summary>
public sealed class BackupHostedService : BackgroundService
{
    private readonly BackupService _backup;
    private readonly SettingsService _settings;
    private readonly ILogger<BackupHostedService> _log;

    public BackupHostedService(BackupService backup, SettingsService settings, ILogger<BackupHostedService> log)
    {
        _backup = backup; _settings = settings; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        // Pequena espera para não competir com a abertura da janela.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stop); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        try
        {
            do
            {
                try
                {
                    var s = _settings.Current;
                    var intervalo = TimeSpan.FromHours(Math.Max(1, s.BackupIntervaloHoras));
                    var vencido = s.UltimoBackupUtc is null || DateTime.UtcNow - s.UltimoBackupUtc >= intervalo;
                    if (vencido && !string.IsNullOrWhiteSpace(s.PastaBackup))
                    {
                        var arquivo = await _backup.ExecutarAsync(stop);
                        _log.LogInformation("Backup criado: {Arquivo}", arquivo);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { _log.LogError(ex, "Falha no backup automático"); }
            }
            while (await timer.WaitForNextTickAsync(stop));
        }
        catch (OperationCanceledException) { }
    }
}

/// <summary>Rotinas diárias: gera as cobranças recorrentes dos contratos ativos (idempotente).</summary>
public sealed class RotinasService : BackgroundService
{
    private readonly CobrancaService _cobrancas;
    private readonly ILogger<RotinasService> _log;

    public RotinasService(CobrancaService cobrancas, ILogger<RotinasService> log)
    {
        _cobrancas = cobrancas; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        try
        {
            do
            {
                try
                {
                    var n = await _cobrancas.GerarRecorrentesAsync(DateTime.Today, stop);
                    if (n > 0) _log.LogInformation("{N} cobrança(s) recorrente(s) gerada(s)", n);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { _log.LogError(ex, "Falha ao gerar cobranças recorrentes"); }
            }
            while (await timer.WaitForNextTickAsync(stop));
        }
        catch (OperationCanceledException) { }
    }
}
