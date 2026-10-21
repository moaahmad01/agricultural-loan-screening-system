using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Models;

namespace AgriculturalLoanSystem.Services.Interfaces
{
    public interface IScreeningService
    {
        Task<List<Section>> GetSectionsAndQuestionsAsync();

        Task<int> CreateNewSessionAsync(int clientId, string userId);

        Task<ClientDetailsViewModel?> GetClientBriefAsync(int clientId);

        Task<(List<ScreeningAnswer> ExistingAnswers, List<int> CompletedSectionIndexes)> GetSessionProgressAsync(int sessionId, List<Section> sections);

        Task SaveSectionAnswersAsync(int sessionId, Dictionary<int, AnswerType> answers);

        Task<bool> IsSessionEditableAsync(int sessionId);

        Task<bool> IsSessionCompleteAsync(int sessionId);

        Task<RecommendationResultDto> ProcessFinalRecommendationAsync(int sessionId);

        Task<EvaluationResultViewModel?> GetEvaluationResultViewModelAsync(int sessionId);


        Task<bool> HasLinkedLoanAsync(int sessionId);

        Task<int?> GetClientIdForSessionAsync(int sessionId);
        Task<int?> GetClientIdForDocumentAsync(int documentId);

        Task<bool> DeleteSessionAsync(int sessionId);

        Task<ScreeningDocument?> SaveDocumentAsync(int sessionId, int sectionId, string fileName, string filePath);
        Task<bool> DeleteDocumentAsync(int documentId);
        Task<ScreeningDocument?> GetDocumentAsync(int documentId);
        Task<List<ScreeningDocument>> GetSessionDocumentsAsync(int sessionId);

        Task<ScreeningSession?> GetSessionReportAsync(int sessionId);

    }

}