using Ecosologic.Api.Media;

namespace Ecosologic.Api.Tests;

public sealed class LocalMediaStorageTests
{
    [Fact]
    public async Task Save_writes_content_to_final_path_without_temp_file()
    {
        var tempDir = NewTempDir();
        try
        {
            var storage = new LocalMediaStorage(tempDir);
            var payload = new byte[] { 1, 2, 3, 4 };

            var name = await storage.SaveAsync(new MemoryStream(payload), ".png", CancellationToken.None);

            Assert.EndsWith(".png", name);
            Assert.DoesNotContain(".tmp", name);
            var files = Directory.GetFiles(tempDir);
            Assert.Equal([Path.Combine(tempDir, name)], files);
            Assert.Equal(payload, File.ReadAllBytes(Path.Combine(tempDir, name)));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task Save_removes_temp_file_on_failure()
    {
        var tempDir = NewTempDir();
        try
        {
            var storage = new LocalMediaStorage(tempDir);

            await Assert.ThrowsAsync<IOException>(() =>
                storage.SaveAsync(new FailingStream(), ".png", CancellationToken.None));

            Assert.Empty(Directory.GetFiles(tempDir));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task Save_removes_temp_file_on_cancellation()
    {
        var tempDir = NewTempDir();
        try
        {
            var storage = new LocalMediaStorage(tempDir);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                storage.SaveAsync(new MemoryStream(new byte[4096]), ".png", cts.Token));

            Assert.Empty(Directory.GetFiles(tempDir));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task Save_removes_temp_file_when_cancelled_after_copy_completes()
    {
        var tempDir = NewTempDir();
        try
        {
            var storage = new LocalMediaStorage(tempDir);
            using var cts = new CancellationTokenSource();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                storage.SaveAsync(new CancelOnEofStream(new byte[4096], cts), ".png", cts.Token));

            Assert.Empty(Directory.GetFiles(tempDir));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task Save_preserves_pre_existing_file_when_move_fails()
    {
        var tempDir = NewTempDir();
        try
        {
            const string name = "fixed.png";
            Directory.CreateDirectory(tempDir);
            var preExistingPath = Path.Combine(tempDir, name);
            File.WriteAllBytes(preExistingPath, [9, 9, 9]);

            var storage = new LocalMediaStorage(tempDir, _ => name);

            await Assert.ThrowsAsync<IOException>(() =>
                storage.SaveAsync(new MemoryStream([1, 2, 3, 4]), ".png", CancellationToken.None));

            Assert.Equal([9, 9, 9], File.ReadAllBytes(preExistingPath));
            Assert.Equal([preExistingPath], Directory.GetFiles(tempDir));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), "ecosologic-media-" + Guid.NewGuid().ToString("N"));

    private static void DeleteTempDir(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            throw new IOException("boom");
        }
    }

    private sealed class CancelOnEofStream(byte[] payload, CancellationTokenSource cts) : Stream
    {
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            if (_offset >= payload.Length)
            {
                cts.Cancel();
                return 0;
            }

            var count = Math.Min(buffer.Length, payload.Length - _offset);
            payload.AsSpan(_offset, count).CopyTo(buffer.Span);
            _offset += count;
            return count;
        }
    }
}
