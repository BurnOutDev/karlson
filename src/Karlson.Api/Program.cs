using Karlson.Api.Data;
using Karlson.Api.Features.Catalog;
using Karlson.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("App") ?? AppDbContext.DevConnectionString;
builder.Services.AddDbContext<AppDbContext>(o => AppDbContext.Configure(o, connectionString));
builder.Services.AddDataProtection();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HouseholdSession>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Keep the local DB in step with the code; production runs migrations explicitly.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapHouseholds();
app.MapCatalog();

// The React app is served from wwwroot in production; in development Vite proxies /api here.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
