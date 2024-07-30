namespace CachedInventory;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

public static class CachedInventoryApiBuilder
{
  public static WebApplication Build(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddScoped<IWarehouseStockSystemClient, WarehouseStockSystemClient>();

    // Inject the cache object and other dictionaries into the service container
    builder.Services.AddSingleton<StockCache>();

    var app = builder.Build();
    // Configure the HTTP request pipeline.

    app.UseHttpsRedirection();

    app.MapGet("/stock/{productId:int}",
      async ([FromServices] StockCache stockCache, int productId) =>
      {
        var stock = await stockCache.GetStock(productId);
        return Results.Ok(stock);
      })
      .WithName("GetStock")
      .WithOpenApi();

    app.MapPost("/stock/retrieve",
      async ([FromServices] StockCache stockCache, [FromBody] RetrieveStockRequest req) =>
      {
        try
        {
          await stockCache.Retrieve(req.ProductId, req.Amount);
          return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
          return Results.BadRequest(ex.Message);
        }
      })
      .WithName("RetrieveStock")
      .WithOpenApi();


    app.MapPost("/stock/restock",
      async ([FromServices] StockCache stockCache, [FromBody] RestockRequest req) =>
      {
        await stockCache.Restock(req.ProductId, req.Amount);
        return Results.Ok();
      })
      .WithName("Restock")
      .WithOpenApi();
    return app;
  }
}

public record RetrieveStockRequest(int ProductId, int Amount);

public record RestockRequest(int ProductId, int Amount);
