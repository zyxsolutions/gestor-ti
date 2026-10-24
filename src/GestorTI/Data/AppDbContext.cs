using GestorTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.Data;

/// <summary>
/// Contexto EF Core (SQLite). Registrado como IDbContextFactory: cada operação abre e descarta
/// seu próprio contexto — o padrão correto para apps desktop (contexto não é thread-safe e
/// não deve viver o tempo todo da janela).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<Cobranca> Cobrancas => Set<Cobranca>();
    public DbSet<LancamentoCaixa> Lancamentos => Set<LancamentoCaixa>();
    public DbSet<NotaFiscal> Notas => Set<NotaFiscal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<OrcamentoItem> OrcamentoItens => Set<OrcamentoItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.Property(x => x.Perfil).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.Login).IsUnique();
        });

        b.Entity<Servico>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cobranca).WithMany().HasForeignKey(x => x.CobrancaId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Orcamento>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Itens).WithOne().HasForeignKey(i => i.OrcamentoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Cliente>(e =>
        {
            e.HasIndex(x => x.CpfCnpj).IsUnique();
        });

        b.Entity<Contrato>(e =>
        {
            // Enums gravados como texto: o .db fica legível e resiste a reordenação do enum.
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Periodicidade).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

            e.HasOne(x => x.Cliente).WithMany(x => x.Contratos)
                .HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Cobranca>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Forma).HasConversion<string>().HasMaxLength(30);

            e.HasOne(x => x.Cliente).WithMany(x => x.Cobrancas)
                .HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Contrato).WithMany(x => x.Cobrancas)
                .HasForeignKey(x => x.ContratoId).OnDelete(DeleteBehavior.SetNull);

            // Garante que a rotina de geração recorrente é idempotente.
            e.HasIndex(x => new { x.ContratoId, x.Competencia }).IsUnique();
            e.HasIndex(x => x.Vencimento);
            e.HasIndex(x => x.Status);
        });

        b.Entity<LancamentoCaixa>(e =>
        {
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(10);
            e.HasOne(x => x.Cobranca).WithMany()
                .HasForeignKey(x => x.CobrancaId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Data);
        });

        b.Entity<NotaFiscal>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.Referencia).IsUnique();
            e.HasOne(x => x.Cobranca).WithMany(x => x.Notas)
                .HasForeignKey(x => x.CobrancaId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
