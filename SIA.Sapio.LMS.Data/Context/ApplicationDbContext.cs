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
        public DbSet<VrstaPrograma> VRSTE_PROGRAMA { get; set; }
        public DbSet<Predmet> PREDMETI { get; set; }
        public DbSet<PredmetInstruktor> PREDMET_INSTRUKTORI { get; set; }
        public DbSet<PredmetMaterijal> PREDMET_MATERIJALI { get; set; }
        public DbSet<TipPitanja> TIPOVI_PITANJA { get; set; }
        public DbSet<TipTezinePitanja> TIPOVI_TEZINE_PITANJA { get; set; }
        public DbSet<PredmetPitanje> PREDMET_PITANJA { get; set; }
        public DbSet<PredmetPitanjeOdgovor> PREDMET_PITANJE_ODGOVORI { get; set; }
        public DbSet<Test> TESTOVI { get; set; }
        public DbSet<TestPitanje> TEST_PITANJA { get; set; }
        public DbSet<TestPitanjeOdgovor> TEST_PITANJE_ODGOVORI { get; set; }
        public DbSet<PolaganjeTesta> POLAGANJE_TESTA { get; set; }
        public DbSet<PolaganjePitanja> POLAGANJE_PITANJA { get; set; }
        public DbSet<PolaganjeOdgovori> POLAGANJE_ODGOVORI { get; set; }
        public DbSet<StudentOdgovor> STUDENT_ODGOVORI { get; set; }
        public DbSet<DodijeljeniTest> DODIJELJENI_TESTOVI { get; set; }
        public DbSet<TestKlasa> TEST_KLASE { get; set; }
        public DbSet<AutoDodjelaTestovaRun> AUTO_DODJELA_TESTOVA_RUN { get; set; }
        public DbSet<AutoDodjelaTestovaLog> AUTO_DODJELA_TESTOVA_LOG { get; set; }
        public DbSet<EvidencijaPromjeneTipaKorisnika> EVIDENCIJA_PROMJENA_TIPA_KORISNIKA { get; set; }
        public DbSet<EvidencijaSinhronizacijeKorisnika> EVIDENCIJA_SINHRONIZACIJE_KORISNIKA { get; set; }
        public DbSet<EvidencijaDeaktivacijeKorisnika> EVIDENCIJA_DEAKTIVACIJE_KORISNIKA { get; set; }
        public DbSet<KorisnikPredmet> KORISNIK_PREDMETI { get; set; }

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