namespace JobMailer.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SavePdfAsync(Stream pdfStream, string fileName, CancellationToken ct = default);
    Task<byte[]> GetPdfBytesAsync(string storagePath, CancellationToken ct = default);
}
