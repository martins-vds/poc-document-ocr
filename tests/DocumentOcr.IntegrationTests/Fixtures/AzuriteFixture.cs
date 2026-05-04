using System.Net.Sockets;

namespace DocumentOcr.IntegrationTests.Fixtures;

/// <summary>
/// Detects whether Azurite is reachable on the standard developer ports
/// (10000 blob / 10001 queue). Tests gated by <see cref="RequiresAzuriteFact"/>
/// are skipped when the emulator is not running so the suite stays green on
/// machines without the local toolchain.
/// </summary>
public sealed class AzuriteFixture
{
    public const string ConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;" +
        "QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;" +
        "TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";

    public bool IsAvailable { get; }

    public AzuriteFixture()
    {
        IsAvailable = TryConnect("127.0.0.1", 10000) && TryConnect("127.0.0.1", 10001);
    }

    private static bool TryConnect(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var ok = client.ConnectAsync(host, port).Wait(TimeSpan.FromMilliseconds(500));
            return ok && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
