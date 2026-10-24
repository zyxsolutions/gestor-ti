using Microsoft.EntityFrameworkCore;

namespace GestorTI.Data;

/// <summary>
/// Atualização de esquema simples e segura para bancos que já existem.
/// <c>EnsureCreated()</c> cria o banco inteiro na 1ª execução, mas NÃO adiciona tabelas a um banco existente.
/// Cada tabela nova entra aqui com <c>CREATE TABLE IF NOT EXISTS</c> (idempotente, não mexe em dados).
/// Quando o app crescer, trocar por migrations do EF Core.
/// </summary>
public static class SchemaUpdater
{
    public static void Atualizar(AppDbContext db)
    {
        // v1.1 — usuários e login
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"Usuarios\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Usuarios\" PRIMARY KEY AUTOINCREMENT, " +
            "\"Nome\" TEXT NOT NULL, " +
            "\"Login\" TEXT NOT NULL, " +
            "\"SenhaHash\" TEXT NOT NULL, " +
            "\"Perfil\" TEXT NOT NULL, " +
            "\"Ativo\" INTEGER NOT NULL, " +
            "\"CriadoEm\" TEXT NOT NULL, " +
            "\"UltimoAcesso\" TEXT NULL);");
        db.Database.ExecuteSqlRaw(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Usuarios_Login\" ON \"Usuarios\" (\"Login\");");

        // v1.2 — serviços avulsos e orçamentos
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"Servicos\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Servicos\" PRIMARY KEY AUTOINCREMENT, " +
            "\"ClienteId\" INTEGER NOT NULL, " +
            "\"Titulo\" TEXT NOT NULL, " +
            "\"Categoria\" TEXT NOT NULL, " +
            "\"Descricao\" TEXT NULL, " +
            "\"Valor\" TEXT NOT NULL, " +
            "\"DataInicio\" TEXT NOT NULL, " +
            "\"Status\" TEXT NOT NULL, " +
            "\"CobrancaId\" INTEGER NULL, " +
            "\"OrcamentoId\" INTEGER NULL, " +
            "\"CriadoEm\" TEXT NOT NULL, " +
            "CONSTRAINT \"FK_Servicos_Clientes_ClienteId\" FOREIGN KEY (\"ClienteId\") REFERENCES \"Clientes\" (\"Id\") ON DELETE RESTRICT, " +
            "CONSTRAINT \"FK_Servicos_Cobrancas_CobrancaId\" FOREIGN KEY (\"CobrancaId\") REFERENCES \"Cobrancas\" (\"Id\") ON DELETE SET NULL);");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Servicos_ClienteId\" ON \"Servicos\" (\"ClienteId\");");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Servicos_CobrancaId\" ON \"Servicos\" (\"CobrancaId\");");

        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"Orcamentos\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Orcamentos\" PRIMARY KEY AUTOINCREMENT, " +
            "\"Numero\" INTEGER NOT NULL, " +
            "\"ClienteId\" INTEGER NOT NULL, " +
            "\"Titulo\" TEXT NOT NULL, " +
            "\"Observacoes\" TEXT NULL, " +
            "\"Validade\" TEXT NOT NULL, " +
            "\"Status\" TEXT NOT NULL, " +
            "\"CriadoEm\" TEXT NOT NULL, " +
            "\"ServicoId\" INTEGER NULL, " +
            "CONSTRAINT \"FK_Orcamentos_Clientes_ClienteId\" FOREIGN KEY (\"ClienteId\") REFERENCES \"Clientes\" (\"Id\") ON DELETE RESTRICT);");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Orcamentos_Numero\" ON \"Orcamentos\" (\"Numero\");");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Orcamentos_ClienteId\" ON \"Orcamentos\" (\"ClienteId\");");

        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"OrcamentoItens\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_OrcamentoItens\" PRIMARY KEY AUTOINCREMENT, " +
            "\"OrcamentoId\" INTEGER NOT NULL, " +
            "\"Descricao\" TEXT NOT NULL, " +
            "\"Quantidade\" TEXT NOT NULL, " +
            "\"ValorUnitario\" TEXT NOT NULL, " +
            "CONSTRAINT \"FK_OrcamentoItens_Orcamentos_OrcamentoId\" FOREIGN KEY (\"OrcamentoId\") REFERENCES \"Orcamentos\" (\"Id\") ON DELETE CASCADE);");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_OrcamentoItens_OrcamentoId\" ON \"OrcamentoItens\" (\"OrcamentoId\");");
    }
}
