using AgriculturalLoanSystem.Controllers;
using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Interceptors;
using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Services.Implementations;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace AgriculturalLoanSystem.Tests.Services;

public sealed class SecurityRegressionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("other")]
    public async Task RegistrationCannotClaimExistingClient(string? previousOwner)
    {
        using var db = Database();
        db.Clients.Add(new Client { NationalId = "DEMO-EXISTING", FullNameAr = "Synthetic", ApplicationUserId = previousOwner });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClientsService(db)
            .LinkOrCreateClientForNewUserAsync("DEMO-EXISTING", "Attacker", null, "attacker"));
        Assert.Equal(previousOwner, db.Clients.Single().ApplicationUserId);
    }

    [Fact]
    public async Task AuditDoesNotCopyIdentitySecretsOrPersonalValues()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = Principal("actor", "Citizen") } };
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new AuditInterceptor(accessor)).Options);
        db.Users.Add(new IdentityUser { UserName = "SENSITIVE-NATIONAL-ID", PasswordHash = "SECRET-HASH", SecurityStamp = "SECRET-STAMP" });
        db.Clients.Add(new Client { NationalId = "PRIVATE-ID", FullNameAr = "PRIVATE-NAME" });
        await db.SaveChangesAsync();
        var logs = await db.AuditLogs.ToListAsync();
        Assert.NotEmpty(logs);
        foreach (var log in logs)
        {
            Assert.Equal("actor", log.UserId);
            Assert.Null(log.UserName);
            Assert.DoesNotContain("SECRET-", log.Changes);
            Assert.DoesNotContain("PRIVATE-", log.Changes);
            Assert.DoesNotContain("SENSITIVE-", log.Changes);
        }
    }

    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("identity_docs/../../secret.pdf")]
    [InlineData("identity_docs/..\\secret.pdf")]
    [InlineData("identity_docs/C:secret.pdf")]
    [InlineData("identity_docs/%2e%2e.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("unknown/file.pdf")]
    public void StorageRejectsUnsafeKeys(string key) => Assert.Null(Store().Resolve(key));

    [Fact]
    public void LegacyReferencesResolveOnlyToPrivateStorage()
    {
        var store = Store();
        var path = store.Resolve("/uploads/identity_docs/example.pdf");
        Assert.NotNull(path);
        Assert.Contains(Path.Combine("App_Data", "Documents", "identity_docs"), path);
        Assert.DoesNotContain("wwwroot", path);
    }

    [Theory]
    [InlineData("owner", "Citizen", true)]
    [InlineData("other", "Citizen", false)]
    [InlineData("staff", "Employee", true)]
    [InlineData("staff", "Admin", true)]
    public async Task BothDownloadEndpointsEnforceOwnership(string userId, string role, bool allowed)
    {
        var clients = new Mock<IClientsService>();
        clients.Setup(s => s.GetClientEntityAsync(1)).ReturnsAsync(new Client { Id = 1, ApplicationUserId = "owner" });
        var screening = new Mock<IScreeningService>();
        screening.Setup(s => s.GetDocumentAsync(2)).ReturnsAsync(new ScreeningDocument { Id = 2 });
        screening.Setup(s => s.GetClientIdForDocumentAsync(2)).ReturnsAsync(1);
        var controller = new DocumentsController(clients.Object, screening.Object, Store())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(userId, role) } }
        };
        var identity = await controller.Identity(1);
        var document = await controller.Screening(2);
        // Authorized requests reach file lookup; unauthorized requests must stop before it.
        if (allowed) { Assert.IsType<NotFoundResult>(identity); Assert.IsType<NotFoundResult>(document); }
        else { Assert.IsType<ForbidResult>(identity); Assert.IsType<ForbidResult>(document); }
    }

    [Theory]
    [InlineData("bad.exe", "%PDF-1.7", false)]
    [InlineData("bad.pdf", "not a PDF", false)]
    [InlineData("sample.pdf", "%PDF-1.7", true)]
    public async Task UploadChecksExtensionAndSignature(string name, string data, bool valid)
    {
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(data));
        var file = new FormFile(stream, 0, stream.Length, "file", name);
        Assert.Equal(valid, (await FileUploadValidator.ValidateAsync(file)).IsValid);
    }

    [Fact]
    public async Task UploadRejectsOversizeAndEmptyFiles()
    {
        using var stream = new MemoryStream();
        Assert.False((await FileUploadValidator.ValidateAsync(new FormFile(stream, 0, 0, "file", "x.pdf"))).IsValid);
        Assert.False((await FileUploadValidator.ValidateAsync(new FormFile(stream, 0, 6 * 1024 * 1024, "file", "x.pdf"))).IsValid);
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static PrivateDocumentStore Store()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(Path.Combine(Path.GetTempPath(), "loan-tests", Guid.NewGuid().ToString()));
        return new PrivateDocumentStore(env.Object);
    }
    private static ClaimsPrincipal Principal(string id, string role) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], "Test"));
}
