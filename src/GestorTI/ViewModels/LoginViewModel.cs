using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Models;
using GestorTI.Services;

namespace GestorTI.ViewModels;

/// <summary>
/// Tela de entrada. Tem dois modos:
///  • Login normal (usuário + senha);
///  • Cadastro do administrador — no primeiro acesso (sem usuários) ou no modo de recuperação.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SettingsService _settings;

    public LoginViewModel(AuthService auth, SettingsService settings, UpdateService updates)
    {
        _auth = auth;
        _settings = settings;
        VersaoTexto = "ZYX Core v" + updates.Versao;
    }

    /// <summary>Disparado após entrar com sucesso; a janela principal troca para o sistema.</summary>
    public event Action<Usuario>? LoginConcluido;

    public string VersaoTexto { get; }

    [ObservableProperty] private string _login = "";
    [ObservableProperty] private string _senha = "";
    [ObservableProperty] private string _confirmarSenha = "";
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private bool _mostrarSenha;
    [ObservableProperty] private bool _ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo))]
    [NotifyPropertyChangedFor(nameof(Subtitulo))]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    [NotifyPropertyChangedFor(nameof(ModoLogin))]
    private bool _modoCadastro;

    [ObservableProperty] private bool _modoRecuperacao;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    private string? _erro;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemInfo))]
    private string? _info;

    public bool TemErro => !string.IsNullOrEmpty(Erro);
    public bool TemInfo => !string.IsNullOrEmpty(Info);
    public bool ModoLogin => !ModoCadastro;

    public string Titulo => ModoCadastro ? "Criar administrador" : "Bem-vindo de volta";
    public string Subtitulo => ModoCadastro
        ? "Defina o acesso principal do sistema. Você poderá criar os demais usuários depois, em Configurações."
        : "Entre com seu usuário e senha para acessar o ZYX Core.";
    public string TextoBotao => ModoCadastro ? "Criar conta e entrar" : "Entrar";

    /// <summary>Chamado ao abrir o app e após "Sair": decide o modo e limpa a senha.</summary>
    public async Task PrepararAsync()
    {
        Senha = ConfirmarSenha = "";
        Erro = Info = null;
        MostrarSenha = false;

        try
        {
            var resultado = await _auth.AplicarRedefinicaoExternaAsync();
            if (resultado is not null) Info = resultado;

            ModoCadastro = await _auth.PrecisaConfigurarAdminAsync();
            ModoRecuperacao = _auth.ModoRecuperacao;
        }
        catch (Exception ex)
        {
            ModoCadastro = false;
            Erro = "Não foi possível abrir o banco de dados: " + ex.Message;
        }

        if (ModoLogin) Login = _settings.Current.UltimoUsuario;
    }

    /// <summary>Um único botão (Enter funciona) que age conforme o modo.</summary>
    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (Ocupado) return;
        Erro = Info = null;
        Ocupado = true;
        try
        {
            var usuario = ModoCadastro
                ? await _auth.ConfigurarAdministradorAsync(Nome, Login, Senha, ConfirmarSenha)
                : await _auth.EntrarAsync(Login, Senha);

            Senha = ConfirmarSenha = "";
            LoginConcluido?.Invoke(usuario);
        }
        catch (InvalidOperationException ex) { Erro = ex.Message; Senha = ""; }
        catch (Exception ex) { Erro = "Erro inesperado: " + ex.Message; Senha = ""; }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private void EsqueciSenha()
    {
        Erro = null;
        Info = "Peça a um administrador para redefinir sua senha em Configurações → Usuários. " +
               "Se você é o único administrador, crie um arquivo chamado redefinir-admin.txt na pasta " +
               "%LOCALAPPDATA%\\GestorTI-Dados e reabra o aplicativo. Outra opção: um arquivo redefinir-senha.txt nessa pasta, " +
               "com as linhas usuario=SEU_LOGIN e senha=NOVA_SENHA (o arquivo é apagado após o uso).";
    }
}
