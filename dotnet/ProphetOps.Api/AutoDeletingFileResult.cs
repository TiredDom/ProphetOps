using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ProphetOps.Api;

public sealed class AutoDeletingFileResult : IActionResult, IDisposable
{
    private readonly string _filePath;
    private readonly string _contentType;
    private readonly string _fileDownloadName;
    private int _disposed;

    public AutoDeletingFileResult(string filePath, string contentType, string fileDownloadName)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        _fileDownloadName = fileDownloadName ?? throw new ArgumentNullException(nameof(fileDownloadName));
    }

    public string FilePath => _filePath;
    public string ContentType => _contentType;
    public string FileDownloadName => _fileDownloadName;

    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.HttpContext.Response;
        response.Headers.CacheControl = "no-store";
        response.ContentType = _contentType;

        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(_fileDownloadName);
        response.Headers.ContentDisposition = disposition.ToString();

        FileStream? stream = null;
        try
        {
            try
            {
                stream = new FileStream(
                    _filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete,
                    bufferSize: 64 * 1024,
                    FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            }
            catch
            {
                Dispose();
                throw;
            }

            await stream.CopyToAsync(response.Body, 64 * 1024, context.HttpContext.RequestAborted);
        }
        finally
        {
            if (stream != null)
            {
                await stream.DisposeAsync();
            }
            Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            DeleteQuietly(_filePath);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
