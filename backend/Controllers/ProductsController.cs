using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RedisDemo.Api.Dtos;
using RedisDemo.Api.Repositories;
using StackExchange.Redis;

namespace RedisDemo.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductRepository _productRepository;
    private readonly RedisRepository _redisRepository;

    public ProductsController(ProductRepository productRepository, RedisRepository redisRepository)
    {
        _productRepository = productRepository;
        _redisRepository = redisRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetProducts([FromQuery] bool trackMetrics = true)
    {
        if (!trackMetrics)
        {
            var observedProducts = await _productRepository.GetProductsAsync();
            Response.Headers["X-Data-Source"] = "MYSQL";
            Response.Headers["X-Cache-Mode"] = "OBSERVE_ONLY";
            return Ok(observedProducts);
        }

        try
        {
            var cachedProducts = await _redisRepository.GetProductsFromCacheAsync(trackMetrics);
            if (cachedProducts is not null)
            {
                Response.Headers["X-Data-Source"] = "REDIS";
                return Ok(cachedProducts);
            }
        }
        catch (RedisException)
        {
            Response.Headers["X-Redis-Status"] = "OFFLINE";
        }

        var lockToken = await _redisRepository.TryAcquireProductsCacheRebuildLockAsync();
        if (lockToken is null)
        {
            var rebuiltProducts = await _redisRepository.WaitForProductsCacheAsync(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(100));
            if (rebuiltProducts is not null)
            {
                Response.Headers["X-Data-Source"] = "REDIS";
                Response.Headers["X-Cache-Rebuild"] = "WAITED_FOR_OTHER_REQUEST";
                return Ok(rebuiltProducts);
            }

            Response.Headers["X-Cache-Rebuild"] = "LOCK_TIMEOUT_FALLBACK_MYSQL";
            var fallbackProducts = await _productRepository.GetProductsAsync();
            Response.Headers["X-Data-Source"] = "MYSQL";
            return Ok(fallbackProducts);
        }

        try
        {
            Response.Headers["X-Cache-Rebuild"] = "LOCK_OWNER";
            var timer = Stopwatch.StartNew();
            var products = await _productRepository.GetProductsAsync();
            timer.Stop();

            try
            {
                await _redisRepository.SaveProductsCacheAsync(products, timer.ElapsedMilliseconds);
            }
            catch (RedisException)
            {
                Response.Headers["X-Redis-Cache-Save"] = "SKIPPED";
            }

            Response.Headers["X-Data-Source"] = "MYSQL";
            return Ok(products);
        }
        finally
        {
            await _redisRepository.ReleaseProductsCacheRebuildLockAsync(lockToken);
        }
    }

    [HttpGet("{productId:long}")]
    public async Task<ActionResult<ProductDto>> GetProduct(long productId)
    {
        var cachedProduct = await _redisRepository.GetProductFromCacheAsync(productId);
        if (cachedProduct is not null)
        {
            Response.Headers["X-Data-Source"] = "REDIS";
            return Ok(cachedProduct);
        }

        var lockToken = await _redisRepository.TryAcquireProductCacheRebuildLockAsync(productId);
        if (lockToken is null)
        {
            var rebuiltProduct = await _redisRepository.WaitForProductCacheAsync(
                productId,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(100));
            if (rebuiltProduct is not null)
            {
                Response.Headers["X-Data-Source"] = "REDIS";
                Response.Headers["X-Cache-Rebuild"] = "WAITED_FOR_OTHER_REQUEST";
                return Ok(rebuiltProduct);
            }

            Response.Headers["X-Cache-Rebuild"] = "LOCK_TIMEOUT_FALLBACK_MYSQL";
            var fallbackProduct = await _productRepository.GetProductByIdAsync(productId);
            if (fallbackProduct is null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            Response.Headers["X-Data-Source"] = "MYSQL";
            return Ok(fallbackProduct);
        }

        try
        {
            Response.Headers["X-Cache-Rebuild"] = "LOCK_OWNER";
            var product = await _productRepository.GetProductByIdAsync(productId);
            if (product is null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm." });
            }

            await _redisRepository.SaveProductCacheAsync(product);
            Response.Headers["X-Data-Source"] = "MYSQL";
            return Ok(product);
        }
        finally
        {
            await _redisRepository.ReleaseProductCacheRebuildLockAsync(productId, lockToken);
        }
    }

    [HttpDelete("cache")]
    public async Task<ActionResult> ClearCache()
    {
        try
        {
            await _redisRepository.ClearProductsCacheAsync();
        }
        catch (RedisException)
        {
            Response.Headers["X-Redis-Status"] = "OFFLINE";
        }

        return NoContent();
    }
}
