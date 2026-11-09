using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Karlson.Api.Tests.Http;

/// <summary>
/// The real API on a throwaway Postgres container. The app schema comes from this repo's
/// migrations (the API migrates on start in Development); the catalog schema is owned by the
/// ingestion repo, so <see cref="CatalogSql"/> stands in for it with only the columns the app reads.
/// If ingestion renames or drops one of them, these tests are where the app side has to follow.
/// </summary>
public class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var seeded = await _postgres.ExecScriptAsync(CatalogSql);
        if (seeded.ExitCode != 0)
            throw new InvalidOperationException($"Catalog fixture failed: {seeded.Stderr}");

        await WaitForPortAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:App", _postgres.GetConnectionString());
        });
    }

    /// <summary>
    /// The container is ready before its published port is: some Docker runtimes (Rancher Desktop)
    /// forward the port a moment after start.
    /// </summary>
    private async Task WaitForPortAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
                await connection.OpenAsync();
                return;
            }
            catch (NpgsqlException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(200);
            }
        }
    }

    /// <summary>A client with its own cookie jar, i.e. one device.</summary>
    public HttpClient NewDevice() => _factory.CreateClient();

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private const string CatalogSql = """
        CREATE SCHEMA catalog;

        CREATE TABLE catalog.stores (
            id text PRIMARY KEY, name text NOT NULL, type text NOT NULL, url text, city text, address text);

        CREATE TABLE catalog.ingredients (
            id int PRIMARY KEY, canonical_name text NOT NULL, name_ka text, name_ru text, category text NOT NULL,
            tags text[] NOT NULL DEFAULT '{}', aliases text[] NOT NULL DEFAULT '{}',
            default_unit text NOT NULL DEFAULT 'g', grams_per_piece numeric(8,1));

        -- A view in the real schema; the app only ever selects from it.
        CREATE TABLE catalog.current_store_products (
            id bigint PRIMARY KEY, run_id bigint NOT NULL, store_id text NOT NULL, external_id text NOT NULL,
            name text NOT NULL, brand text, price numeric(12,2) NOT NULL, regular_price numeric(12,2),
            currency text NOT NULL DEFAULT 'GEL', unit_raw text, unit_qty numeric(12,3), unit text,
            price_per_unit numeric(12,2), sold_by text NOT NULL DEFAULT 'package', barcode text,
            category text, url text, image_url text, in_stock boolean NOT NULL DEFAULT true);

        CREATE TABLE catalog.product_ingredient (
            store_id text NOT NULL, external_id text NOT NULL, ingredient_id int,
            confidence numeric(4,3) NOT NULL, source text NOT NULL, reviewed boolean NOT NULL DEFAULT false,
            PRIMARY KEY (store_id, external_id));

        INSERT INTO catalog.stores VALUES ('agrohub', 'Agrohub', 'web', 'https://agrohub.ge', 'Tbilisi', NULL);

        INSERT INTO catalog.ingredients (id, canonical_name, name_ka, name_ru, category, tags, default_unit, grams_per_piece) VALUES
            (1, 'greek yogurt', 'ბერძნული იოგურტი', 'греческий йогурт', 'dairy', '{dairy,protein}', 'g', NULL),
            (2, 'bananas', 'ბანანი', 'бананы', 'fruit', '{vegan}', 'pcs', 120),
            (3, 'imeretian saffron', 'ყვითელი ყვავილი', 'имеретинский шафран', 'spices', '{}', 'g', NULL);

        INSERT INTO catalog.current_store_products
            (id, run_id, store_id, external_id, name, brand, price, regular_price, unit_qty, unit, price_per_unit, sold_by, in_stock) VALUES
            (1, 5, 'agrohub', '22918', 'ბერძნული იოგურტი 10% /Olympus/ 500 გრ', 'Olympus',  9.50, 11.95, 500, 'g', 19.00, 'package', true),
            (2, 5, 'agrohub', '23215', 'ბერძნული იოგურტი ვანილით 0% Olympus 150გ', NULL,     3.50, NULL,  150, 'g', 23.33, 'package', true),
            (3, 5, 'agrohub', '30001', 'ბერძნული იოგურტი 1 კგ',                     NULL,     1.00, NULL, 1000, 'g',  1.00, 'package', false),
            (4, 5, 'agrohub', '19369', 'ბანანი ასაწონი /500.00გრ',                  NULL,     2.38, NULL,  500, 'g',  4.76, 'weight',  true),
            (5, 5, 'agrohub', '40001', 'შამპუნი 400 მლ',                            NULL,    12.00, NULL,  400, 'ml', 30.00, 'package', true);

        INSERT INTO catalog.product_ingredient (store_id, external_id, ingredient_id, confidence, source) VALUES
            ('agrohub', '22918', 1,    0.85, 'rule'),
            ('agrohub', '23215', 1,    0.60, 'llm'),
            ('agrohub', '30001', 1,    0.85, 'rule'),
            ('agrohub', '19369', 2,    0.85, 'rule'),
            ('agrohub', '40001', NULL, 1.00, 'rule');
        """;
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
