using Grpc.Core;

namespace Bemobi.ProtocolBuffers.FileSystem.Server;

public class Worker : IHostedService
{
    private readonly ILogger<Worker> _logger;

    private Func<Task> Stop;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var s = new ServerFileSystem((virtualpath: "/myfiles/", realpath: "/workspaces/"));
        var server = new Grpc.Core.Server
        {
            Services = { AwesomeTools.Grpc.FileSystem.FileSystem.BindService(s) },
            Ports = { new ServerPort("0.0.0.0", 41000, ServerCredentials.Insecure) },
        };
        server.Start();
        this.Stop = server.ShutdownAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return this.Stop();
    }
}
