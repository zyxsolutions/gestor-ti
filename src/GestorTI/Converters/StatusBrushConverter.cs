using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace GestorTI.Converters;

/// <summary>
/// Converte um texto de status em cor de "badge" (estilo Asaas: fundo suave + texto escuro).
/// ConverterParameter="fg" devolve a cor do texto; sem parâmetro, a cor de fundo.
/// </summary>
public sealed class StatusBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (bg, fg) = Paleta(value?.ToString());
        return new SolidColorBrush(Color.Parse(parameter as string == "fg" ? fg : bg));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static bool Escuro => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    private static (string Bg, string Fg) Paleta(string? s) => Escuro ? PaletaEscura(s) : PaletaClara(s);

    private static (string Bg, string Fg) PaletaEscura(string? s) => s switch
    {
        "Paga" or "Autorizada" or "Ativo" or "Entrada" or "Concluído" or "Aprovado" => ("#143D27", "#6EE7A0"),
        "Pendente" or "Processando" or "Aguardando envio" or "Suspenso" or "Em andamento" or "Enviado" => ("#3A2E0F", "#FFD98A"),
        "Vencida" or "Rejeitada" or "Saída" or "Recusado" => ("#3D1A1A", "#FF9A92"),
        _ => ("#2A3552", "#C3CCE0")
    };

    private static (string Bg, string Fg) PaletaClara(string? s) => s switch
    {
        "Paga" or "Autorizada" or "Ativo" or "Entrada" or "Concluído" or "Aprovado" => ("#E3F6EA", "#1B7F43"),
        "Pendente" or "Processando" or "Aguardando envio" or "Suspenso" or "Em andamento" or "Enviado" => ("#FFF4DB", "#946200"),
        "Vencida" or "Rejeitada" or "Saída" or "Recusado" => ("#FDE7E7", "#B42318"),
        _ => ("#EAECF0", "#475467") // Cancelada, Encerrada, etc.
    };
}
