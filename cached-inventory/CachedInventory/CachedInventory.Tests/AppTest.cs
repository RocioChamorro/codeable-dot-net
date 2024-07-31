// ReSharper disable ClassNeverInstantiated.Global

namespace CachedInventory.Tests;

public class SingleRetrieval
{
  [Fact(DisplayName = "retirar un producto")]
  public static async Task Test() => await TestApiPerformance.Test(1, [3], false, 2_000);
}

public class FourRetrievalsInParallel
{
  [Fact(DisplayName = "retirar cuatro productos en paralelo")]
  public static async Task Test() => await TestApiPerformance.Test(2, [1, 2, 3, 4], true, 1_000);
}

public class FourRetrievalsInParallelMore
{
  [Fact(DisplayName = "retirar más productos de lo que hay en el stock")]
  public static async Task Test() => await TestApiPerformance.Test(6, [1, 2, 3, 4, 5, 6, 7, 8, 9], true, 1_000, true);
}

public class FourRetrievalsSequentially
{
  [Fact(DisplayName = "retirar cuatro productos secuencialmente")]
  public static async Task Test() => await TestApiPerformance.Test(3, [1, 2, 3, 4], false, 1_000);
}

public class SevenRetrievalsInParallel
{
  [Fact(DisplayName = "retirar siete productos en paralelo")]
  public static async Task Test() => await TestApiPerformance.Test(4, [1, 2, 3, 4, 5, 6, 7], true, 500);
}

public class SevenRetrievalsSequentially
{
  [Fact(DisplayName = "retirar siete productos secuencialmente")]
  public static async Task Test() => await TestApiPerformance.Test(5, [1, 2, 3, 4, 5, 6, 7], false, 500);
}

internal static class TestApiPerformance
{
  internal static async Task Test(int productId, int[] retrievals, bool isParallel, long expectedPerformance, bool more = false)
  {
    await using var setup = await TestSetup.Initialize();
    var initialStock = retrievals.Sum();
    await setup.Restock(productId, retrievals.Sum());
    if (more)
    {
      initialStock -= 1;
      await setup.Restock(productId, initialStock);
    }

    await setup.VerifyStockFromFile(productId, retrievals.Sum());

    var tasks = new List<Task>();
    var errors = new List<Exception>();
    foreach (var retrieval in retrievals)
    {
      var task = Task.Run(async () =>
     {
       try
       {
         await setup.Retrieve(productId, retrieval);
       }
       catch (Exception ex)
       {
         if (more)
         {
           errors.Add(ex);
         }
         else
         {
           throw;
         }
       }
     });

      if (!isParallel)
      {
        await task;
      }

      tasks.Add(task);
    }

    await Task.WhenAll(tasks);
    if (more)
    {
      Assert.True(errors.Count > 0, "Se esperaba que al menos una retirada fallara por falta de stock.");
      var failedRetrievals = errors.Count;
      var expectedFinalStock = initialStock - (retrievals.Sum() - retrievals.Take(failedRetrievals).Sum());
      var finalStock = await setup.GetStock(productId);
      Assert.True(finalStock == expectedFinalStock, $"El stock final no es el esperado. Stock final: {finalStock}, Stock esperado: {expectedFinalStock}.");
    }
    else
    {
      Assert.True(errors.Count == 0, "No se esperaban errores durante las retiradas.");
      var finalStock = await setup.GetStock(productId);
      Assert.True(finalStock == 0, $"El stock final no es 0, sino {finalStock}.");

    }

    Assert.True(
      setup.AverageRequestDuration < expectedPerformance,
      $"Duración promedio: {setup.AverageRequestDuration}ms, se esperaba un máximo de {expectedPerformance}ms.");
    if (!more)
    {
      await setup.VerifyStockFromFile(productId, 0);
    }
  }
}
