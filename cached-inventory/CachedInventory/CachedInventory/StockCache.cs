namespace CachedInventory;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class StockCache : IWarehouseStockSystemClient
{
  private readonly ConcurrentDictionary<int, int> cache = new();
  private readonly IWarehouseStockSystemClient client;
  private readonly SemaphoreSlim semaphore = new(1, 1);

  public StockCache(IWarehouseStockSystemClient client) => this.client = client;

  public async Task<int> GetStock(int productId)
  {
    if (!cache.TryGetValue(productId, out var stock))
    {
      stock = await client.GetStock(productId);
      cache[productId] = stock;
    }

    return stock;
  }

  public async Task UpdateStock(int productId, int newAmount)
  {
    cache[productId] = newAmount;
    await client.UpdateStock(productId, newAmount);
  }

  public async Task Retrieve(int productId, int amount)
  {
    await semaphore.WaitAsync();
    try
    {
      var stock = await GetStock(productId);
      if (stock < amount)
      {
        throw new InvalidOperationException($"Not enough stock for product {productId}.");
      }
      stock -= amount;
      await UpdateStock(productId, stock);
    }
    finally
    {
      semaphore.Release();
    }
  }
}
