using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AgriculturalLoanSystem.Services.Implementations
{
    public class ClientsService : IClientsService
    {
        private readonly AppDbContext _context;

        public ClientsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Client> LinkOrCreateClientForNewUserAsync(
            string nationalId,
            string fullNameAr,
            string? phoneNumber,
            string applicationUserId)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.NationalId == nationalId);

            if (client != null)
            {
                // Knowing a national ID is not evidence of identity or ownership.
                throw new InvalidOperationException("An existing client record requires staff-assisted identity verification.");
            }
            else
            {
                client = new Client
                {
                    NationalId = nationalId,
                    FullNameAr = fullNameAr,
                    PhoneNumber = phoneNumber,
                    ApplicationUserId = applicationUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _context.Clients.Add(client);
            }

            await _context.SaveChangesAsync();
            return client;
        }

        public async Task<Client?> GetClientEntityAsync(int clientId)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
        }

        public async Task<Client?> GetClientByApplicationUserIdAsync(string applicationUserId)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == applicationUserId);
        }

        public async Task<bool> NationalIdExistsAsync(string nationalId)
        {
            return await _context.Clients.AnyAsync(c => c.NationalId == nationalId);
        }

        public async Task<Client> CreateClientAsync(ClientCreateViewModel model)
        {
            var client = new Client
            {
                NationalId = model.NationalId,
                FullNameAr = model.FullNameAr,
                FullNameEn = model.FullNameEn ?? string.Empty,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth,
                Region = model.Region,
                DetailedArea = model.DetailedArea,
                Address = model.Address,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                // ApplicationUserId يبقى فارغًا عمدًا: هذا المسار مخصَّص للموظف الذي يُدخل بيانات
                // عميل قبل أن يُنشئ ذلك العميل حسابه الخاص لاحقًا (سيُربط تلقائيًا حين يسجّل بنفس
                // الرقم الوطني عبر LinkOrCreateClientForNewUserAsync أعلاه).
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();
            return client;
        }

        public async Task<Client?> UpdateProfileAsync(
            int id,
            string fullNameAr,
            string? fullNameEn,
            string? phoneNumber,
            DateOnly? dateOfBirth,
            string? region,
            string? detailedArea,
            string? address,
            string? newIdentityDocumentPath)
        {
            var existingClient = await _context.Clients.FindAsync(id);
            if (existingClient == null) return null;

            existingClient.FullNameAr = fullNameAr;
            existingClient.FullNameEn = fullNameEn ?? existingClient.FullNameEn;
            existingClient.PhoneNumber = phoneNumber;
            existingClient.DateOfBirth = dateOfBirth;
            existingClient.Region = region;
            existingClient.DetailedArea = detailedArea;
            existingClient.Address = address;
            existingClient.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(newIdentityDocumentPath))
            {
                existingClient.IdentityDocumentPath = newIdentityDocumentPath;
            }

            _context.Update(existingClient);
            await _context.SaveChangesAsync();
            return existingClient;
        }

        public async Task<(List<ClientSearchResult> Items, int Total)> SearchClientsAsync(
            string? query,
            IReadOnlyCollection<string> excludedApplicationUserIds,
            int page = 1,
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var queryableClients = _context.Clients
                .Where(c => c.ApplicationUserId == null || !excludedApplicationUserIds.Contains(c.ApplicationUserId))
                .AsQueryable();

            var effectiveQuery = query ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(effectiveQuery))
            {
                queryableClients = queryableClients.Where(c =>
                    c.FullNameAr.Contains(effectiveQuery) || c.NationalId.Contains(effectiveQuery));
            }

            // (DB-07 — إصلاح خطأ حقيقي من موديل 2): كان العدد الإجمالي يُحسَب قبل تطبيق فلتر
            // البحث النصي، فكانت قيمة "إجمالي العملاء" المعروضة بالواجهة ثابتة دومًا (كل العملاء
            // غير الموظفين) بغض النظر عن نص البحث المُدخَل، بدل عدد النتائج المطابقة فعليًا.
            var total = await queryableClients.CountAsync();

            var items = await queryableClients
                .OrderBy(c => c.FullNameAr)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ClientSearchResult
                {
                    Id = c.Id,
                    FullName = c.FullNameAr,
                    FullNameEn = c.FullNameEn,
                    NationalId = c.NationalId,
                    PhoneNumber = c.PhoneNumber,
                    Region = c.Region
                }).ToListAsync();

            return (items, total);
        }

        public async Task<ClientDetailsViewModel?> GetDetailsAsync(int clientId)
        {
            return await _context.Clients
                .Where(c => c.Id == clientId)
                .Select(c => new ClientDetailsViewModel
                {
                    Id = c.Id,
                    NationalId = c.NationalId,
                    FullNameAr = c.FullNameAr,
                    FullNameEn = c.FullNameEn,
                    PhoneNumber = c.PhoneNumber,
                    DateOfBirth = c.DateOfBirth,
                    Region = c.Region ?? string.Empty,
                    DetailedArea = c.DetailedArea ?? string.Empty,
                    Address = c.Address ?? string.Empty,
                    IdentityDocumentPath = c.IdentityDocumentPath,

                    PreviousLoans = c.PreviousLoans.Select(l => new PreviousLoanDto
                    {
                        // (DB-01): l.LoanCategory أصبحت enum؛ الـ DTO يعرضها كنص كما كان سابقًا.
                        LoanType = l.LoanCategory.HasValue ? l.LoanCategory.Value.ToString() : null,
                        LoanAmount = l.LoanAmount,
                        Status = l.Status.ToString(),
                        LoanDate = l.LoanDate
                    }).OrderByDescending(l => l.LoanDate).ToList(),

                    LoanCalculations = c.LoanCalculations.ToList(),

                    ScreeningSessions = c.ScreeningSessions
                        .OrderByDescending(s => s.CompletedAt ?? s.StartedAt)
                        .ToList(),

                    LastScreeningSession = c.ScreeningSessions
                        .OrderByDescending(s => s.CompletedAt)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<Dictionary<int, LoanApplicationStatus>> GetLoanStatusMapAsync(int clientId)
        {
            return await _context.LoanCalculations
                .Where(l => l.ClientId == clientId && l.SessionId != null)
                .OrderBy(l => l.Id)
                .GroupBy(l => l.SessionId!.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Last().Status);
        }
    }
}
