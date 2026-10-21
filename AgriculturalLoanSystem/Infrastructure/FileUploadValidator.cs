namespace AgriculturalLoanSystem.Infrastructure
{
    /// <summary>
    /// مدقق رفع ملفات مشترك (SEC-09 في تقرير المراجعة): كان رفع الملفات (مستندات هوية العملاء،
    /// ومستندات جلسات الفحص لاحقًا في موديل 3) يقبل أي ملف بأي امتداد وبأي حجم دون تحقق إطلاقًا.
    ///
    /// هذا المدقق يتحقق من ثلاثة أشياء:
    /// 1) الحجم الأقصى.
    /// 2) الامتداد الظاهري (Whitelist).
    /// 3) التوقيع الفعلي لبداية الملف (Magic Number) — لمنع ملف تنفيذي أُعيدت تسميته ليبدو PDF/صورة.
    /// </summary>
    public static class FileUploadValidator
    {
        public const long DefaultMaxSizeBytes = 5 * 1024 * 1024; // 5 ميجابايت

        private static readonly Dictionary<string, List<byte[]>> AllowedSignatures = new()
        {
            [".pdf"] = new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46 } },                 // %PDF
            [".jpg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        };

        public static async Task<FileValidationResult> ValidateAsync(
            IFormFile? file,
            long maxSizeBytes = DefaultMaxSizeBytes)
        {
            if (file == null || file.Length <= 0)
            {
                return FileValidationResult.Fail(
                    "الملف فارغ أو غير موجود.",
                    "The file is empty or missing.");
            }

            if (file.Length > maxSizeBytes)
            {
                return FileValidationResult.Fail(
                    $"حجم الملف يتجاوز الحد المسموح ({maxSizeBytes / (1024 * 1024)} ميجابايت).",
                    $"File size exceeds the allowed limit ({maxSizeBytes / (1024 * 1024)} MB).");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedSignatures.TryGetValue(extension, out var signatures))
            {
                return FileValidationResult.Fail(
                    "نوع الملف غير مسموح. الأنواع المسموحة فقط: PDF, JPG, JPEG, PNG.",
                    "File type not allowed. Only PDF, JPG, JPEG, PNG are accepted.");
            }

            await using var stream = file.OpenReadStream();
            var headerBytes = new byte[8];
            var readCount = await stream.ReadAsync(headerBytes.AsMemory(0, (int)Math.Min(8, file.Length)));

            bool matchesSignature = signatures.Any(sig =>
                readCount >= sig.Length && headerBytes.Take(sig.Length).SequenceEqual(sig));

            if (!matchesSignature)
            {
                return FileValidationResult.Fail(
                    "محتوى الملف لا يطابق امتداده الظاهري (قد يكون الملف معدَّلًا أو غير صالح).",
                    "The file content doesn't match its apparent extension (the file may be corrupted or disguised).");
            }

            return FileValidationResult.Ok();
        }
    }

    public class FileValidationResult
    {
        public bool IsValid { get; private init; }
        public string? ErrorAr { get; private init; }
        public string? ErrorEn { get; private init; }

        public static FileValidationResult Ok() => new() { IsValid = true };

        public static FileValidationResult Fail(string errorAr, string errorEn) =>
            new() { IsValid = false, ErrorAr = errorAr, ErrorEn = errorEn };

        public string CombinedErrorMessage => $"{ErrorAr} / {ErrorEn}";
    }
}
