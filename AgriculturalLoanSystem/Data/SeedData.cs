using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace AgriculturalLoanSystem.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<AppDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            if (!context.Sections.Any())
            {
                var sections = new List<Section>
                {
                    new Section { SectionNameAr = "أ. ممارسات مناخية إيجابية ذكية", SectionNameEn = "A. Positive Climate Smart Practices", DisplayOrder = 1, IsActive = true, Weight = 20 },
                    new Section { SectionNameAr = "ب. الفوائد الاجتماعية والبيئية الإيجابية", SectionNameEn = "B. Positive Social and Environmental Benefits", DisplayOrder = 2, IsActive = true, Weight = 25 },
                    new Section { SectionNameAr = "ج. استبعادي", SectionNameEn = "C. Exclusionary Criteria", DisplayOrder = 3, IsActive = true, Weight = 25 },
                    new Section { SectionNameAr = "د. الأنشطة ذات المخاطر المتزايدة", SectionNameEn = "D. Activities with Increased Risks", DisplayOrder = 4, IsActive = true, Weight = 30 },
                    new Section { SectionNameAr = "هـ. القدرات والاستعداد للتنفيذ", SectionNameEn = "E. Capabilities and Readiness to Implement", DisplayOrder = 5, IsActive = true, Weight = 0 }
                };
                context.Sections.AddRange(sections);
                await context.SaveChangesAsync();
            }

            var s1 = await context.Sections.FirstOrDefaultAsync(s => s.DisplayOrder == 1);
            var s2 = await context.Sections.FirstOrDefaultAsync(s => s.DisplayOrder == 2);
            var s3 = await context.Sections.FirstOrDefaultAsync(s => s.DisplayOrder == 3);
            var s4 = await context.Sections.FirstOrDefaultAsync(s => s.DisplayOrder == 4);
            var s5 = await context.Sections.FirstOrDefaultAsync(s => s.DisplayOrder == 5);

            if (!context.Questions.Any())
            {
                var questions = new List<Question>();

                if (s1 != null)
                {
                    questions.Add(new Question
                    {
                        SectionId = s1.Id,
                        DisplayOrder = 1,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يطبق المشروع ممارسات الري والتسميد الفعالة (مثل التقطير المدمج، أشرطة التنقيط، التسميد بالري)؟",
                        QuestionTextEn = "Does the project apply effective irrigation and fertigation practices (e.g., integrated drip, drip tapes, fertigation)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s1.Id,
                        DisplayOrder = 2,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل تم دمج تكنولوجيا الطاقة المتجددة والتحكم في المناخ (مثل مضخات الري الشمسية، التجفيف الشمسي، البيوت البلاستيكية)؟",
                        QuestionTextEn = "Is renewable energy and climate control technology integrated (e.g., solar pumps, solar drying, greenhouses)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s1.Id,
                        DisplayOrder = 3,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يتم استخدام أصناف المحاصيل المقاومة للمناخ وممارسات تحسين التربة (مثل بذور مقاومة للجفاف، التسميد العضوي)؟",
                        QuestionTextEn = "Are climate-resilient crop varieties and soil improvement practices used (e.g., drought-resistant seeds, composting)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s1.Id,
                        DisplayOrder = 4,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل ينفذ المشروع أساليب الزراعة البيئية التي تدعم مكافحة الآفات والتنوع البيولوجي (مثل المكافحة المتكاملة للآفات)؟",
                        QuestionTextEn = "Does the project implement agroecological methods supporting pest control and biodiversity (e.g., Integrated Pest Management)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s1.Id,
                        DisplayOrder = 5,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يتضمن المشروع تقنيات مستدامة لإنتاج الثروة الحيوانية (مثل سلالات مقاومة لتغير المناخ، تحسين البنية التحتية للمياه)؟",
                        QuestionTextEn = "Does the project include sustainable livestock techniques (e.g., climate-resilient breeds, improved water infrastructure)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s1.Id,
                        DisplayOrder = 6,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يتضمن المشروع أنظمة التخزين والتداول المستدامة بعد الحصاد (مثل التخزين المبرد، مرافق التعبئة والتدريج)؟",
                        QuestionTextEn = "Does the project include sustainable post-harvest storage and handling systems (e.g., cold storage, packing facilities)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s1.Id,
                        DisplayOrder = 7,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يتم تطبيق تدابير الحفاظ على الموارد الأرضية والمائية (مثل المصاطب، السدود الحجرية، حصاد مياه الأمطار)؟",
                        QuestionTextEn = "Are land and water conservation measures applied (e.g., terracing, stone bunds, rainwater harvesting)?"
                    });
                }

                if (s2 != null)
                {
                    questions.Add(new Question
                    {
                        SectionId = s2.Id,
                        DisplayOrder = 1,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يعمل المشروع بشكل فعال على إشراك الشباب أو النساء أو الفئات الضعيفة وتحسين وصولهم للتمويل؟",
                        QuestionTextEn = "Does the project effectively engage youth, women, or vulnerable groups and improve their access to finance?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s2.Id,
                        DisplayOrder = 2,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يعمل المشروع على توسيع نطاق وصول الفئات الضعيفة إلى الممارسات الذكية مناخيًا والموارد الإنتاجية؟",
                        QuestionTextEn = "Does the project expand access for vulnerable groups to climate-smart practices and productive resources?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s2.Id,
                        DisplayOrder = 3,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يعمل المشروع على خلق أو تعزيز سبل العيش (مثل توليد فرص عمل) مما يقلل الهشاشة المناخية؟",
                        QuestionTextEn = "Does the project create or enhance livelihoods (e.g., job creation), reducing climate vulnerability?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s2.Id,
                        DisplayOrder = 4,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يعمل المشروع على بناء المهارات العملية وشبكة العلاقات والوصول للخدمات الإرشادية؟",
                        QuestionTextEn = "Does the project build practical skills, networks, and access to extension services?"
                    });
                }

                if (s3 != null)
                {
                    questions.Add(new Question
                    {
                        SectionId = s3.Id,
                        DisplayOrder = 1,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل سيؤدي المشروع إلى فقدان التنوع البيولوجي أو الإضرار بالموائل الحساسة؟",
                        QuestionTextEn = "Will the project lead to biodiversity loss or damage sensitive habitats?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s3.Id,
                        DisplayOrder = 2,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل سيؤدي المشروع إلى استخدام غير مستدام للمياه أو استنزاف طبقات المياه الجوفية؟",
                        QuestionTextEn = "Will the project lead to unsustainable water use or depletion of aquifers?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s3.Id,
                        DisplayOrder = 3,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يتم استخدام أو التخطيط لاستخدام مواد كيميائية محظورة أو شديدة الخطورة؟",
                        QuestionTextEn = "Are prohibited or highly hazardous chemicals being used or planned for use?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s3.Id,
                        DisplayOrder = 4,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل سيؤدي المشروع إلى تدهور التربة دون وجود خطة للتعافي (مثل الزراعة في منحدرات معرضة للتعرية)؟",
                        QuestionTextEn = "Will the project lead to soil degradation without a recovery plan (e.g., farming on erosion-prone slopes)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s3.Id,
                        DisplayOrder = 5,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يتضمن المشروع نزاعات على الأراضي أو تهجير أو قضايا حيازة؟",
                        QuestionTextEn = "Does the project involve land disputes, displacement, or tenure issues?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s3.Id,
                        DisplayOrder = 6,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل هناك انتهاكات تتعلق بالعمل (مثل عمالة الأطفال أو ظروف عمل غير آمنة)؟",
                        QuestionTextEn = "Are there labor violations (e.g., child labor or unsafe working conditions)?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s3.Id,
                        DisplayOrder = 7,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل سيعمل المشروع على المواقع الثقافية أو التاريخية أو سيؤثر عليها؟",
                        QuestionTextEn = "Will the project operate on or affect cultural or historical sites?"
                    });
                }

                if (s4 != null)
                {
                    questions.Add(new Question
                    {
                        SectionId = s4.Id,
                        DisplayOrder = 1,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل هناك ممارسات تُسبب انبعاثات غازات دفيئة عالية دون اتباع نهج للتخفيف؟",
                        QuestionTextEn = "Are there practices causing high greenhouse gas emissions without mitigation?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s4.Id,
                        DisplayOrder = 2,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل هناك زراعة أحادية للأنواع غير المحلية دون اتباع نهج لإدارة المخاطر؟",
                        QuestionTextEn = "Is there monoculture of non-native species without a risk management approach?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s4.Id,
                        DisplayOrder = 3,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل سيتسبب المشروع في تلوث أو تدهور المياه أو التربة أو الهواء؟",
                        QuestionTextEn = "Will the project cause pollution or degradation of water, soil, or air?"
                    });
                }

                if (s5 != null)
                {
                    questions.Add(new Question
                    {
                        SectionId = s5.Id,
                        DisplayOrder = 1,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يمتلك مقدم الطلب القدرة والمهارات والدعم المطلوب لتنفيذ المشروع بشكل مستدام؟",
                        QuestionTextEn = "Does the applicant possess the required capacity, skills, and support for sustainable implementation?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s5.Id,
                        DisplayOrder = 2,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل هناك إمكانية الوصول الكافية إلى الموارد والبنية الأساسية لدعم التنفيذ؟",
                        QuestionTextEn = "Is there adequate access to resources and infrastructure to support implementation?"
                    });

                    questions.Add(new Question
                    {
                        SectionId = s5.Id,
                        DisplayOrder = 3,
                        IsActive = true,
                        PointValue = 3,
                        QuestionTextAr = "هل يمتلك مقدم الطلب معرفة مالية أو لديه أنظمة إدارة مالية لضمان الشفافية؟",
                        QuestionTextEn = "Does the applicant have financial literacy or financial management systems to ensure transparency?"
                    });
                }

                context.Questions.AddRange(questions);
                await context.SaveChangesAsync();
            }
            if (!context.InterestRates.Any())
            {
                // (DB-01): استُبدلت القيم النصية الحرة "MediumLong"/"SeasonalShort" (التي لم تكن
                // مطابقة أصلاً لأسماء enum LoanCategory الحقيقية) بقيم الـ enum نفسها.
                var rates = new List<InterestRate>
                {
                    new InterestRate { LoanType = LoanCategory.MediumLongTerm, MinLoanAmount = 1, MaxLoanAmount = 10000, AnnualRate = 6.5m },
                    new InterestRate { LoanType = LoanCategory.MediumLongTerm, MinLoanAmount = 10001, MaxLoanAmount = 15000, AnnualRate = 7.0m },
                    new InterestRate { LoanType = LoanCategory.MediumLongTerm, MinLoanAmount = 15001, MaxLoanAmount = 20000, AnnualRate = 7.5m },
                    new InterestRate { LoanType = LoanCategory.MediumLongTerm, MinLoanAmount = 20001, MaxLoanAmount = 30000, AnnualRate = 8.0m },
                    new InterestRate { LoanType = LoanCategory.MediumLongTerm, MinLoanAmount = 30001, MaxLoanAmount = 50000, AnnualRate = 8.5m },
                    new InterestRate { LoanType = LoanCategory.MediumLongTerm, MinLoanAmount = 50001, MaxLoanAmount = 999999999, AnnualRate = 9.0m },

                    new InterestRate { LoanType = LoanCategory.SeasonalShortTerm, MinLoanAmount = 1, MaxLoanAmount = 5000, AnnualRate = 6.5m },
                    new InterestRate { LoanType = LoanCategory.SeasonalShortTerm, MinLoanAmount = 5001, MaxLoanAmount = 10000, AnnualRate = 7.0m },
                    new InterestRate { LoanType = LoanCategory.SeasonalShortTerm, MinLoanAmount = 10001, MaxLoanAmount = 15000, AnnualRate = 7.5m },
                    new InterestRate { LoanType = LoanCategory.SeasonalShortTerm, MinLoanAmount = 15001, MaxLoanAmount = 20000, AnnualRate = 8.0m },
                    new InterestRate { LoanType = LoanCategory.SeasonalShortTerm, MinLoanAmount = 20001, MaxLoanAmount = 30000, AnnualRate = 8.5m },
                    new InterestRate { LoanType = LoanCategory.SeasonalShortTerm, MinLoanAmount = 30001, MaxLoanAmount = 999999999, AnnualRate = 9.0m }
                };

                context.InterestRates.AddRange(rates);
                await context.SaveChangesAsync();
            }

            // توحيد اسم الدور (ARCH-02): كان الدور المزروع هنا "Officer" بينما كل الكونترولرز
            // (Admin/Clients/Loans/Home) تتحقق من دور "Employee" — ما يجعل "Officer" دورًا ميتًا
            // لا يمنح أي صلاحية فعلية. تم توحيده على "Employee" في كل مكان.
            string[] roleNames = { "Admin", "Employee", "Citizen" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var initialAdminPassword = configuration["INITIAL_ADMIN_PASSWORD"];
            var adminUser = await userManager.FindByNameAsync("admin");
            // Explicit opt-in bootstrap. No generated passwords, credential logs, or default accounts.
            if (adminUser == null && !string.IsNullOrWhiteSpace(initialAdminPassword))
            {
                var user = new IdentityUser { UserName = "admin" };
                var result = await userManager.CreateAsync(user, initialAdminPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException("Administrator bootstrap failed. Check the configured password policy.");
                var roleResult = await userManager.AddToRoleAsync(user, "Admin");
                if (!roleResult.Succeeded)
                {
                    await userManager.DeleteAsync(user);
                    throw new InvalidOperationException("Administrator role assignment failed.");
                }
            }
        }
    }
}
