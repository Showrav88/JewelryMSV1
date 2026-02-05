using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.DTOs.Rates;

namespace JewelryMS.API.Controllers;


[ApiController]
[Route("api/admin/rates")]
public class MetalRateController : ControllerBase
{
    private readonly IMetalRateService _rateService;

    public MetalRateController(IMetalRateService rateService)
    {
        _rateService = rateService;
    }

    private Guid CurrentShopId => Guid.Parse(User.FindFirst("shop_id")?.Value ?? Guid.Empty.ToString());
    [HttpGet("shop")]
    [Authorize]
    [HasPermission("view_rates")]
    public async Task<IActionResult> GetShopRates()
    {
        var rates = await _rateService.GetRatesForCurrentShopAsync(CurrentShopId);
        return Ok(rates);
    }

[HttpPut("update")]
[Authorize]
[HasPermission("update_rates")]
public async Task<IActionResult> UpdateDailyRate([FromBody] RateUpdateRequest request)
{
    try 
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out Guid userId)) return Unauthorized();

        // 3. Extract the role from the token
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value 
                      ?? User.FindFirst("role")?.Value;

        if (string.IsNullOrEmpty(userRole)) return BadRequest("Role is missing in token.");

        // 4. Pass the userRole to the Service
        var success = await _rateService.UpdateMetalRateAsync(request, CurrentShopId, userId, userRole);
        return success ? Ok(new { Message = "Rate updated successfully" }) : BadRequest();
    }
    catch (ArgumentException ex) 
    {
        return BadRequest(new { Error = ex.Message });
    }
}
}