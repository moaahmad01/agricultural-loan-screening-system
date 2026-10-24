using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgriculturalLoanSystem.Tests.Services;

public sealed class AdminCreationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateSectionPersistsScalarsWithoutSubmittedQuestions(bool submitGraph)
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var existing = new Section { SectionNameAr = "Existing", SectionNameEn = "Existing", Weight = 80, DisplayOrder = 1 };
        db.Sections.Add(existing);
        await db.SaveChangesAsync();
        var input = new Section
        {
            SectionNameAr = "Synthetic section", SectionNameEn = "Synthetic section",
            Weight = 20, DisplayOrder = 2, IsActive = !submitGraph
        };
        if (submitGraph)
        {
            input.Id = 123456;
            input.CreatedAt = input.UpdatedAt = new DateTime(2001, 1, 1);
            input.Questions.Add(new Question { QuestionTextAr = "Injected", QuestionTextEn = "Injected", PointValue = 5 });
        }

        var before = DateTime.UtcNow;
        await new AdminService(db, null!, cache).CreateSectionAsync(input);
        db.ChangeTracker.Clear();

        Assert.Equal(2, await db.Sections.CountAsync());
        Assert.Empty(await db.Questions.ToListAsync());
        var saved = await db.Sections.SingleAsync(s => s.SectionNameEn == input.SectionNameEn);
        Assert.Equal(input.SectionNameAr, saved.SectionNameAr);
        Assert.Equal(input.Weight, saved.Weight);
        Assert.Equal(input.DisplayOrder, saved.DisplayOrder);
        Assert.Equal(input.IsActive, saved.IsActive);
        Assert.NotEqual(123456, saved.Id);
        Assert.InRange(saved.CreatedAt, before, DateTime.UtcNow);
        Assert.InRange(saved.UpdatedAt, before, DateTime.UtcNow);
        Assert.Equal(80, (await db.Sections.SingleAsync(s => s.Id == existing.Id)).Weight);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateQuestionUsesExistingSectionWithoutSubmittedNavigation(bool submitGraph)
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var existing = new Section { SectionNameAr = "Existing", SectionNameEn = "Existing", Weight = 100, DisplayOrder = 1 };
        db.Sections.Add(existing);
        await db.SaveChangesAsync();
        var input = new Question
        {
            SectionId = existing.Id, QuestionTextAr = "Synthetic question", QuestionTextEn = "Synthetic question",
            PointValue = 3, DisplayOrder = 1, IsActive = !submitGraph
        };
        if (submitGraph)
        {
            input.Id = 123456;
            input.CreatedAt = input.UpdatedAt = new DateTime(2001, 1, 1);
            input.Section = new Section { SectionNameAr = "Injected", SectionNameEn = "Injected", Weight = 100, DisplayOrder = 2 };
        }

        var before = DateTime.UtcNow;
        await new AdminService(db, null!, cache).CreateQuestionAsync(input);
        db.ChangeTracker.Clear();

        Assert.Single(await db.Sections.ToListAsync());
        var saved = await db.Questions.SingleAsync();
        Assert.Equal(existing.Id, saved.SectionId);
        Assert.Equal(input.QuestionTextAr, saved.QuestionTextAr);
        Assert.Equal(input.QuestionTextEn, saved.QuestionTextEn);
        Assert.Equal(input.PointValue, saved.PointValue);
        Assert.Equal(input.DisplayOrder, saved.DisplayOrder);
        Assert.Equal(input.IsActive, saved.IsActive);
        Assert.NotEqual(123456, saved.Id);
        Assert.InRange(saved.CreatedAt, before, DateTime.UtcNow);
        Assert.InRange(saved.UpdatedAt, before, DateTime.UtcNow);
        Assert.Equal(100, (await db.Sections.SingleAsync()).Weight);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(999)]
    public async Task CreateQuestionRejectsMissingSectionEvenWithSubmittedNavigation(int sectionId)
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var input = new Question
        {
            SectionId = sectionId, QuestionTextAr = "Synthetic", QuestionTextEn = "Synthetic",
            Section = new Section { SectionNameAr = "Injected", SectionNameEn = "Injected" }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => new AdminService(db, null!, cache).CreateQuestionAsync(input));
        Assert.Empty(await db.Sections.ToListAsync());
        Assert.Empty(await db.Questions.ToListAsync());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
