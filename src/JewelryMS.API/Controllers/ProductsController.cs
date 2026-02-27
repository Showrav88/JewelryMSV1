using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.DTOs.Product;
using System.Security.Claims;

namespace JewelryMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    // Helper property to get ShopId from JWT
    private Guid CurrentShopId => Guid.Parse(User.FindFirst("shop_id")?.Value ?? Guid.Empty.ToString());

    // FIX: This helper method must be INSIDE the class scope
    private (Guid UserId, string Role) GetAuditMetadata()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdString, out Guid userId);

        var role = User.FindFirst(ClaimTypes.Role)?.Value 
                   ?? User.FindFirst("role")?.Value 
                   ?? "STAFF";

        return (userId, role);
    }

    [HttpGet]
    [Authorize(Roles = "SHOP_OWNER,STAFF")]
    public async Task<IActionResult> GetProducts()
    {
        var response = await _productService.GetShopProductsAsync();
        return Ok(response);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "SHOP_OWNER,STAFF")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var response = await _productService.GetProductByIdAsync(id);
        if (response == null) return NotFound("Product not found.");
        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "SHOP_OWNER")]
    [HasPermission("create_product")]
    public async Task<IActionResult> CreateProduct([FromBody] ProductCreateRequest request)
    {
        try 
        {
            var (userId, role) = GetAuditMetadata();
            var id = await _productService.CreateProductAsync(request, CurrentShopId, userId, role);
            return CreatedAtAction(nameof(GetProduct), new { id }, new { Message = "Product created", Id = id });
        }
        catch (ArgumentException ex) 
        {
            return BadRequest(new { Error = "Validation Failed", Details = ex.Message });
        }
        catch (InvalidOperationException ex) 
        {
            return Conflict(new { Error = "Conflict", Details = ex.Message });
        }
    }

    [HttpPatch("{id}")]
    [Authorize]
    [HasPermission("edit_product_price")] 
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductUpdateRequest request)
    {
        try 
        {
            var (userId, role) = GetAuditMetadata();
            var success = await _productService.UpdateProductAsync(id, request, userId, role);
            
            if (!success) return NotFound("Product not found or update failed.");
            return Ok(new { Message = "Update successful" });
        }
        catch (ArgumentException ex)
        {
            // Catches validation issues (Material, Purity, Category)
            return BadRequest(new { Error = "Validation Failed", Details = ex.Message });
        }
        catch (InvalidOperationException ex) 
        {
            // FIX: Catches the 'Sold' status violation and returns 400 instead of 500
            return BadRequest(new { Error = "Invalid Operation", Details = ex.Message });
        }
        // Change 'catch (Exception ex)' to just 'catch'
        catch (Exception) 
        { 
        return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    [HasPermission("delete_product")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var (userId, role) = GetAuditMetadata();
        var success = await _productService.DeleteProductAsync(id, userId, role);
        
        if (!success) return NotFound("Product not found.");
        return Ok("Product deleted.");
    }

    [HttpGet("metadata")]
    [Authorize]
public async Task<IActionResult> GetMetadata()
{
    var metadata = await _productService.GetProductMetadataAsync();
    return Ok(metadata);
}
}