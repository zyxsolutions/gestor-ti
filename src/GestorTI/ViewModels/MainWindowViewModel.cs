using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace GestorTI.ViewModels;

/// <summary>Navegação do menu lateral: troca a ViewModel exibida na área principal.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _sp;

    public MainWindowViewModel(IServiceProvider sp) => _sp = sp;

    [ObservableProperty] private ViewModelBase? _paginaAtual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboard))]
    [NotifyPropertyChangedFor(nameof(IsClientes))]
    [NotifyPropertyChangedFor(nameof(IsContratos))]
    [NotifyPropertyChangedFor(nameof(IsFinanceiro))]
    [NotifyPropertyChangedFor(nameof(IsNotasFiscais))]
    [NotifyPropertyChangedFor(nameof(IsConfiguracoes))]
    private string _paginaAtiva = "";

    public bool IsDashboard => PaginaAtiva == "Dashboard";
    public bool IsClientes => PaginaAtiva == "Clientes";
    public bool IsContratos => PaginaAtiva == "Contratos";
    public bool IsFinanceiro => PaginaAtiva == "Financeiro";
    public bool IsNotasFiscais => PaginaAtiva == "NotasFiscais";
    public bool IsConfiguracoes => PaginaAtiva == "Configuracoes";

    public Task IniciarAsync() => NavegarAsync("Dashboard");

    [RelayCommand]
    private async Task NavegarAsync(string? destino)
    {
        ViewModelBase pagina = destino switch
        {
            "Clientes" => _sp.GetRequiredService<ClientesViewModel>(),
            "Contratos" => _sp.GetRequiredService<ContratosViewModel>(),
            "Financeiro" => _sp.GetRequiredService<FinanceiroViewModel>(),
            "NotasFiscais" => _sp.GetRequiredService<NotasFiscaisViewModel>(),
            "Configuracoes" => _sp.GetRequiredService<ConfiguracoesViewModel>(),
            _ => _sp.GetRequiredService<DashboardViewModel>()
        };

        PaginaAtiva = destino ?? "Dashboard";
        PaginaAtual = pagina;
        await pagina.CarregarAsync();
    }
}
