using System.Net.Sockets;
using Microsoft.Azure.Cosmos;

namespace DocumentOcr.IntegrationTests.Fixtures;

/// <summary>
/// Detects whether the Cosmos DB Emulator is reachable on the default
/// developer endpoint (https://localhost:8081). When available, exposes a
/// <see cref="CosmosClient"/> bound to the well-known emulator key, so each
/// test can create / drop its own throwaway database.
/// </summary>
public sealed class CosmosEmulatorFixture : IAsyncLifetime
{
    public const string EmulatorEndpoint = "https://localhost:8081/";
    public const string EmulatorKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    public bool IsAvailable { get; private set; }
    public CosmosClient? Client { get; private set; }

    public Task InitializeAsync()
    {
        if (!TryConnect("localhost", 8081))
        {
            IsAvailable = false;
            return Task.CompletedTask;
        }

        try
        {
            Client = new CosmosClient(
                EmulatorEndpoint,
                EmulatorKey,
                new CosmosClientOptions
                {
                    HttpClientFactory = () =>
                    {
                        var handler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                        };
                        return new HttpClient(handler);
                    },
                    ConnectionMode = ConnectionMode.Gateway,
                });
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
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
