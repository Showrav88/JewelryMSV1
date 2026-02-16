using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JewelryMS.API.Controllers;

/// <summary>
/// Base controller with common helper methods for extracting JWT claims
/// </summary>
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Get current user's shop ID from JWT token
    /// </summary>
    protected Guid GetCurrentShopId()
    {
        var shopIdClaim = User.FindFirst("shop_id")?.Value;
        
        if (string.IsNullOrEmpty(shopIdClaim))
            throw new UnauthorizedAccessException("Shop ID not found in token");

        if (!Guid.TryParse(shopIdClaim, out var shopId))
            throw new UnauthorizedAccessException("Invalid Shop ID format");

        return shopId;
    }

    /// <summary>
    /// Get current user ID from JWT token
    /// </summary>
    protected Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in token");

        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid User ID format");

        return userId;
    }

    /// <summary>
    /// Get current user's role from JWT token
    /// </summary>
    protected string GetCurrentUserRole()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (string.IsNullOrEmpty(role))
            throw new UnauthorizedAccessException("Role not found in token");

        return role;
    }

    /// <summary>
    /// Try to get shop ID, returns null if not found (for optional shop context)
    /// </summary>
    protected Guid? TryGetCurrentShopId()
    {
        var shopIdClaim = User.FindFirst("shop_id")?.Value;
        
        if (string.IsNullOrEmpty(shopIdClaim))
            return null;

        return Guid.TryParse(shopIdClaim, out var shopId) ? shopId : null;
    }
}