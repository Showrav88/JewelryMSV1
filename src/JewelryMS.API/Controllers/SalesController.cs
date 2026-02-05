using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.DTOs.Sales;
using System.Security.Claims;

namespace JewelryMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly ISaleService _saleService;
    public SalesController(ISaleService saleService) => _saleService = saleService;

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CreateSaleRequest request)
    {
        try 
        {
            var shopIdClaim = User.FindFirst("shop_id")?.Value;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (shopIdClaim == null || userIdClaim == null)
                return Unauthorized("Missing user or shop identification in token.");

            var shopId = Guid.Parse(shopIdClaim);
            var userId = Guid.Parse(userIdClaim);

            var invoiceNo = await _saleService.ProcessCheckoutAsync(request, shopId, userId);
            return Ok(new { success = true, invoiceNo });
        }
        catch (Exception ex)
        {
            return BadRequest(new { 
                success = false, 
                message = ex.Message 
            });
        }
    }

    [HttpGet("report/{invoiceNo}")]
    public async Task<IActionResult> GetInvoiceReport(string invoiceNo)
    {
        try
        {
            var report = await _saleService.GetInvoiceDetailsAsync(invoiceNo);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}