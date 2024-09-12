namespace Document_library.DAL
{
    public class DocumentDB(DbContextOptions<DocumentDB> opt) : IdentityDbContext(opt)
    {
        public DbSet<Document> Documents { get; set; }

        public override int SaveChanges()
        {
            SetTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void SetTimestamps()
        {
            // Get all added or modified entities
            var entities = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entityEntry in entities)
            {
                if (entityEntry.State == EntityState.Added)
                {
                    // Set CreatedAt only for newly added entities
                    ((BaseEntity)entityEntry.Entity).CreatedAt = DateTime.Now;
                }

                // Always set UpdatedAt for added or modified entities
                ((BaseEntity)entityEntry.Entity).UpdatedAt = DateTime.Now;
            }
        }
    }   
}
