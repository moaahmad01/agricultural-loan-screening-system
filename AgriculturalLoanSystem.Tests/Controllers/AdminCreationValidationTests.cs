using AgriculturalLoanSystem.Controllers;
using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgriculturalLoanSystem.Tests.Controllers;

public sealed class AdminCreationValidationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(999)]
    public async Task MissingSectionReturnsValidationErrorWithoutCreatingRecords(int sectionId)
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new AdminController(new AdminService(db, null!, cache), null!, null!);
        var input = new Question { SectionId = sectionId, QuestionTextAr = "Synthetic", QuestionTextEn = "Synthetic" };

        var result = await controller.CreateQuestion(input);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(controller.ModelState[nameof(Question.SectionId)]!.Errors);
        Assert.Empty(await db.Questions.ToListAsync());
        Assert.Empty(await db.Sections.ToListAsync());
        Assert.NotNull(controller.ViewBag.SectionId);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task SectionCreationPreservesExistingTotalWeightLimit(int weight, bool allowed)
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        db.Sections.Add(new Section { SectionNameAr = "Existing", SectionNameEn = "Existing", Weight = 100, DisplayOrder = 1 });
        await db.SaveChangesAsync();
        var controller = new AdminController(new AdminService(db, null!, cache), null!, null!);
        var input = new Section { SectionNameAr = "Synthetic", SectionNameEn = "Synthetic", Weight = weight, DisplayOrder = 2 };

        var result = await controller.CreateSection(input);

        Assert.Equal(allowed, controller.ModelState.IsValid);
        Assert.Equal(allowed ? 2 : 1, await db.Sections.CountAsync());
        Assert.Empty(await db.Questions.ToListAsync());
        if (allowed) Assert.IsType<RedirectToActionResult>(result);
        else
        {
            Assert.IsType<ViewResult>(result);
            Assert.NotEmpty(controller.ModelState[nameof(Section.Weight)]!.Errors);
        }
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
