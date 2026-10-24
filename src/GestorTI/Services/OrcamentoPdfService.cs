using System.Globalization;
using Avalonia.Platform;
using GestorTI.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestorTI.Services;

/// <summary>Gera o PDF do orçamento e salva na pasta do cliente (Clientes\&lt;cliente&gt;\Orcamentos).</summary>
public sealed class OrcamentoPdfService
{
    private static readonly CultureInfo Br = new("pt-BR");
    private readonly SettingsService _settings;
    private readonly PastasService _pastas;

    public OrcamentoPdfService(SettingsService settings, PastasService pastas)
    {
        _settings = settings;
        _pastas = pastas;
        QuestPDF.Settings.License = LicenseType.Community; // gratuita para empresas com faturamento até US$ 1 mi/ano
    }

    private static string Moeda(decimal v) => v.ToString("C", Br);

    /// <summary>O orçamento precisa vir com Cliente e Itens carregados.</summary>
    public string Gerar(Orcamento o)
    {
        var s = _settings.Current;
        var pasta = Path.Combine(_pastas.Garantir(o.Cliente), "Orcamentos");
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, $"{o.NumeroTexto} - {PastasService.Sanitizar(o.Titulo)}.pdf");

        byte[]? logo = null;
        try
        {
            using var st = AssetLoader.Open(new Uri("avares://GestorTI/Assets/logo-mark.png"));
            using var ms = new MemoryStream();
            st.CopyTo(ms);
            logo = ms.ToArray();
        }
        catch { /* sem logo: o PDF sai só com o texto */ }

        const string azul = "#0030B9", cinza = "#667085", linha = "#E4E7EC";

        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor("#101828"));

            page.Header().PaddingBottom(14).Row(row =>
            {
                if (logo is not null) row.ConstantItem(46).Height(46).Image(logo);
                row.RelativeItem().PaddingLeft(logo is null ? 0 : 10).Column(c =>
                {
                    c.Item().Text(s.NomeEmpresa).FontSize(16).Bold().FontColor(azul);
                    if (!string.IsNullOrWhiteSpace(s.CnpjPrestador))
                        c.Item().Text("CNPJ " + s.CnpjPrestador).FontSize(9).FontColor(cinza);
                    if (!string.IsNullOrWhiteSpace(s.ContatoEmpresa))
                        c.Item().Text(s.ContatoEmpresa).FontSize(9).FontColor(cinza);
                });
                row.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().Text("ORÇAMENTO").FontSize(9).FontColor(cinza).AlignRight();
                    c.Item().Text(o.NumeroTexto).FontSize(18).Bold().AlignRight();
                    c.Item().Text("Emitido em " + DateTime.Today.ToString("dd/MM/yyyy")).FontSize(9).FontColor(cinza).AlignRight();
                });
            });

            page.Content().Column(col =>
            {
                col.Spacing(14);

                col.Item().Background("#F5F6FA").Padding(12).Column(c =>
                {
                    c.Item().Text("CLIENTE").FontSize(8).FontColor(cinza).Bold();
                    c.Item().Text(o.Cliente.Nome).FontSize(12).Bold();
                    c.Item().Text(o.Cliente.DocumentoFormatado).FontSize(9).FontColor(cinza);
                    if (!string.IsNullOrWhiteSpace(o.Cliente.Contato))
                        c.Item().Text(o.Cliente.Contato).FontSize(9).FontColor(cinza);
                });

                col.Item().Text(o.Titulo).FontSize(14).Bold();

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(6);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.2f);
                    });

                    static IContainer Cab(IContainer c) =>
                        c.BorderBottom(1.5f).BorderColor("#0030B9").PaddingVertical(6);
                    t.Header(h =>
                    {
                        h.Cell().Element(Cab).Text("Descrição").Bold().FontSize(9);
                        h.Cell().Element(Cab).AlignRight().Text("Qtd.").Bold().FontSize(9);
                        h.Cell().Element(Cab).AlignRight().Text("Valor unit.").Bold().FontSize(9);
                        h.Cell().Element(Cab).AlignRight().Text("Total").Bold().FontSize(9);
                    });

                    static IContainer Cel(IContainer c) =>
                        c.BorderBottom(1).BorderColor("#E4E7EC").PaddingVertical(7);
                    foreach (var i in o.Itens)
                    {
                        t.Cell().Element(Cel).Text(i.Descricao);
                        t.Cell().Element(Cel).AlignRight().Text(i.Quantidade.ToString("0.##", Br));
                        t.Cell().Element(Cel).AlignRight().Text(Moeda(i.ValorUnitario));
                        t.Cell().Element(Cel).AlignRight().Text(Moeda(i.Total));
                    }
                });

                col.Item().AlignRight().Background(azul).PaddingVertical(8).PaddingHorizontal(16)
                    .Text("TOTAL  " + Moeda(o.Total)).FontSize(13).Bold().FontColor("#FFFFFF");

                if (!string.IsNullOrWhiteSpace(o.Observacoes))
                    col.Item().Column(c =>
                    {
                        c.Item().Text("Condições e observações").Bold();
                        c.Item().PaddingTop(3).Text(o.Observacoes).FontColor("#344054");
                    });

                col.Item().Text($"Proposta válida até {o.Validade:dd/MM/yyyy}.").FontColor(cinza);
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.DefaultTextStyle(t => t.FontSize(8).FontColor(cinza));
                x.Span($"{s.NomeEmpresa} · {o.NumeroTexto} · Página ");
                x.CurrentPageNumber();
                x.Span(" de ");
                x.TotalPages();
            });
        })).GeneratePdf(arquivo);

        return arquivo;
    }
}
