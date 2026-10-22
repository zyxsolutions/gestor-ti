using CommunityToolkit.Mvvm.ComponentModel;

namespace GestorTI.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Mensagem exibida no banner do topo (sucesso ou erro).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    public bool TemMensagem => !string.IsNullOrEmpty(Mensagem);

    /// <summary>Recarrega os dados da tela. Chamado a cada navegação.</summary>
    public virtual Task CarregarAsync() => Task.CompletedTask;

    /// <summary>
    /// Executa uma ação capturando exceções para o banner — evita que erro em comando async
    /// derrube o aplicativo.
    /// </summary>
    protected async Task SafeAsync(Func<Task> acao)
    {
        try
        {
            Mensagem = null;
            await acao();
        }
        catch (InvalidOperationException ex) { Mensagem = ex.Message; }
        catch (Exception ex) { Mensagem = "Erro: " + ex.Message; }
    }
}
