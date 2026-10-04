namespace ProphetOps.Api;

public sealed class QuotaBoundedWriteStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _baseBytes;
    private readonly long _maxTotalBytes;
    private readonly string _operationDescription;
    private long _bytesWritten;

    public QuotaBoundedWriteStream(Stream innerStream, long baseBytes, long maxTotalBytes, string operationDescription)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _baseBytes = Math.Max(0, baseBytes);
        if (maxTotalBytes <= 0)
        {
            throw new InvalidOperationException("Backup staging quota must be a positive number of bytes.");
        }
        _maxTotalBytes = maxTotalBytes;
        _operationDescription = operationDescription;

        if (_baseBytes > _maxTotalBytes)
        {
            throw new InvalidOperationException(
                $"Backup staging hard quota exceeded before {_operationDescription}: staging size ({_baseBytes} bytes) exceeded configured limit of {_maxTotalBytes} bytes.");
        }
    }

    public long BytesWritten => _bytesWritten;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _innerStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void WriteByte(byte value)
    {
        CheckQuota(1);
        _innerStream.WriteByte(value);
        _bytesWritten += 1;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckQuota(count);
        _innerStream.Write(buffer, offset, count);
        _bytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CheckQuota(buffer.Length);
        _innerStream.Write(buffer);
        _bytesWritten += buffer.Length;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        CheckQuota(count);
        await _innerStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        _bytesWritten += count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        CheckQuota(buffer.Length);
        await _innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _bytesWritten += buffer.Length;
    }

    private void CheckQuota(int incomingBytes)
    {
        if (incomingBytes <= 0) return;
        if (_baseBytes + _bytesWritten + incomingBytes > _maxTotalBytes)
        {
            var projected = _baseBytes + _bytesWritten + incomingBytes;
            throw new InvalidOperationException(
                $"Backup staging hard quota exceeded during {_operationDescription}: projected staging size ({projected} bytes) exceeded configured limit of {_maxTotalBytes} bytes.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _innerStream.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
