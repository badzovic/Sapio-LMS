using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;

namespace SIA.Sapio.LMS.Data.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Korisnik> KORISNICI { get; set; }
        public DbSet<Kompanija> KOMPANIJE { get; set; }
        public DbSet<TipKorisnika> TIPOVI_KORISNIKA { get; set; }

        public DbSet<RegistrationToken> REGISTRATION_TOKENS { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Korisnik>(entity =>
            {
                entity.ToTable("KORISNICI");

                entity.HasKey(e => e.ID);

                entity.Property(e => e.ID).HasColumnName("ID");
                entity.Property(e => e.IME).HasColumnName("IME");
                entity.Property(e => e.PREZIME).HasColumnName("PREZIME");
                entity.Property(e => e.ACCESS_KEY).HasColumnName("ACCESS_KEY");
                entity.Property(e => e.USER_ID).HasColumnName("USER_ID");
                entity.Property(e => e.EMAIL).HasColumnName("EMAIL");
                entity.Property(e => e.KOMPANIJA).HasColumnName("KOMPANIJA");
                entity.Property(e => e.HRM_ID).HasColumnName("HRM_ID");
                entity.Property(e => e.AKTIVAN).HasColumnName("AKTIVAN");
                entity.Property(e => e.IME_OCA).HasColumnName("IME_OCA");
                entity.Property(e => e.KOMPANIJA_ID).HasColumnName("KOMPANIJA_ID");

                entity.HasOne(e => e.Kompanija)
                      .WithMany()
                      .HasForeignKey(e => e.KOMPANIJA_ID)
                      .OnDelete(DeleteBehavior.Restrict);
            });
           
        }
    }
}