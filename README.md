# Gestor TI

Sistema desktop (Windows) para gestão de uma operação de TI como MEI: clientes, contratos recorrentes (SLA), cobranças, fluxo de caixa e emissão de NFS-e. Inspirado conceitualmente na usabilidade do Asaas (layout limpo, menu lateral, cartões de indicadores) — sem usar marca, código ou ativos do Asaas.

- **UI:** Avalonia 11 (XAML, tema Fluent) · MVVM com CommunityToolkit.Mvvm
- **Dados:** SQLite local + EF Core 8 (`%LOCALAPPDATA%\GestorTI\gestor.db`)
- **NFS-e:** Focus NFe (Padrão Nacional, `/v2/nfsen`) com fila offline-first e polling
- **Backup:** `.zip` automático (snapshot do SQLite + pasta de notas) para o OneDrive
- **Build:** GitHub Actions → um único `GestorTI.exe` (single-file, ReadyToRun, self-contained) em *Releases*

## Estrutura

```
src/GestorTI/
  Models/        Cliente, Contrato, Cobranca (+LancamentoCaixa), NotaFiscal, Enums
  Data/          AppDbContext (EF Core/SQLite), AppPaths
  Services/      NfseService, FocusNfeProvider (INfseProvider), BackupService,
                 CobrancaService, DashboardService, SettingsService, BackgroundServices
  ViewModels/    uma por tela
  Views/         MainWindow (menu lateral) + telas
.github/workflows/build.yml
```

## Primeiros passos

1. **Rodar em desenvolvimento** (precisa do .NET 8 SDK): `dotnet run --project src/GestorTI`
2. **Configurações** → informe CNPJ, município emissor (IBGE), token da Focus NFe (comece em `homologacao`) e a pasta de backup (OneDrive).
3. **Clientes** → cadastre com endereço completo e código IBGE (exigido pela NFS-e).
4. **Assinaturas** → crie contratos; as cobranças do mês são geradas sozinhas (e há o botão manual).
5. **Cobranças e Extrato** → marque "Receber" (gera a entrada no caixa).
6. **Notas Fiscais** → "Emitir NFS-e" nas cobranças pagas. Cada cliente tem sua pasta (`<PastaNotas>\Clientes\<Nome> - <CPF/CNPJ>`): use "Criar pasta do cliente" e "Importar NF do computador" para guardar PDFs/XMLs; as notas emitidas pela API caem nela automaticamente.

## NFS-e sem webhook (como funciona)

1. A nota é **gravada no SQLite antes** de qualquer chamada de rede, com uma `Referencia` única.
2. Antes de enviar, o app **consulta a referência** no provedor; se já existe, só adota o status → nunca duplica nota, mesmo após queda de internet/energia.
3. Sem internet = a nota fica `Aguardando envio`; nada é perdido.
4. Um serviço de background (`NfsePollingService`) roda a cada 60 s: reenvia pendentes e consulta as `Processando` até virarem `Autorizada`/`Rejeitada` (backoff de 10 min após 10 tentativas).
5. Ao autorizar, baixa **PDF e XML** para a pasta do cliente (`<PastaNotas>\Clientes\<Nome> - <CPF/CNPJ>\`).

> Alternativa a polling: expor um webhook local via túnel (Cloudflare Tunnel / Hookdeck CLI). Não é necessário e adiciona dependência; o polling é suficiente para o volume de um MEI.

**Atenção:** os nomes de campos do payload e do cancelamento estão isolados em `FocusNfeProvider.MontarPayload/CancelarAsync`. Valide tudo em **homologação** (a Focus pode exigir campos extras conforme o município/serviço) antes de usar produção.

## Backup

`BackupService` usa a API de backup online do SQLite (cópia consistente com o app aberto), compacta junto com `NotasFiscais/` e move o `.zip` para a pasta de backup só ao final (o OneDrive nunca vê arquivo incompleto). Mantém os N mais recentes. Para restaurar: feche o app e extraia `gestor.db` para `%LOCALAPPDATA%\GestorTI\`.

## Build e release

Todo push na `main` dispara `.github/workflows/build.yml`: `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true …` e publica `GestorTI.exe` em **Releases** (tag `v1.0.<run>`).

Windows pode exibir o SmartScreen ("editor desconhecido") porque o `.exe` não é assinado; clique em *Mais informações → Executar assim mesmo*.

## Próximos passos sugeridos

- Trocar `EnsureCreated()` por **migrations** do EF Core antes de ter dados reais.
- Seletor de pastas (StorageProvider do Avalonia), ícones (Material.Icons.Avalonia) e gráficos (LiveCharts2).
- Edição de clientes/contratos, reajuste anual (IGPM/IPCA) e relatórios.
- Faturamento MEI: acompanhar o limite anual de receita no dashboard.
