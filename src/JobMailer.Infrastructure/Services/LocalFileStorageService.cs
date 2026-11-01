using JobMailer.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;

namespace JobMailer.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;

    public LocalFileStorageService(IHostEnvironment environment)
    {
        _storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "Resumes");
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> SavePdfAsync(Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        var sanitizedFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
        var destinationPath = Path.Combine(_storageDirectory, uniqueFileName);

        using (var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await pdfStream.CopyToAsync(outputStream, ct);
        }

        return destinationPath;
    }

    public async Task<byte[]> GetPdfBytesAsync(string storagePath, CancellationToken ct = default)
    {
        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"Resume file not found at path: {storagePath}", storagePath);
        }

        return await File.ReadAllBytesAsync(storagePath, ct);
    }
}
