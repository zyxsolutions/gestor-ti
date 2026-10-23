using System.Globalization;
using Avalonia;
using GestorTI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Velopack;

namespace GestorTI;

public static class Program
{
    /// <summary>Host de DI + serviços de background, vivo durante todo o app.</summary>
    public static IHost AppHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack: precisa ser a PRIMEIRA coisa do app (trata os comandos de instalar/atualizar/desinstalar).
        VelopackApp.Build().Run();

        var pt = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = pt;
        CultureInfo.DefaultThreadCurrentUICulture = pt;
        CultureInfo.CurrentCulture = pt;
        CultureInfo.CurrentUICulture = pt;

        AppPaths.Ensure();
        AppHost = Host.CreateDefaultBuilder(args).ConfigureServices(Startup.Configure).Build();

        // Cria o banco/tabelas na primeira execução.
        // Evolução futura: trocar por migrations (dotnet ef migrations add ...) e db.Database.Migrate().
        var factory = AppHost.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            SchemaUpdater.Atualizar(db); // bancos criados em versões anteriores ganham as tabelas novas
        }

        AppHost.Start(); // inicia polling de NFS-e, backup e rotinas
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppHost.StopAsync().GetAwaiter().GetResult();
            AppHost.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
