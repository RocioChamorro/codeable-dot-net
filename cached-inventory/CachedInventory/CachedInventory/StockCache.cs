namespace CachedInventory;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class StockCache
{
  private readonly ConcurrentDictionary<int, int> cache = new();
  private readonly ConcurrentDictionary<int, Timer> timers = new();
  private readonly ConcurrentDictionary<int, SemaphoreSlim> semaphores = new();
  private readonly IWarehouseStockSystemClient client;

  public StockCache(IWarehouseStockSystemClient client) => this.client = client;

  public async Task<int> GetStock(int productId)
  {
    if (cache.TryGetValue(productId, out var cachedStock))
    {
      return cachedStock;
    }
    return await FetchAndCacheStock(productId);
  }

  public async Task Retrieve(int productId, int amount)
  {
    var semaphore = semaphores.GetOrAdd(productId, new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync();
    try
    {
      var stock = await GetStock(productId);
      if (stock < amount)
      {
        throw new InvalidOperationException("Not enough stock.");
      }
      cache[productId] = stock - amount;
      ResetTimer(productId);
    }
    finally
    {
      semaphore.Release();
    }
  }

  public async Task Restock(int productId, int amount)
  {
    var semaphore = semaphores.GetOrAdd(productId, new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync();
    try
    {
      var stock = await GetStock(productId);
      cache[productId] = stock + amount;
      ResetTimer(productId);
    }
    finally
    {
      semaphore.Release();
    }
  }

  private async Task<int> FetchAndCacheStock(int productId)
  {
    var stock = await client.GetStock(productId);
    cache[productId] = stock;
    return stock;
  }


  private void ResetTimer(int productId)
  {
    if (timers.TryGetValue(productId, out var existingTimer))
    {
      existingTimer.Change(2500, Timeout.Infinite);
    }
    else
    {
      var newTimer = new Timer(async state =>
      {
        var pid = (int)state!;
        if (cache.TryGetValue(pid, out var stock))
        {
          try
          {
            await client.UpdateStock(pid, stock);
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Error al actualizar el stock para el producto {pid}: {ex.Message}");
          }
        }
      }, productId, 2500, Timeout.Infinite);
      timers[productId] = newTimer;
    }
  }
}
