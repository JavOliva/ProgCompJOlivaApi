namespace ProgCompJOlivaApi.Utility;

public static class FileSystem
{
    public static void CopyDirectory(string sourceDir, string destDir, bool overwrite = true)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Directory.CreateDirectory(destDir);

        // Copiar archivos
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite);
        }

        // Copiar subdirectorios recursivamente
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);
            var destSubDir = Path.Combine(destDir, dirName);

            CopyDirectory(directory, destSubDir, overwrite);
        }
    }

    public static async Task<string> UploadOrganizationLogo(IFormFile logo, IWebHostEnvironment env)
    {
        if (logo == null || logo.Length == 0)
            throw new ArgumentException("Organization logo was not found");

        // wwwroot/organizations/logos
        var uploadsFolder = Path.Combine(
            env.WebRootPath,
            "organizations",
            "logos"
        );

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var extension = Path.GetExtension(logo.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await logo.CopyToAsync(stream);
        }

        return $"/organizations/logos/{fileName}";
    }

    public static async Task<string> ModifyOrganizationLogo(string previousUrl, IFormFile logo, IWebHostEnvironment env)
    {
        if (logo == null || logo.Length == 0)
            throw new ArgumentException($"Organization new logo was not found");

        var newLogoUrl = await UploadOrganizationLogo(logo, env);

        if (!string.IsNullOrWhiteSpace(previousUrl))
        {
            try
            {
                var previousPath = previousUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(env.WebRootPath, previousPath);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch
            {

            }
        }

        return newLogoUrl;
    }
}
