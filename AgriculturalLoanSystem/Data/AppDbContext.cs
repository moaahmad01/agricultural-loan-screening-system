using AgriculturalLoanSystem.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace AgriculturalLoanSystem.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<PreviousLoan> PreviousLoans { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<ScreeningSession> ScreeningSessions { get; set; }
        public DbSet<ScreeningAnswer> ScreeningAnswers { get; set; }
        public DbSet<LoanCalculation> LoanCalculations { get; set; }
        public DbSet<PaymentSchedule> PaymentSchedules { get; set; }
        public DbSet<InterestRate> InterestRates { get; set; }
        public DbSet<ScreeningDocument> ScreeningDocuments { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<LoanCalculation>().HasIndex(l => l.SessionId)
                .IsUnique().HasFilter("[SessionId] IS NOT NULL");
            modelBuilder.Entity<PreviousLoan>().Property(l => l.Status).HasConversion<string>();
            modelBuilder.Entity<ScreeningSession>().Property(s => s.SessionStatus).HasConversion<string>();
            modelBuilder.Entity<ScreeningSession>().Property(s => s.Recommendation).HasConversion<string>();
            modelBuilder.Entity<ScreeningAnswer>().Property(a => a.Answer).HasConversion<string>();
            modelBuilder.Entity<LoanCalculation>().Property(l => l.PaymentFrequency).HasConversion<string>();
            modelBuilder.Entity<LoanCalculation>().Property(l => l.InterestType).HasConversion<string>();
            // (DB-01): تحويل الحقول الثلاثة المرتبطة بفئة القرض من نص حر إلى enum فعلي، بنفس نمط
            // التخزين كنص قابل للقراءة المُستخدم لبقية الـ Enums أعلاه.
            modelBuilder.Entity<LoanCalculation>().Property(l => l.LoanCategory).HasConversion<string>();
            modelBuilder.Entity<PreviousLoan>().Property(l => l.LoanCategory).HasConversion<string>();
            modelBuilder.Entity<InterestRate>().Property(r => r.LoanType).HasConversion<string>();
            modelBuilder.Entity<ScreeningDocument>()
   .HasOne(d => d.Session)
   .WithMany(s => s.Documents)
   .HasForeignKey(d => d.SessionId)
   .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ScreeningDocument>()
   .HasOne(d => d.Section)
   .WithMany().HasForeignKey(d => d.SectionId)
   .OnDelete(DeleteBehavior.Restrict);

            // (DB-02): كانت العلاقتان أدناه تعتمدان على سلوك EF Core الافتراضي (Cascade) للعلاقات
            // الإلزامية. هذا كان يعني أن حذف قسم يحذف معه كل أسئلته، وحذف سؤال يحذف معه كل إجابات
            // الفحص التاريخية المرتبطة به — حتى لو كانت هذه الإجابات جزءًا من قرار قرض معتمد فعليًا.
            // الآن Restrict يمنع الحذف الفعلي طالما توجد بيانات مرتبطة (AdminService يتحقق مسبقًا
            // ويستخدم تعطيلاً منطقيًا IsActive=false بدلاً من محاولة حذف سيفشل بخطأ قاعدة بيانات).
            modelBuilder.Entity<Question>()
   .HasOne(q => q.Section)
   .WithMany(s => s.Questions)
   .HasForeignKey(q => q.SectionId)
   .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ScreeningAnswer>()
   .HasOne(a => a.Question)
   .WithMany()
   .HasForeignKey(a => a.QuestionId)
   .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Client>().HasIndex(c => c.NationalId).IsUnique();

            // (DB-06): لم يكن هناك أي فهرس على AuditLogs رغم أن GetAuditLogsAsync يستعلم دومًا
            // بالترتيب على Timestamp (تنازليًا)، وأصبحت تدعم الآن فلترة بـ UserId أيضًا (DB-07) —
            // بلا فهرس، هذا يعني Table Scan كامل مع كل استعلام على جدول ينمو مع كل عملية حفظ
            // تقريبًا بالنظام (بسبب AuditInterceptor العام).
            modelBuilder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
            modelBuilder.Entity<AuditLog>().HasIndex(a => a.UserId);

            // (DB-04): الفهارس الفريدة أدناه كانت تشمل كل السجلات بما فيها المعطَّلة (IsActive=false)،
            // ما كان يمنع إعادة استخدام رقم ترتيب من قسم/سؤال مُعطَّل منطقيًا لعنصر جديد فعّال.
            // HasFilter تجعل الفهرس يفرض التفرّد على السجلات الفعّالة فقط.
            modelBuilder.Entity<Section>()
                .HasIndex(s => s.DisplayOrder)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            modelBuilder.Entity<Question>()
                .HasIndex(q => new { q.SectionId, q.DisplayOrder })
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            // (DB-03): لم يكن هناك أي قيد يمنع إجابتين لنفس السؤال في نفس جلسة الفحص — كان
            // منطق SaveSectionAnswersAsync (حذف ثم إعادة إدراج) الحارس الوحيد، وهو غير آمن تحت
            // التزامن (طلبان متزامنان لنفس الجلسة يمكن أن ينتجا صفوفًا مكررة).
            modelBuilder.Entity<ScreeningAnswer>()
                .HasIndex(a => new { a.SessionId, a.QuestionId })
                .IsUnique();

            modelBuilder.Entity<Client>()
   .Property(c => c.DateOfBirth)
   .HasColumnType("date");



        }
    }
}