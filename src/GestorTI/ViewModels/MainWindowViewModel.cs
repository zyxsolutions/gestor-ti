using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GestorTI.ViewModels;

/// <summary>Navegação do menu lateral: troca a ViewModel exibida na área principal.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _sp;

    private readonly UpdateService _updates;

    public MainWindowViewModel(IServiceProvider sp, UpdateService updates)
    {
        _sp = sp;
        _updates = updates;
        VersaoTexto = "v" + updates.Versao;
    }

    // ---- Atualização automática ----
    [ObservableProperty] private string _versaoTexto = "";
    [ObservableProperty] private bool _atualizacaoDisponivel;
    [ObservableProperty] private string _textoAtualizacao = "";
    [ObservableProperty] private bool _atualizando;

    private async Task VerificarAtualizacaoAsync()
    {
        try
        {
            var nova = await _updates.VerificarAsync();
            if (nova is null) return;
            TextoAtualizacao = $"Atualizar para v{nova.TargetFullRelease.Version}";
            AtualizacaoDisponivel = true;
        }
        catch { /* sem internet ou GitHub fora do ar: tenta de novo na próxima abertura */ }
    }

    [RelayCommand]
    private async Task AtualizarAppAsync()
    {
        try
        {
            Atualizando = true;
            TextoAtualizacao = "Baixando atualização…";
            await _updates.InstalarAsync(p => TextoAtualizacao = $"Baixando atualização… {p}%");
            // Se chegou aqui o app já está reiniciando.
        }
        catch (Exception ex)
        {
            Atualizando = false;
            TextoAtualizacao = "Falha ao atualizar";
            Mensagem = "Não foi possível atualizar: " + ex.Message;
        }
    }

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

    public async Task IniciarAsync()
    {
        _ = VerificarAtualizacaoAsync(); // em segundo plano, não atrasa a abertura
        await NavegarAsync("Dashboard");
    }

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
