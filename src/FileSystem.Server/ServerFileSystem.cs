using AwesomeTools.Grpc.FileSystem;

internal class ServerFileSystem : FileSystem.FileSystemBase
{
    public ServerFileSystem(string basePath)
    {

    }
    public override Task<CalculateHashResponse> CalculateHash(FileItem request, global::Grpc.Core.ServerCallContext context)
    {
        return base.CalculateHash(request, context);
    }

    public override Task<DeleteFileResponse> DeleteFile(FileItem request, global::Grpc.Core.ServerCallContext context)
    {
        return base.DeleteFile(request, context);
    }

    public override Task ListFiles(ListFileRequest request, global::Grpc.Core.IServerStreamWriter<FileItem> responseStream, global::Grpc.Core.ServerCallContext context)
    {
        return base.ListFiles(request, responseStream, context);
    }

    public override Task OpenFileData(FileItem request, global::Grpc.Core.IServerStreamWriter<FileData> responseStream, global::Grpc.Core.ServerCallContext context)
    {
        return base.OpenFileData(request, responseStream, context);
    }
}
