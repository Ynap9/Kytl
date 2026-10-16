using Microsoft.EntityFrameworkCore;
using ktl.be.domain.Documents;

namespace ktl.be.infrastructure.data.Persistence
{
    public class SignTemplateDbContext : DbContext
    {
        public SignTemplateDbContext(DbContextOptions<SignTemplateDbContext> options) : base(options)
        {
        }

        public DbSet<SignTemplate> SignTemplate { get; set; }
        public DbSet<SignTemplatePosition> SignTemplatePosition { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SignTemplate>(entity =>
            {
                entity.Property(e => e.TenTemplate).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Thumbprint).HasMaxLength(100).IsRequired();
                entity.Property(e => e.TenChungThu).HasMaxLength(500);
                entity.Property(e => e.LyDoKy).HasMaxLength(500);
                entity.Property(e => e.NoiKy).HasMaxLength(255);
                entity.Property(e => e.FontChuKySo).HasMaxLength(100);
                entity.Property(e => e.MauChuKySo).HasMaxLength(20);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasMany(e => e.Positions)
                    .WithOne(p => p.SignTemplate)
                    .HasForeignKey(p => p.SignTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SignTemplatePosition>(entity =>
            {
                entity.Property(e => e.PageNumber).HasDefaultValue(1);
                entity.HasIndex(e => new { e.SignTemplateId, e.Kind });
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
