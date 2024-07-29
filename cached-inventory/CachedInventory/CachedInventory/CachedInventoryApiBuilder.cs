namespace CachedInventory;

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
    builder.Services.AddSingleton<StockCache>(); // Registro de StockCache

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.MapGet(
                    "/stock/{productId:int}",
                    async ([FromServices] StockCache stockCache, int productId) =>
                    {
                      var stock = await stockCache.GetStock(productId);
                      return Results.Ok(stock);
                    })
                    .WithName("GetStock")
                    .WithOpenApi();

    app.MapPost(
        "/stock/retrieve",
        async ([FromServices] StockCache stockCache, [FromBody] RetrieveStockRequest req) =>
        {
          var stock = await stockCache.GetStock(req.ProductId);
          if (stock < req.Amount)
          {
            return Results.BadRequest("Not enough stock.");
          }

          stock -= req.Amount;
          await stockCache.UpdateStock(req.ProductId, stock);
          return Results.Ok();
        })
        .WithName("RetrieveStock")
        .WithOpenApi();

    app.MapPost(
        "/stock/restock",
        async ([FromServices] StockCache stockCache, [FromBody] RestockRequest req) =>
        {
          var stock = await stockCache.GetStock(req.ProductId);
          stock += req.Amount;
          await stockCache.UpdateStock(req.ProductId, stock);
          return Results.Ok();
        })
        .WithName("Restock")
        .WithOpenApi();
    return app;
  }
}

public record RetrieveStockRequest(int ProductId, int Amount);

public record RestockRequest(int ProductId, int Amount);
