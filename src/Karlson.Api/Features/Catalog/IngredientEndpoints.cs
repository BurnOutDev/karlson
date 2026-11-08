using Karlson.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Karlson.Api.Features.Catalog;

public record StoreOfferDto(string StoreId, int ProductCount, decimal MinPrice, decimal? MinPricePerKg);
public record IngredientDto(int Id, string Name, string? NameKa, string? NameRu, string Category, string[] Tags, IReadOnlyList<StoreOfferDto> Offers);
public record ProductDto(string StoreId, string ExternalId, string Name, string? Brand, decimal Price, decimal? UnitQty, string? Unit, decimal? PricePerKg, string? Url, string? ImageUrl, decimal Confidence);

/// <summary>Read-only queries over the ingestion catalog: what each ingredient costs where.</summary>
public static class IngredientEndpoints
{
    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");
        group.MapGet("/stores", Stores);
        group.MapGet("/ingredients", Ingredients);
        group.MapGet("/ingredients/{id:int}/products", IngredientProducts);
        return app;
    }

    private static async Task<Ok<List<CatalogStore>>> Stores(AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Stores.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct));

    private static async Task<Ok<List<IngredientDto>>> Ingredients(AppDbContext db, string? search, CancellationToken ct)
    {
        var ingredientsQuery = db.Ingredients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            ingredientsQuery = ingredientsQuery.Where(i =>
                i.CanonicalName.ToLower().Contains(s) ||
                (i.NameKa != null && i.NameKa.Contains(s)) ||
                (i.NameRu != null && i.NameRu.ToLower().Contains(s)));
        }
        var ingredients = await ingredientsQuery.OrderBy(i => i.Category).ThenBy(i => i.CanonicalName).ToListAsync(ct);

        // One grouped query for all offers: per ingredient and store, how many products and the cheapest.
        var offers = await (
            from link in db.ProductIngredients
            join product in db.CurrentProducts on new { link.StoreId, link.ExternalId } equals new { product.StoreId, product.ExternalId }
            where link.IngredientId != null && product.InStock
            group product by new { IngredientId = link.IngredientId!.Value, product.StoreId } into g
            select new
            {
                g.Key.IngredientId,
                g.Key.StoreId,
                Count = g.Count(),
                MinPrice = g.Min(p => p.Price),
                MinPricePerKg = g.Where(p => p.UnitQty > 0 && (p.Unit == "g" || p.Unit == "ml"))
                                 .Min(p => (decimal?)(p.Price / p.UnitQty! * 1000)),
            }).ToListAsync(ct);

        var byIngredient = offers.ToLookup(o => o.IngredientId);

        return TypedResults.Ok(ingredients.Select(i => new IngredientDto(
            i.Id, i.CanonicalName, i.NameKa, i.NameRu, i.Category, i.Tags,
            byIngredient[i.Id]
                .OrderBy(o => o.StoreId)
                .Select(o => new StoreOfferDto(o.StoreId, o.Count, o.MinPrice, o.MinPricePerKg is { } ppk ? Math.Round(ppk, 2) : null))
                .ToList())).ToList());
    }

    private static async Task<Ok<List<ProductDto>>> IngredientProducts(int id, AppDbContext db, CancellationToken ct)
    {
        var products = await (
            from link in db.ProductIngredients
            join product in db.CurrentProducts on new { link.StoreId, link.ExternalId } equals new { product.StoreId, product.ExternalId }
            where link.IngredientId == id
            orderby product.StoreId, product.Price
            select new ProductDto(
                product.StoreId, product.ExternalId, product.Name, product.Brand, product.Price, product.UnitQty, product.Unit,
                product.UnitQty > 0 && (product.Unit == "g" || product.Unit == "ml") ? Math.Round(product.Price / product.UnitQty!.Value * 1000, 2) : null,
                product.Url, product.ImageUrl, link.Confidence))
            .ToListAsync(ct);

        return TypedResults.Ok(products);
    }
}
