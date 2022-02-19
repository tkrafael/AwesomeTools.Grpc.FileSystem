using System.Buffers;
using System.Security.Cryptography;
using AwesomeTools.Grpc.FileSystem;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

internal interface IFileSystem
{
    Task<Stream> OpenRead(string filename);
    IAsyncEnumerable<FileEntry> ListFiles(string path);
    ValueTask DeleteFile(string filename);
}
internal class FileEntry
{
    public string FullName { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }

    public long Length { get; set; }
}
internal class PhysicalPathRootedFileSystem : IFileSystem
{
    private readonly string virtualPath;
    private readonly string basePath;

    public PhysicalPathRootedFileSystem(string virtualPath, string basePath)
    {
        this.virtualPath = virtualPath;
        this.basePath = basePath;
    }

    public ValueTask DeleteFile(string filename)
    {
        File.Delete(ResolveFullName(filename));
        return new ValueTask();
    }

    public IAsyncEnumerable<FileEntry> ListFiles(string path)
    {
        return EnumerateFilesRecursively(ResolveFullName(path)).ToAsyncEnumerable();
    }

    private IEnumerable<FileEntry> EnumerateFilesRecursively(string path)
    {
        var thoseFiles = Directory.EnumerateFiles(path).Select(a =>
        {
            var realFile = new FileInfo(a);
            return new FileEntry
            {
                FullName = ResolveVirtualName(a),
                LastWriteTimeUtc = realFile.LastWriteTimeUtc,
                Length = realFile.Length
            };
        });
        var filesInDir = Directory.EnumerateDirectories(path).SelectMany(EnumerateFilesRecursively);
        return thoseFiles.Concat(filesInDir);
    }

    public Task<Stream> OpenRead(string filename)
    {
        return Task.FromResult((Stream)File.OpenRead(ResolveFullName(filename)));
    }
    private string ResolveFullName(string input)
    {
        return Path.Combine(this.basePath, input.Substring(this.virtualPath.Length));
    }

    private string ResolveVirtualName(string input)
    {
        return Path.Combine(this.virtualPath, input.Substring(this.basePath.Length));
    }
}
internal class ServerFileSystem : FileSystem.FileSystemBase
{
    private readonly Dictionary<Guid, (IFileSystem filesystem, string virtualpath, string realpath)> providers;

    public ServerFileSystem(params (string virtualpath, string realpath)[] paths)
    {
        this.providers = paths.Select(a =>
        (filesystem:
            (IFileSystem)new PhysicalPathRootedFileSystem(a.virtualpath, a.realpath),
            a.virtualpath,
            a.realpath
        )
        ).ToDictionary(a => Guid.NewGuid(), a => a);
    }
    public override async Task<CalculateHashResponse> CalculateHash(FileItem request, global::Grpc.Core.ServerCallContext context)
    {
        // TODO: find virtual path from constructor field
        var path = Path.Combine(request.FileName);
        using var md5 = MD5.Create();
        using var fs = File.OpenRead(path);

        var hash = await md5.ComputeHashAsync(fs);
        return new CalculateHashResponse
        {
            Hash = string.Join("", hash.Select(a => a.ToString("X2")))
        };
    }

    public override Task<DeleteFileResponse> DeleteFile(FileItem request, global::Grpc.Core.ServerCallContext context)
    {
        var provider = this.providers[Guid.Parse(request.FileProvider)];
        provider.filesystem.DeleteFile(request.FileName);
        return Task.FromResult(new DeleteFileResponse
        {

        });
    }

    public override async Task ListFiles(ListFileRequest request, global::Grpc.Core.IServerStreamWriter<FileItem> responseStream, global::Grpc.Core.ServerCallContext context)
    {
        var allFiles = this.providers.Values.ToAsyncEnumerable().SelectMany(a => a.filesystem.ListFiles(a.virtualpath).Select(s => (cfg: a, file: s)));

        await foreach (var path in allFiles)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                break;
            }

            var now = DateTime.UtcNow.AddMinutes(-request.OlderThanMinutes);
            if (path.file.LastWriteTimeUtc > now)
            {
                continue;
            }
            await responseStream.WriteAsync(new FileItem
            {
                FileName = path.file.FullName,
                LastModified = Timestamp.FromDateTime(path.file.LastWriteTimeUtc),
                Length = path.file.Length
            });
        }
    }

    public override async Task OpenFileData(FileItem request, global::Grpc.Core.IServerStreamWriter<FileData> responseStream, global::Grpc.Core.ServerCallContext context)
    {
        var provider = this.providers[Guid.Parse(request.FileProvider)];
        var filename = Path.Combine(provider.realpath, request.FileName.Substring(provider.virtualpath.Length));
        using var file = await provider.filesystem.OpenRead(request.FileName);
        var d = new FileData();
        using var buffer = MemoryPool<byte>.Shared.Rent(32_768);
        if (file.Length > 65_535)
        {
            responseStream.WriteOptions = new WriteOptions(WriteFlags.BufferHint | WriteFlags.NoCompress);
        }

        while (!context.CancellationToken.IsCancellationRequested)
        {
            var read = await file.ReadAsync(buffer.Memory);
            if (read == 0)
            {
                break;
            }
            d.Data = ByteString.CopyFrom(buffer.Memory.Span.Slice(0, read));
            await responseStream.WriteAsync(d);
        }
    }
}
