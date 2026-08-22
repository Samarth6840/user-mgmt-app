using Microsoft.EntityFrameworkCore;
using UserMgmt.Api.Models;

namespace UserMgmt.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var u = modelBuilder.Entity<User>();

            // Each email address must be unique across the system.
            u.HasIndex(x => x.Email)
                .IsUnique()
                .HasDatabaseName("idx_users_email_unique");

            // Index on last_activity speeds up the default "most recently active" sort.
            u.HasIndex(x => x.LastActivity)
                .HasDatabaseName("idx_users_last_activity");

            // Store the enum as its string value (e.g. "Active") instead of an integer.
            u.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
        }
    }
}
