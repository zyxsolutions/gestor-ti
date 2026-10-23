using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Models;
using GestorTI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GestorTI.ViewModels;

/// <summary>
/// Janela principal: mostra a tela de login até alguém entrar; depois, o sistema com o menu lateral.
/// A navegação respeita o perfil (Configurações só para administradores).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _sp;
    private readonly UpdateService _updates;
    private readonly SessaoService _sessao;
    private bool _jaVerificouAtualizacao;

    public MainWindowViewModel(IServiceProvider sp, UpdateService updates, LoginViewModel login, SessaoService sessao)
    {
        _sp = sp;
        _updates = updates;
        _sessao = sessao;
        Login = login;
        VersaoTexto = "v" + updates.Versao;
        login.LoginConcluido += OnLoginConcluido;
    }

    public LoginViewModel Login { get; }

    // ---- Sessão ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaoAutenticado))]
    private bool _autenticado;

    public bool NaoAutenticado => !Autenticado;

    [ObservableProperty] private string _usuarioNome = "";
    [ObservableProperty] private string _perfilTexto = "";
    [ObservableProperty] private string _iniciais = "";
    [ObservableProperty] private bool _ehAdmin;

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

    // ---- Navegação ----
    [ObservableProperty] private ViewModelBase? _paginaAtual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboard))]
    [NotifyPropertyChangedFor(nameof(IsClientes))]
    [NotifyPropertyChangedFor(nameof(IsContratos))]
    [NotifyPropertyChangedFor(nameof(IsFinanceiro))]
    [NotifyPropertyChangedFor(nameof(IsNotasFiscais))]
    [NotifyPropertyChangedFor(nameof(IsConfiguracoes))]
    [NotifyPropertyChangedFor(nameof(IsConta))]
    private string _paginaAtiva = "";

    public bool IsDashboard => PaginaAtiva == "Dashboard";
    public bool IsClientes => PaginaAtiva == "Clientes";
    public bool IsContratos => PaginaAtiva == "Contratos";
    public bool IsFinanceiro => PaginaAtiva == "Financeiro";
    public bool IsNotasFiscais => PaginaAtiva == "NotasFiscais";
    public bool IsConfiguracoes => PaginaAtiva == "Configuracoes";
    public bool IsConta => PaginaAtiva == "Conta";

    /// <summary>Chamado ao abrir a janela: prepara a tela de login.</summary>
    public Task IniciarAsync() => Login.PrepararAsync();

    private async void OnLoginConcluido(Usuario usuario)
    {
        UsuarioNome = usuario.Nome;
        PerfilTexto = usuario.PerfilTexto;
        Iniciais = usuario.Iniciais;
        EhAdmin = usuario.EhAdministrador;
        Mensagem = null;
        Autenticado = true;

        if (!_jaVerificouAtualizacao)
        {
            _jaVerificouAtualizacao = true;
            _ = VerificarAtualizacaoAsync(); // em segundo plano, não atrasa a abertura
        }
        await NavegarAsync("Dashboard");
    }

    [RelayCommand]
    private async Task SairAsync()
    {
        _sessao.Encerrar();
        Autenticado = false;
        PaginaAtual = null;
        PaginaAtiva = "";
        UsuarioNome = PerfilTexto = Iniciais = "";
        EhAdmin = false;
        Mensagem = null;
        await Login.PrepararAsync();
    }

    [RelayCommand]
    private async Task NavegarAsync(string? destino)
    {
        if (!_sessao.EstaAutenticado) return;

        // Proteção também no servidor da UI: operador não abre Configurações nem digitando o comando.
        if (destino == "Configuracoes" && !_sessao.EhAdministrador) destino = "Dashboard";

        ViewModelBase pagina = destino switch
        {
            "Clientes" => _sp.GetRequiredService<ClientesViewModel>(),
            "Contratos" => _sp.GetRequiredService<ContratosViewModel>(),
            "Financeiro" => _sp.GetRequiredService<FinanceiroViewModel>(),
            "NotasFiscais" => _sp.GetRequiredService<NotasFiscaisViewModel>(),
            "Configuracoes" => _sp.GetRequiredService<ConfiguracoesViewModel>(),
            "Conta" => _sp.GetRequiredService<ContaViewModel>(),
            _ => _sp.GetRequiredService<DashboardViewModel>()
        };

        PaginaAtiva = destino ?? "Dashboard";
        PaginaAtual = pagina;
        await pagina.CarregarAsync();
    }
}
