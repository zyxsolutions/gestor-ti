using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Services;

namespace GestorTI.ViewModels;

public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly BackupService _backup;

    public ConfiguracoesViewModel(SettingsService settings, BackupService backup, UsuariosViewModel usuarios)
    {
        _settings = settings;
        _backup = backup;
        Usuarios = usuarios;
    }

    /// <summary>Aba "Usuários" (criar, editar, excluir).</summary>
    public UsuariosViewModel Usuarios { get; }

    public string[] Ambientes { get; } = { "homologacao", "producao" };

    [ObservableProperty] private string _ambiente = "homologacao";
    [ObservableProperty] private string _token = "";
    [ObservableProperty] private string _cnpjPrestador = "";
    [ObservableProperty] private string _inscricaoMunicipal = "";
    [ObservableProperty] private string _codigoMunicipioEmissor = "";
    [ObservableProperty] private string _codigoTributacaoNacional = "";
    [ObservableProperty] private string _codigoNbs = "";
    [ObservableProperty] private string _pastaNotas = "";
    [ObservableProperty] private string _pastaBackup = "";
    [ObservableProperty] private decimal? _backupIntervaloHoras = 24;
    [ObservableProperty] private decimal? _backupRetencao = 14;
    [ObservableProperty] private string _ultimoBackup = "";

    public override async Task CarregarAsync()
    {
        var s = _settings.Current;
        Ambiente = s.Ambiente;
        Token = _settings.FocusToken;
        CnpjPrestador = s.CnpjPrestador;
        InscricaoMunicipal = s.InscricaoMunicipal;
        CodigoMunicipioEmissor = s.CodigoMunicipioEmissor;
        CodigoTributacaoNacional = s.CodigoTributacaoNacional;
        CodigoNbs = s.CodigoNbs;
        PastaNotas = s.PastaNotas;
        PastaBackup = s.PastaBackup;
        BackupIntervaloHoras = s.BackupIntervaloHoras;
        BackupRetencao = s.BackupRetencao;
        AtualizarUltimoBackup();
        await Usuarios.CarregarAsync();
    }

    private void AtualizarUltimoBackup() =>
        UltimoBackup = _settings.Current.UltimoBackupUtc is { } u
            ? "Último backup: " + u.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Nenhum backup realizado ainda.";

    [RelayCommand]
    private Task SalvarAsync() => SafeAsync(() =>
    {
        _settings.Update(s =>
        {
            s.Ambiente = Ambiente;
            s.CnpjPrestador = CnpjPrestador.Trim();
            s.InscricaoMunicipal = InscricaoMunicipal.Trim();
            s.CodigoMunicipioEmissor = CodigoMunicipioEmissor.Trim();
            s.CodigoTributacaoNacional = CodigoTributacaoNacional.Trim();
            s.CodigoNbs = CodigoNbs.Trim();
            s.PastaNotas = PastaNotas.Trim();
            s.PastaBackup = PastaBackup.Trim();
            s.BackupIntervaloHoras = (int)(BackupIntervaloHoras ?? 24);
            s.BackupRetencao = (int)(BackupRetencao ?? 14);
        });
        _settings.FocusToken = Token.Trim();
        Mensagem = "Configurações salvas.";
        return Task.CompletedTask;
    });

    [RelayCommand]
    private Task BackupAgoraAsync() => SafeAsync(async () =>
    {
        // Salva antes, para o backup usar a pasta que está na tela.
        _settings.Update(s => { s.PastaNotas = PastaNotas.Trim(); s.PastaBackup = PastaBackup.Trim(); });
        var arquivo = await _backup.ExecutarAsync();
        AtualizarUltimoBackup();
        Mensagem = "Backup criado: " + arquivo;
    });
}
