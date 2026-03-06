using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.DTOs.Sales;

namespace JewelryMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalesController : BaseApiController
{
    private readonly ISaleService _saleService;
    
    public SalesController(ISaleService saleService) => _saleService = saleService;
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // EXISTING ENDPOINTS
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Direct checkout - with or without exchange
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(Roles = "SHOP_OWNER,STAFF")]
    public async Task<IActionResult> Checkout([FromBody] CreateSaleRequest request)
    {
        try 
        {
            var shopId = GetCurrentShopId();
            var userId = GetCurrentUserId();

            var invoiceNo = await _saleService.ProcessCheckoutAsync(request, shopId, userId);
            return Ok(new { success = true, invoiceNo });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Calculate how much gold customer needs to bring
    /// </summary>
    [HttpGet("exchange-requirement")]
    [Authorize(Roles = "SHOP_OWNER,STAFF")]
    public async Task<IActionResult> GetExchangeRequirement(
        [FromQuery] string productIds,
        [FromQuery] decimal lossPercentage = 10)
    {
        try
        {
            var shopId = GetCurrentShopId();
            
            var ids = productIds.Split(',')
                .Select(id => Guid.Parse(id.Trim()))
                .ToList();

            var requirement = await _saleService.GetExchangeRequirementAsync(ids, lossPercentage, shopId);

            return Ok(new { success = true, data = requirement });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Download invoice PDF
    /// </summary>
    [HttpGet("download/{invoiceNo}")]
    [Authorize(Roles = "SHOP_OWNER,STAFF")]
    public async Task<IActionResult> DownloadInvoice(string invoiceNo)
    {
        try
        {
            var pdfBytes = await _saleService.GenerateInvoicePdfAsync(invoiceNo);
            return File(pdfBytes, "application/pdf", $"{invoiceNo}.pdf");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpGet]
[HttpGet]
public async Task<IActionResult> GetAll()
{
    var sales = await _saleService.GetAllAsync();
    return Ok(sales);
}

[HttpGet("{invoiceNo}")]
public async Task<IActionResult> GetByInvoiceNo(string invoiceNo)
{
    var details = await _saleService.GetInvoiceByNumberAsync(invoiceNo);
    if (details == null || !details.Any()) return NotFound();
    return Ok(details);
}
    /// <summary>
    /// Get invoice details (admin report)
    /// </summary>
    [HttpGet("report/{invoiceNo}")]
    [Authorize(Roles = "SHOP_OWNER")]
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