namespace AgriculturalLoanSystem.Infrastructure;

/// <summary>Opaque document keys, stored outside the static-file tree.</summary>
public sealed class PrivateDocumentStore
{
    private readonly string _root;

    public PrivateDocumentStore(IWebHostEnvironment environment)
    {
        _root = Path.Combine(environment.ContentRootPath, "App_Data", "Documents");
    }

    public string? Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        // Existing database references remain valid after the offline file migration.
        if (key.StartsWith("/uploads/", StringComparison.Ordinal)) key = key[9..];
        var parts = key.Split('/');
        if (parts.Length != 2 || parts[0] is not ("identity_docs" or "screening_docs")) return null;
        var name = parts[1];
        if (name.Length == 0 || name is "." or ".." ||
            name.IndexOfAny(['\\', ':', '\0', '/', '%']) >= 0 ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        var path = Path.Combine(_root, parts[0], name);
        // Storage must not use symlinks/junctions to locations outside this tree.
        foreach (var candidate in new[] { Path.GetDirectoryName(_root)!, _root, Path.GetDirectoryName(path)!, path })
            if ((File.Exists(candidate) || Directory.Exists(candidate)) &&
                File.GetAttributes(candidate).HasFlag(FileAttributes.ReparsePoint)) return null;
        return path;
    }

    public async Task<string> SaveAsync(string category, IFormFile file)
    {
        var validation = await FileUploadValidator.ValidateAsync(file);
        if (!validation.IsValid) throw new ArgumentException(validation.CombinedErrorMessage);
        var key = $"{category}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        var path = Resolve(key) ?? throw new ArgumentException("Invalid document category.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(output);
        return key;
    }

    public void Delete(string? key)
    {
        var path = Resolve(key);
        if (path != null && File.Exists(path)) File.Delete(path);
    }
}
