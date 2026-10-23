using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Models;
using GestorTI.Services;

namespace GestorTI.ViewModels;

/// <summary>Criar, editar e excluir usuários (aba "Usuários" em Configurações; só administradores).</summary>
public partial class UsuariosViewModel : ViewModelBase
{
    private readonly UsuariosService _svc;
    private readonly SessaoService _sessao;

    public UsuariosViewModel(UsuariosService svc, SessaoService sessao)
    {
        _svc = svc;
        _sessao = sessao;
    }

    public ObservableCollection<Usuario> Usuarios { get; } = new();
    public Array Perfis { get; } = Enum.GetValues(typeof(PerfilUsuario));

    // Formulário
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _login = "";
    [ObservableProperty] private string _senha = "";
    [ObservableProperty] private string _confirmarSenha = "";
    [ObservableProperty] private PerfilUsuario _perfilSelecionado = PerfilUsuario.Operador;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editando))]
    [NotifyPropertyChangedFor(nameof(TituloForm))]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    [NotifyPropertyChangedFor(nameof(RotuloSenha))]
    private Usuario? _usuarioEmEdicao;

    public bool Editando => UsuarioEmEdicao is not null;
    public string TituloForm => Editando ? "Editar usuário" : "Novo usuário";
    public string TextoBotao => Editando ? "Salvar alterações" : "Criar usuário";
    public string RotuloSenha => Editando ? "Nova senha (vazio = manter)" : "Senha (mín. 8 caracteres)";

    // Confirmação de exclusão
    private Func<Task>? _acaoPendente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemConfirmacao))]
    private string _textoConfirmacao = "";

    public bool TemConfirmacao => !string.IsNullOrEmpty(TextoConfirmacao);

    [RelayCommand]
    private Task ConfirmarAsync() => SafeAsync(async () =>
    {
        var acao = _acaoPendente;
        _acaoPendente = null;
        TextoConfirmacao = "";
        if (acao is not null) await acao();
    });

    [RelayCommand]
    private void CancelarConfirmacao()
    {
        _acaoPendente = null;
        TextoConfirmacao = "";
    }

    public override Task CarregarAsync() => SafeAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        var lista = await _svc.ListarAsync();
        Usuarios.Clear();
        foreach (var u in lista) Usuarios.Add(u);
    }

    private void LimparForm()
    {
        UsuarioEmEdicao = null;
        Nome = Login = Senha = ConfirmarSenha = "";
        PerfilSelecionado = PerfilUsuario.Operador;
        Ativo = true;
    }

    [RelayCommand]
    private void EditarUsuario(Usuario? u)
    {
        if (u is null) return;
        Nome = u.Nome;
        Login = u.Login;
        Senha = ConfirmarSenha = "";
        PerfilSelecionado = u.Perfil;
        Ativo = u.Ativo;
        UsuarioEmEdicao = u;
        Mensagem = null;
    }

    [RelayCommand]
    private void CancelarEdicao() => LimparForm();

    [RelayCommand]
    private Task SalvarAsync() => SafeAsync(async () =>
    {
        if (UsuarioEmEdicao is null)
        {
            await _svc.CriarAsync(Nome, Login, Senha, ConfirmarSenha, PerfilSelecionado);
            LimparForm();
            await RecarregarAsync();
            Mensagem = "Usuário criado.";
            return;
        }

        await _svc.AtualizarAsync(UsuarioEmEdicao.Id, Nome, Login, PerfilSelecionado, Ativo, Senha, ConfirmarSenha);
        LimparForm();
        await RecarregarAsync();
        Mensagem = "Usuário atualizado.";
    });

    [RelayCommand]
    private void ExcluirUsuario(Usuario? u)
    {
        if (u is null) return;
        if (_sessao.Atual?.Id == u.Id)
        {
            Mensagem = "Você não pode excluir o seu próprio usuário.";
            return;
        }

        var id = u.Id;
        TextoConfirmacao = $"Excluir o usuário \"{u.Nome}\" ({u.Login})? Esta ação não pode ser desfeita.";
        _acaoPendente = async () =>
        {
            await _svc.ExcluirAsync(id);
            if (UsuarioEmEdicao?.Id == id) LimparForm();
            await RecarregarAsync();
            Mensagem = "Usuário excluído.";
        };
    }
}
