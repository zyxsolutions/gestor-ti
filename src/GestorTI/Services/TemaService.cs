using Avalonia;
using Avalonia.Styling;

namespace GestorTI.Services;

/// <summary>Aplica e guarda o tema (claro/escuro/sistema).</summary>
public sealed class TemaService
{
    private readonly SettingsService _settings;
    public TemaService(SettingsService settings) => _settings = settings;

    public bool Escuro => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public void AplicarSalvo() => Aplicar(_settings.Current.Tema);

    public void Aplicar(string? tema)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = tema switch
        {
            "Escuro" => ThemeVariant.Dark,
            "Claro" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    /// <summary>Alterna claro↔escuro a partir do que está na tela e grava a escolha.</summary>
    public void Alternar()
    {
        var novo = Escuro ? "Claro" : "Escuro";
        _settings.Update(s => s.Tema = novo);
        Aplicar(novo);
    }
}
