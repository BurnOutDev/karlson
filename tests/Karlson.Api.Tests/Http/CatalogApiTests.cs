using System.Net.Http.Json;
using Karlson.Api.Data;
using Karlson.Api.Features.Catalog;

namespace Karlson.Api.Tests.Http;

[Collection(ApiCollection.Name)]
public class CatalogApiTests(ApiFixture api)
{
    [Fact]
    public async Task Stores_come_from_the_catalog()
    {
        var stores = await api.NewDevice().GetFromJsonAsync<List<CatalogStore>>("/api/stores");

        var store = Assert.Single(stores!);
        Assert.Equal(("agrohub", "Tbilisi"), (store.Id, store.City));
    }

    [Fact]
    public async Task Ingredient_offers_count_only_in_stock_products_and_pick_the_cheapest()
    {
        var ingredients = await api.NewDevice().GetFromJsonAsync<List<IngredientDto>>("/api/ingredients");

        Assert.Equal(3, ingredients!.Count);

        var yogurt = ingredients.Single(i => i.Name == "greek yogurt");
        var offer = Assert.Single(yogurt.Offers);
        // The 1.00 ₾ pack is out of stock, so it neither counts nor sets the minimum.
        Assert.Equal(("agrohub", 2, 3.50m, 19.00m), (offer.StoreId, offer.ProductCount, offer.MinPrice, offer.MinPricePerKg));

        var bananas = ingredients.Single(i => i.Name == "bananas");
        Assert.Equal(("pcs", 120m), (bananas.DefaultUnit, bananas.GramsPerPiece));

        Assert.Empty(ingredients.Single(i => i.Name == "imeretian saffron").Offers);
    }

    [Theory]
    [InlineData("YOGURT", "greek yogurt")]
    [InlineData("ბანან", "bananas")]
    [InlineData("шафран", "imeretian saffron")]
    public async Task Ingredients_are_searchable_in_three_languages(string search, string expected)
    {
        var ingredients = await api.NewDevice().GetFromJsonAsync<List<IngredientDto>>($"/api/ingredients?search={Uri.EscapeDataString(search)}");

        Assert.Equal(expected, Assert.Single(ingredients!).Name);
    }

    [Fact]
    public async Task Products_of_an_ingredient_carry_sale_price_and_how_they_are_sold()
    {
        var device = api.NewDevice();

        var yogurts = (await device.GetFromJsonAsync<List<ProductDto>>("/api/ingredients/1/products"))!;
        Assert.Equal(["30001", "23215", "22918"], yogurts.Select(p => p.ExternalId));   // cheapest first
        var onSale = yogurts.Single(p => p.ExternalId == "22918");
        Assert.Equal((9.50m, 11.95m, 19.00m, "package"), (onSale.Price, onSale.RegularPrice, onSale.PricePerUnit, onSale.SoldBy));
        Assert.Null(yogurts.Single(p => p.ExternalId == "23215").RegularPrice);

        var banana = Assert.Single((await device.GetFromJsonAsync<List<ProductDto>>("/api/ingredients/2/products"))!);
        Assert.Equal(("weight", 500m, 4.76m), (banana.SoldBy, banana.UnitQty, banana.PricePerUnit));
    }
}
