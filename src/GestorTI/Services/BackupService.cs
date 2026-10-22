using System.IO.Compression;
using GestorTI.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GestorTI.Services;

/// <summary>
/// Gera um .zip com (1) um snapshot CONSISTENTE do SQLite e (2) a pasta de notas fiscais,
/// e grava na pasta de backup (ex.: OneDrive — o próprio cliente da nuvem sincroniza).
///
/// Por que não simplesmente copiar o .db? Com o app aberto, o arquivo pode estar no meio de
/// uma escrita. A API de backup online do SQLite (<c>BackupDatabase</c>) faz uma cópia atômica.
///
/// Para restaurar: feche o app, extraia "gestor.db" para %LOCALAPPDATA%\GestorTI\ e a pasta
/// "NotasFiscais" para o local configurado.
/// </summary>
public sealed class BackupService
{
    private readonly SettingsService _settings;
    private readonly ILogger<BackupService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(SettingsService settings, ILogger<BackupService> log)
    {
        _settings = settings; _log = log;
    }

    /// <summary>Executa um backup agora e devolve o caminho do .zip criado.</summary>
    public async Task<string> ExecutarAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cfg = _settings.Current;
            if (string.IsNullOrWhiteSpace(cfg.PastaBackup))
                throw new InvalidOperationException("Defina a pasta de backup em Configurações.");

            Directory.CreateDirectory(cfg.PastaBackup);

            var carimbo = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var dbTemp = Path.Combine(Path.GetTempPath(), $"gestorti-{carimbo}.db");
            var zipTemp = Path.Combine(Path.GetTempPath(), $"gestorti-{carimbo}.zip");
            var zipFinal = Path.Combine(cfg.PastaBackup, $"GestorTI-backup-{carimbo}.zip");

            try
            {
                await Task.Run(() =>
                {
                    SnapshotBanco(dbTemp);
                    CriarZip(dbTemp, cfg.PastaNotas, zipTemp);
                }, ct);

                // Move só no final: o OneDrive nunca enxerga um .zip incompleto.
                File.Move(zipTemp, zipFinal, overwrite: true);
            }
            finally
            {
                TryDelete(dbTemp);
                TryDelete(zipTemp);
            }

            AplicarRetencao(cfg.PastaBackup, Math.Max(1, cfg.BackupRetencao));
            _settings.Update(s => s.UltimoBackupUtc = DateTime.UtcNow);
            _log.LogInformation("Backup concluído: {Zip}", zipFinal);
            return zipFinal;
        }
        finally { _gate.Release(); }
    }

    private static void SnapshotBanco(string destino)
    {
        // Pooling=False: garante que o arquivo temporário seja liberado ao fechar.
        using var origem = new SqliteConnection($"Data Source={AppPaths.DbFile};Pooling=False");
        using var copia = new SqliteConnection($"Data Source={destino};Pooling=False");
        origem.Open();
        copia.Open();
        origem.BackupDatabase(copia);
    }

    private static void CriarZip(string dbSnapshot, string pastaNotas, string zipPath)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(dbSnapshot, "gestor.db", CompressionLevel.Optimal);

        if (!Directory.Exists(pastaNotas)) return;
        foreach (var arquivo in Directory.EnumerateFiles(pastaNotas, "*", SearchOption.AllDirectories))
        {
            var relativo = Path.GetRelativePath(pastaNotas, arquivo).Replace('\\', '/');
            zip.CreateEntryFromFile(arquivo, "NotasFiscais/" + relativo, CompressionLevel.Optimal);
        }
    }

    /// <summary>Mantém apenas os N backups mais recentes (o nome contém a data, então ordenar por nome basta).</summary>
    private void AplicarRetencao(string pasta, int manter)
    {
        var antigos = Directory.GetFiles(pasta, "GestorTI-backup-*.zip")
            .OrderByDescending(Path.GetFileName)
            .Skip(manter);
        foreach (var f in antigos) TryDelete(f);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* melhor esforço */ }
    }
}
