using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Services.Implementations;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgriculturalLoanSystem.Tests.Services;

public sealed class WorkflowIntegrityTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData(SessionStatus.Completed, false)]
    [InlineData(SessionStatus.Abandoned, false)]
    [InlineData(SessionStatus.InProgress, true)]
    public async Task ClosedOrLoanLinkedSessionsRejectAnswerAndDocumentChanges(SessionStatus status, bool linked)
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var session = new ScreeningSession { ClientId = 1, SessionStatus = status };
        db.ScreeningSessions.Add(session);
        await db.SaveChangesAsync();
        if (linked) db.LoanCalculations.Add(new LoanCalculation { ClientId = 1, SessionId = session.Id });
        var doc = new ScreeningDocument { SessionId = session.Id, SectionId = 1, FilePath = "screening_docs/test.pdf" };
        db.ScreeningDocuments.Add(doc);
        await db.SaveChangesAsync();
        var service = new ScreeningService(db, cache);
        Assert.False(await service.IsSessionEditableAsync(session.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSectionAnswersAsync(session.Id, new() { [1] = AnswerType.Yes }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessFinalRecommendationAsync(session.Id));
        Assert.Null(await service.SaveDocumentAsync(session.Id, 1, "test.pdf", "screening_docs/test.pdf"));
        Assert.False(await service.DeleteDocumentAsync(doc.Id));
        Assert.Single(db.ScreeningDocuments);
        Assert.Empty(db.ScreeningAnswers);
        if (linked) Assert.False(await service.DeleteSessionAsync(session.Id));
    }

    [Theory]
    [InlineData(3, Recommendation.Eligible, 75)]
    [InlineData(2, Recommendation.EligibleWithConditions, 50)]
    [InlineData(1, Recommendation.NotEligible, 25)]
    public async Task ScoringPreservesDocumentedThresholds(int yesCount, Recommendation expected, int score)
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var section = new Section { SectionNameAr = "Synthetic", SectionNameEn = "Synthetic", Weight = 100, IsActive = true };
        var session = new ScreeningSession { ClientId = 1 };
        db.Sections.Add(section);
        db.ScreeningSessions.Add(session);
        await db.SaveChangesAsync();
        for (int i = 0; i < 4; i++)
        {
            var question = new Question { Section = section, QuestionTextAr = "Test", QuestionTextEn = "Test", IsActive = true, PointValue = 1, DisplayOrder = i + 1 };
            db.ScreeningAnswers.Add(new ScreeningAnswer { Session = session, Question = question, Answer = i < yesCount ? AnswerType.Yes : AnswerType.No });
        }
        await db.SaveChangesAsync();
        var result = await new ScreeningService(db, cache).ProcessFinalRecommendationAsync(session.Id);
        Assert.Equal(expected, result.Status);
        Assert.Equal(score, result.FinalScore);
        Assert.Equal(SessionStatus.Completed, session.SessionStatus);
    }

    [Fact]
    public async Task IncompleteScreeningCannotBeFinalized()
    {
        using var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var session = new ScreeningSession { ClientId = 1 };
        db.ScreeningSessions.Add(session);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ScreeningService(db, cache).ProcessFinalRecommendationAsync(session.Id));
        Assert.Equal(SessionStatus.InProgress, session.SessionStatus);
    }

    [Fact]
    public async Task LoanCannotReferenceAnotherClientsScreeningOrDuplicateASession()
    {
        using var db = Database();
        var session = new ScreeningSession { ClientId = 1 };
        db.ScreeningSessions.Add(session);
        await db.SaveChangesAsync();
        var service = new LoansService(db);
        var input = new LoanInputModel { PrincipalAmount = 1000, DurationMonths = 12 };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLoanApplicationAsync(2, session.Id, LoanCategory.SeasonalShortTerm, input, 6.5m, new()));
        await service.CreateLoanApplicationAsync(1, session.Id, LoanCategory.SeasonalShortTerm, input, 6.5m, new());
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLoanApplicationAsync(1, session.Id, LoanCategory.SeasonalShortTerm, input, 6.5m, new()));
        Assert.Single(db.LoanCalculations);
    }
}
