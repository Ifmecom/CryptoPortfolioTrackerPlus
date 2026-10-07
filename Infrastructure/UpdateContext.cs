using CryptoPortfolioTracker.Infrastructure.EntityConfigurations;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Infrastructure
{
  
    public class UpdateContext : DbContext
    {

        /// <summary>
        /// /for design-time migration
        /// </summary>


        //public PortfolioContext() : base()
        //{
        //}

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    optionsBuilder.UseSqlite("Data Source=" + AppConstants.AppDataPath + "\\" + AppConstants.DbName);
        //}


        public UpdateContext(DbContextOptions<UpdateContext> connection) : base(connection) { }

        // Vangnet tegen lege narratieven via Coins.Update() op een losse munt (v1.48-incident) — zie NarrativeGuard.
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
            => NarrativeGuard.Save(this, () => base.SaveChanges(acceptAllChangesOnSuccess));

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            => NarrativeGuard.SaveAsync(this, () => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken));


        public DbSet<Coin> Coins  { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Narrative> Narratives { get; set; }
        public DbSet<Mutation> Mutations { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        public DbSet<PriceLevel> PriceLevels { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfiguration(new CoinEntityTypeConfiguration());
            builder.ApplyConfiguration(new TransactionEntityTypeConfiguration());
            builder.ApplyConfiguration(new AccountEntityTypeConfiguration());
            builder.ApplyConfiguration(new NarrativeEntityTypeConfiguration());
            builder.ApplyConfiguration(new AssetEntityTypeConfiguration());
            builder.ApplyConfiguration(new MutationEntityTypeConfiguration());
            builder.ApplyConfiguration(new PriceLevelEntityTypeConfiguration());

        }

    }
}
