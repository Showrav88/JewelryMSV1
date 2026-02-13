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
   [Authorize]
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
    [HttpGet("download/{invoiceNo}")]
    [Authorize(Roles = "SHOP_OWNER")]
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
    [HttpPut("complete-draft")]
   [Authorize]
public async Task<IActionResult> CompleteDraft([FromBody] UpdateDraftSaleRequest request)
{
    try
    {
        // Use the standard "sub" claim for UserId and matching "shop_id" for ShopId
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                        ?? User.FindFirst("sub")?.Value;
        
        var shopIdStr = User.FindFirst("shop_id")?.Value;

        if (string.IsNullOrEmpty(shopIdStr) || string.IsNullOrEmpty(userIdStr))
            return Unauthorized(new { Message = "User context is missing from token." });

        var shopId = Guid.Parse(shopIdStr);
        var userId = Guid.Parse(userIdStr);

        var invoiceNo = await _saleService.UpdateDraftSaleAsync(request, shopId, userId);
        
        return Ok(new { 
            Success = true, 
            Message = "Draft invoice successfully converted to completed sale.", 
            InvoiceNo = invoiceNo 
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { Message = ex.Message });
    }
}
[HttpGet("kacha-memo/{invoiceNo}")]
[Authorize]
public async Task<IActionResult> DownloadKachaMemo(string invoiceNo)
{
    // 1. Basic Validation
    if (string.IsNullOrWhiteSpace(invoiceNo))
    {
        return BadRequest(new { message = "Invoice number is required." });
    }

    try
    {
        // 2. Call Service
        var pdfBytes = await _saleService.GenerateKachaMemoPdfAsync(invoiceNo);

        // 3. Check if PDF was generated successfully
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            return NotFound(new { message = $"Kacha Memo for {invoiceNo} could not be generated or does not exist." });
        }

        // 4. Return File
        return File(pdfBytes, "application/pdf", $"KachaMemo_{invoiceNo}.pdf");
    }
    catch (Exception ex)
    {
        // Log the error here if you have a logger
        // If it's a permission error, it will now be caught here
        return StatusCode(500, new { message = "An error occurred while generating the PDF.", details = ex.Message });
    }
}

}