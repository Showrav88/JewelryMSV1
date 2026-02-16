using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.DTOs.Sales;
using System.Security.Claims;

namespace JewelryMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalesController : BaseApiController  // ← Changed from ControllerBase
{
    private readonly ISaleService _saleService;
    
    public SalesController(ISaleService saleService) => _saleService = saleService;
    
    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> Checkout([FromBody] CreateSaleRequest request)
    {
        try 
        {
            var shopId = GetCurrentShopId();  // ← Use base method
            var userId = GetCurrentUserId();   // ← Use base method

            var invoiceNo = await _saleService.ProcessCheckoutAsync(request, shopId, userId);
            return Ok(new { success = true, invoiceNo });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
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
            var shopId = GetCurrentShopId();  // ← Use base method
            var userId = GetCurrentUserId();   // ← Use base method

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
        if (string.IsNullOrWhiteSpace(invoiceNo))
            return BadRequest(new { message = "Invoice number is required." });

        try
        {
            var pdfBytes = await _saleService.GenerateKachaMemoPdfAsync(invoiceNo);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = $"Kacha Memo for {invoiceNo} not found." });

            return File(pdfBytes, "application/pdf", $"KachaMemo_{invoiceNo}.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error generating PDF.", details = ex.Message });
        }
    }

    [HttpGet("exchange-requirement")]
    [Authorize]  // ← Changed from SHOP_OWNER to allow all authenticated users
    public async Task<IActionResult> GetExchangeRequirement(
        [FromQuery] string productIds,
        [FromQuery] decimal extraPercentage = 10)
    {
        try
        {
            var shopId = GetCurrentShopId();  // ← Now works!
            
            var ids = productIds.Split(',')
                .Select(id => Guid.Parse(id.Trim()))
                .ToList();

            var requirement = await _saleService.GetExchangeRequirementAsync(
                ids,
                extraPercentage,
                shopId
            );

            return Ok(new { success = true, data = requirement });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}