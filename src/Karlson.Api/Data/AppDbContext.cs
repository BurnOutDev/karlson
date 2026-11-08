using Karlson.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Karlson.Api.Data;

/// <summary>
/// Owns the "app" schema. Also maps the ingestion service's "catalog" schema read-only:
/// those tables and views are excluded from this context's migrations.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const string Schema = "app";

    /// <summary>Local docker-compose database (see the ingestion repo); override with ConnectionStrings__App.</summary>
    public const string DevConnectionString =
        "Host=localhost;Port=5432;Database=karlson;Username=karlson_app;Password=app_dev";

    public DbSet<Household> Households => Set<Household>();
    public DbSet<Member> Members => Set<Member>();

    public DbSet<CatalogIngredient> Ingredients => Set<CatalogIngredient>();
    public DbSet<CatalogStore> Stores => Set<CatalogStore>();
    public DbSet<CurrentStoreProduct> CurrentProducts => Set<CurrentStoreProduct>();
    public DbSet<ProductIngredientLink> ProductIngredients => Set<ProductIngredientLink>();

    public static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", Schema))
            .UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<Household>(e =>
        {
            e.ToTable("households");
            // Ids are assigned by the domain, never by the store; otherwise EF treats a new
            // member discovered through the Members collection as an update.
            e.Property(h => h.Id).ValueGeneratedNever();
            e.Property(h => h.Name).HasMaxLength(60);
            e.Property(h => h.InviteCode).HasMaxLength(12);
            e.HasIndex(h => h.InviteCode).IsUnique();
            e.Property(h => h.WeeklyBudgetGel).HasPrecision(10, 2);
            e.HasMany(h => h.Members).WithOne().HasForeignKey(m => m.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(h => h.Members).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        });

        model.Entity<Member>(e =>
        {
            e.ToTable("members");
            e.Property(m => m.Id).ValueGeneratedNever();
            e.Property(m => m.Name).HasMaxLength(60);
            e.Property(m => m.DietType).HasMaxLength(20);
            e.Property(m => m.Notes).HasMaxLength(500);
        });

        // --- catalog schema, read-only, owned by the ingestion repo ---

        model.Entity<CatalogIngredient>(e =>
        {
            e.ToTable("ingredients", "catalog", t => t.ExcludeFromMigrations());
            e.HasKey(i => i.Id);
        });

        model.Entity<CatalogStore>(e =>
        {
            e.ToTable("stores", "catalog", t => t.ExcludeFromMigrations());
            e.HasKey(s => s.Id);
        });

        model.Entity<CurrentStoreProduct>(e =>
        {
            e.ToView("current_store_products", "catalog");
            e.HasKey(p => p.Id);
        });

        model.Entity<ProductIngredientLink>(e =>
        {
            e.ToTable("product_ingredient", "catalog", t => t.ExcludeFromMigrations());
            e.HasKey(m => new { m.StoreId, m.ExternalId });
        });
    }
}

// Read models over the catalog schema. Column names follow the ingestion repo's snake_case schema.

public class CatalogIngredient
{
    public int Id { get; set; }
    public string CanonicalName { get; set; } = "";
    public string? NameKa { get; set; }
    public string? NameRu { get; set; }
    public string Category { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public string[] Aliases { get; set; } = [];
}

public class CatalogStore
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Url { get; set; }
}

/// <summary>Each store's products from its latest successful scrape.</summary>
public class CurrentStoreProduct
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public string StoreId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "GEL";
    public string? UnitRaw { get; set; }
    public decimal? UnitQty { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public bool InStock { get; set; }
}

public class ProductIngredientLink
{
    public string StoreId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public int? IngredientId { get; set; }
    public decimal Confidence { get; set; }
    public string Source { get; set; } = "";
    public bool Reviewed { get; set; }
}

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContext.Configure(builder, Environment.GetEnvironmentVariable("ConnectionStrings__App") ?? AppDbContext.DevConnectionString);
        return new AppDbContext(builder.Options);
    }
}
