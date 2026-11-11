using Hakari.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hakari.Infrastructure.Dados;

// Mapeamento das entidades para tabelas. Os enums são gravados como texto ("Aberta",
// "LampadaSemResposta"...) para o banco ficar legível, exceto Severidade, que fica como número
// para que ORDER BY severidade siga a ordem Aviso < Alta < Desastre.

public class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("usuarios");
        b.Property(u => u.Nome).HasMaxLength(120).IsRequired();
        b.Property(u => u.Email).HasMaxLength(160).IsRequired();
        b.Property(u => u.Telefone).HasMaxLength(11).IsRequired();
        b.Property(u => u.SenhaHash).HasMaxLength(100).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
        b.Ignore(u => u.Perfil);

        // Herança TPH: cidadãos e técnicos na mesma tabela, separados pela coluna "perfil".
        b.HasDiscriminator<string>("perfil")
            .HasValue<Cidadao>("Cidadao")
            .HasValue<Tecnico>("Tecnico");
    }
}

public class TecnicoConfiguracao : IEntityTypeConfiguration<Tecnico>
{
    public void Configure(EntityTypeBuilder<Tecnico> b) =>
        b.Property(t => t.Matricula).HasMaxLength(20);
}

public class PosteConfiguracao : IEntityTypeConfiguration<Poste>
{
    public void Configure(EntityTypeBuilder<Poste> b)
    {
        b.ToTable("postes");
        b.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
        b.HasIndex(p => p.Codigo).IsUnique();
        b.Property(p => p.Endereco).HasMaxLength(200).IsRequired();
        b.Property(p => p.Bairro).HasMaxLength(80).IsRequired();
        b.HasIndex(p => p.Bairro);

        b.HasMany(p => p.Alertas).WithOne(a => a.Poste).HasForeignKey(a => a.PosteId);
        b.Navigation(p => p.Alertas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class AlertaConfiguracao : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> b)
    {
        b.ToTable("alertas");
        b.Property(a => a.Trigger).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Detalhe).HasMaxLength(200);
        b.Ignore(a => a.AbreOcorrencia);
        // Consulta mais comum: "alertas ativos deste poste".
        b.HasIndex(a => new { a.PosteId, a.Estado });
    }
}

public class OcorrenciaConfiguracao : IEntityTypeConfiguration<Ocorrencia>
{
    public void Configure(EntityTypeBuilder<Ocorrencia> b)
    {
        b.ToTable("ocorrencias");
        b.Property(o => o.Origem).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.Categoria).HasConversion<string>().HasMaxLength(30);
        b.Property(o => o.Descricao).HasMaxLength(500).IsRequired();
        b.Property(o => o.Solucao).HasMaxLength(500);
        b.Ignore(o => o.EstaAtiva);

        b.HasOne(o => o.Poste).WithMany().HasForeignKey(o => o.PosteId);
        b.HasOne(o => o.Alerta).WithMany().HasForeignKey(o => o.AlertaId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(o => o.Responsavel).WithMany().HasForeignKey(o => o.ResponsavelId).OnDelete(DeleteBehavior.SetNull);
        // F8: se o cidadão excluir a conta, a ocorrência fica, mas sem autor.
        b.HasOne(o => o.Autor).WithMany().HasForeignKey(o => o.AutorId).OnDelete(DeleteBehavior.SetNull);

        b.HasMany(o => o.Historico).WithOne(h => h.Ocorrencia).HasForeignKey(h => h.OcorrenciaId);
        b.HasMany(o => o.Apoios).WithOne(a => a.Ocorrencia).HasForeignKey(a => a.OcorrenciaId);
        b.Navigation(o => o.Historico).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(o => o.Apoios).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(o => new { o.PosteId, o.Status });
    }
}

public class HistoricoConfiguracao : IEntityTypeConfiguration<HistoricoOcorrencia>
{
    public void Configure(EntityTypeBuilder<HistoricoOcorrencia> b)
    {
        b.ToTable("historico_ocorrencias");
        b.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(h => h.Descricao).HasMaxLength(600).IsRequired();
    }
}

public class ApoioConfiguracao : IEntityTypeConfiguration<Apoio>
{
    public void Configure(EntityTypeBuilder<Apoio> b)
    {
        b.ToTable("apoios");
        // F8: excluir a conta apaga os apoios do cidadão.
        b.HasOne(a => a.Cidadao).WithMany().HasForeignKey(a => a.CidadaoId).OnDelete(DeleteBehavior.Cascade);
        // Reforço no banco da regra "Você já apoiou esta ocorrência".
        b.HasIndex(a => new { a.OcorrenciaId, a.CidadaoId }).IsUnique();
    }
}

public class LeituraConfiguracao : IEntityTypeConfiguration<Leitura>
{
    public void Configure(EntityTypeBuilder<Leitura> b)
    {
        b.ToTable("leituras");
        b.HasOne(l => l.Poste).WithMany().HasForeignKey(l => l.PosteId);
        // Telemetria cresce rápido: o índice atende "leituras do poste X nas últimas N horas".
        b.HasIndex(l => new { l.PosteId, l.RecebidaEm });
    }
}
