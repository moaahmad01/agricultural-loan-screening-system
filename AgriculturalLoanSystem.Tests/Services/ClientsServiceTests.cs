using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriculturalLoanSystem.Tests.Services
{
    public class ClientsServiceTests
    {
        private static (ClientsService service, AppDbContext context) CreateService(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new AppDbContext(options);
            var service = new ClientsService(context);
            return (service, context);
        }

        [Fact]
        public async Task SearchClientsAsync_IncludesClientsWithNullApplicationUserId()
        {
            // عملاء أنشأهم موظف عبر ميزة "إضافة عميل جديد" (موديل 2) لهم ApplicationUserId=null
            // حتى ينشئوا حسابهم بأنفسهم لاحقًا — يجب أن يظهروا بنتائج البحث فورًا، وليس فقط بعد
            // ربطهم بحساب. هذا اختبار مباشر للإصلاح الذي طبّقته بموديل 2 حول دلالات SQL NULL.
            var (service, context) = CreateService(nameof(SearchClientsAsync_IncludesClientsWithNullApplicationUserId));

            context.Clients.Add(new Client { NationalId = "1111111111", FullNameAr = "عميل بلا حساب", ApplicationUserId = null });
            await context.SaveChangesAsync();

            var (items, total) = await service.SearchClientsAsync(query: null, excludedApplicationUserIds: new List<string> { "staff-1" });

            Assert.Equal(1, total);
            Assert.Single(items);
        }

        [Fact]
        public async Task SearchClientsAsync_ExcludesStaffAccounts()
        {
            var (service, context) = CreateService(nameof(SearchClientsAsync_ExcludesStaffAccounts));

            context.Clients.AddRange(
                new Client { NationalId = "2222222222", FullNameAr = "عميل عادي", ApplicationUserId = "citizen-1" },
                new Client { NationalId = "3333333333", FullNameAr = "حساب موظف", ApplicationUserId = "staff-1" }
            );
            await context.SaveChangesAsync();

            var (items, total) = await service.SearchClientsAsync(query: null, excludedApplicationUserIds: new List<string> { "staff-1" });

            Assert.Equal(1, total);
            Assert.Equal("عميل عادي", items.Single().FullName);
        }

        [Fact]
        public async Task SearchClientsAsync_TotalCount_ReflectsFilteredResults_NotUnfilteredTotal()
        {
            // (DB-07 — إصلاح خطأ حقيقي من موديل 2): كان العدد الإجمالي يُحسَب قبل تطبيق فلتر
            // البحث النصي، فكان ثابتًا دومًا بغض النظر عن نص البحث.
            var (service, context) = CreateService(nameof(SearchClientsAsync_TotalCount_ReflectsFilteredResults_NotUnfilteredTotal));

            context.Clients.AddRange(
                new Client { NationalId = "4444444444", FullNameAr = "أحمد محمد" },
                new Client { NationalId = "5555555555", FullNameAr = "سالم علي" }
            );
            await context.SaveChangesAsync();

            var (items, total) = await service.SearchClientsAsync(query: "أحمد", excludedApplicationUserIds: new List<string>());

            Assert.Equal(1, total); // وليس 2 (إجمالي كل العملاء) كما كان يحدث بالخطأ القديم
            Assert.Single(items);
        }

        [Fact]
        public async Task SearchClientsAsync_RespectsPageSize()
        {
            // (DB-07): كانت تُعيد كل النتائج المطابقة دفعة واحدة بلا أي حد.
            var (service, context) = CreateService(nameof(SearchClientsAsync_RespectsPageSize));

            for (int i = 0; i < 25; i++)
            {
                context.Clients.Add(new Client { NationalId = $"9{i:D9}", FullNameAr = $"عميل {i}" });
            }
            await context.SaveChangesAsync();

            var (items, total) = await service.SearchClientsAsync(
                query: null, excludedApplicationUserIds: new List<string>(), page: 1, pageSize: 10);

            Assert.Equal(25, total); // العدد الإجمالي الحقيقي
            Assert.Equal(10, items.Count); // لكن حجم الصفحة الواحدة محدود فعليًا
        }
    }
}
