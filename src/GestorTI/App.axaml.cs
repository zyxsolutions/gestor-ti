using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GestorTI.ViewModels;
using GestorTI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GestorTI;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Program.AppHost.Services.GetRequiredService<GestorTI.Services.TemaService>().AplicarSalvo();
            desktop.MainWindow = new MainWindow
            {
                DataContext = Program.AppHost.Services.GetRequiredService<MainWindowViewModel>()
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
