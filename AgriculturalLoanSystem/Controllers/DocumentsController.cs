using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgriculturalLoanSystem.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DocumentsController(
    IClientsService clients, IScreeningService screening, PrivateDocumentStore storage) : Controller
{
    private async Task<bool> CanReadAsync(int clientId)
    {
        if (User.Identity?.IsAuthenticated != true) return false;
        if (User.IsInRole("Admin") || User.IsInRole("Employee")) return true;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var client = await clients.GetClientEntityAsync(clientId);
        return userId != null && client?.ApplicationUserId == userId;
    }

    [HttpGet]
    public async Task<IActionResult> Identity(int clientId)
    {
        if (!await CanReadAsync(clientId)) return Forbid();
        var client = await clients.GetClientEntityAsync(clientId);
        return Download(client?.IdentityDocumentPath);
    }

    [HttpGet]
    public async Task<IActionResult> Screening(int documentId)
    {
        var document = await screening.GetDocumentAsync(documentId);
        if (document == null) return NotFound();
        var clientId = await screening.GetClientIdForDocumentAsync(documentId);
        if (clientId == null || !await CanReadAsync(clientId.Value)) return Forbid();
        return Download(document.FilePath);
    }

    private IActionResult Download(string? key)
    {
        var path = storage.Resolve(key);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, "application/octet-stream", "document" + Path.GetExtension(path));
    }
}
