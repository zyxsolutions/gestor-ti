using GestorTI.Data;
using GestorTI.Services;
using GestorTI.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GestorTI;

/// <summary>Composição de dependências (DI) e serviços de background.</summary>
public static class Startup
{
    public static void Configure(HostBuilderContext _, IServiceCollection s)
    {
        // Banco local SQLite — uma factory, contextos curtos.
        s.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={AppPaths.DbFile}"));

        // Serviços
        s.AddSingleton<SettingsService>();
        s.AddSingleton<DashboardService>();
        s.AddSingleton<CobrancaService>();
        s.AddSingleton<BackupService>();
        s.AddSingleton<PastasService>();
        s.AddSingleton<NfseService>();
        s.AddHttpClient<INfseProvider, FocusNfeProvider>(); // HttpClient gerenciado (pool/DNS) pelo IHttpClientFactory

        // Background (rodam enquanto o app estiver aberto)
        s.AddHostedService<NfsePollingService>();
        s.AddHostedService<BackupHostedService>();
        s.AddHostedService<RotinasService>();

        // ViewModels
        s.AddSingleton<MainWindowViewModel>();
        s.AddTransient<DashboardViewModel>();
        s.AddTransient<ClientesViewModel>();
        s.AddTransient<ContratosViewModel>();
        s.AddTransient<FinanceiroViewModel>();
        s.AddTransient<NotasFiscaisViewModel>();
        s.AddTransient<ConfiguracoesViewModel>();
    }
}
