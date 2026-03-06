using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelryMS.Domain.DTOs.Purchase;
using JewelryMS.Domain.Interfaces.Services;

namespace JewelryMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchasesController : BaseApiController
{
    private readonly IPurchaseService _service;

    public PurchasesController(IPurchaseService service)
    {
        _service = service;
    }

    // ════════════════════════════════════════════════════════════════
    //  STEP 1 — Rate Calculator  (no DB, safe to call anytime)
    //  POST /api/purchases/calculate
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Preview what the customer will receive before saving anything.
    /// Body: { baseMaterial, grossWeight, testedPurity,
    ///         standardBuyingRatePerGram, standardPurity }
    /// </summary>
    [HttpPost("calculate")]
    public IActionResult CalculateRate([FromBody] CalculateRateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = _service.CalculateRate(request);
        return Ok(result);
    }

    // ════════════════════════════════════════════════════════════════
    //  STEP 2 — Customer Lookup  (find by NID / phone, not UUID)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Search by NID number, contact number, or name in one call.
    /// GET /api/purchases/customers/search?q=017xxxxxxxx
    /// </summary>
    [HttpGet("customers/search")]
    public async Task<IActionResult> SearchCustomers([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Search term (q) is required." });

        var results = await _service.SearchCustomersAsync(q);
        return Ok(results);
    }

    /// <summary>GET /api/purchases/customers/search/nid?nid=1234567890</summary>
    [HttpGet("customers/search/nid")]
    public async Task<IActionResult> SearchByNid([FromQuery] string nid)
    {
        if (string.IsNullOrWhiteSpace(nid))
            return BadRequest(new { message = "NID number is required." });

        var results = await _service.SearchCustomersByNidAsync(nid);
        return Ok(results);
    }

    /// <summary>GET /api/purchases/customers/search/contact?contact=017xxxxxxxx</summary>
    [HttpGet("customers/search/contact")]
    public async Task<IActionResult> SearchByContact([FromQuery] string contact)
    {
        if (string.IsNullOrWhiteSpace(contact))
            return BadRequest(new { message = "Contact number is required." });

        var results = await _service.SearchCustomersByContactAsync(contact);
        return Ok(results);
    }

    // ════════════════════════════════════════════════════════════════
    //  STEP 3 — Purchase CRUD
    // ════════════════════════════════════════════════════════════════

    // GET /api/purchases
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    // GET /api/purchases/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    // GET /api/purchases/customer/{customerId}
    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId)
        => Ok(await _service.GetByCustomerAsync(customerId));

    // GET /api/purchases/material/Gold  or  /material/Silver
    [HttpGet("material/{baseMaterial}")]
    public async Task<IActionResult> GetByMaterial(string baseMaterial)
        => Ok(await _service.GetByMaterialAsync(baseMaterial));

    // GET /api/purchases/range?from=2025-01-01&to=2025-12-31
    [HttpGet("range")]
    public async Task<IActionResult> GetByDateRange(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to)
        => Ok(await _service.GetByDateRangeAsync(from, to));

    // POST /api/purchases
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // BaseApiController.GetCurrentUserId() / GetCurrentShopId()
        // read directly from the JWT — same pattern as your AuthController
        var receiptNo = await _service.CreateAsync(request, GetCurrentUserId(), GetCurrentShopId());
        return Ok(new { receiptNo });
    }



 [HttpGet("receipt/{receiptNo}")]
public async Task<IActionResult> DownloadReceiptByNumber(string receiptNo)
{
    try
    {
        var purchase = await _service.GetByReceiptNoAsync(receiptNo);
        if (purchase is null) return NotFound();

        var pdf = await _service.GeneratePurchaseReceiptPdfAsync(purchase.Id);
        return File(pdf, "application/pdf", $"{purchase.ReceiptNo}.pdf");
    }
    catch (KeyNotFoundException) { return NotFound(); }
}

    // PUT /api/purchases/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePurchaseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _service.UpdateAsync(id, request, GetCurrentUserId());
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // DELETE /api/purchases/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}