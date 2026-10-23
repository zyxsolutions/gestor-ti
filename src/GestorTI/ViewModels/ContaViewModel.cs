using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Services;

namespace GestorTI.ViewModels;

/// <summary>"Minha conta": dados do usuário logado e troca da própria senha (qualquer perfil).</summary>
public partial class ContaViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly SessaoService _sessao;

    public ContaViewModel(AuthService auth, SessaoService sessao)
    {
        _auth = auth;
        _sessao = sessao;
    }

    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _login = "";
    [ObservableProperty] private string _perfil = "";
    [ObservableProperty] private string _senhaAtual = "";
    [ObservableProperty] private string _novaSenha = "";
    [ObservableProperty] private string _confirmarNova = "";

    public override Task CarregarAsync()
    {
        var u = _sessao.Atual;
        Nome = u?.Nome ?? "";
        Login = u?.Login ?? "";
        Perfil = u?.PerfilTexto ?? "";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task AlterarSenhaAsync() => SafeAsync(async () =>
    {
        var u = _sessao.Atual ?? throw new InvalidOperationException("Sessão encerrada. Entre novamente.");
        await _auth.AlterarSenhaAsync(u.Id, SenhaAtual, NovaSenha, ConfirmarNova);
        SenhaAtual = NovaSenha = ConfirmarNova = "";
        Mensagem = "Senha alterada com sucesso.";
    });
}
