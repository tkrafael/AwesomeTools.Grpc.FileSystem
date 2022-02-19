// See https://aka.ms/new-console-template for more information
using AwesomeTools.Grpc.FileSystem;
using Grpc.Core;
using Grpc.Net.Client;

using var channel = GrpcChannel.ForAddress("http://127.0.0.1:41000");
var client = new FileSystem.FileSystemClient(channel);
while (true)
{
    var cmd = Console.ReadLine().Split(" ");
    if (cmd.Length == 0)
    {
        continue;
    }
    switch (cmd[0])
    {
        case "ls":
            await ListFiles(client);
            break;
        case "hash":
            await HashFile(cmd[1]); break;
    }
}
async Task HashFile(string filename)
{
    var ret = await client.CalculateHashAsync(new FileItem
    {
        FileName = filename
    });
}
async Task ListFiles(FileSystem.FileSystemClient fs)
{
    var ls = fs.ListFiles(new ListFileRequest());
    var files = ls.ResponseStream.ReadAllAsync();
    await foreach (var item in files)
    {
        Console.WriteLine($"{item.FileName}\t{item.Length}\t{item.LastModified}");
    }
}
Console.WriteLine("Hello, World!");
