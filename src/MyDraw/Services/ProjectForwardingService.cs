using System.IO.Pipes;
using System.Text;

namespace Com.StudentTools.MyDraw.Services;

public sealed class ProjectForwardingService : IDisposable
{
    private static readonly string PipeName =
        $"MyDraw.OpenProject.{Environment.UserName.Replace('\\', '_')}";
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;

    public event EventHandler<string>? ProjectPathReceived;

    public void Start()
    {
        _listener ??= Task.Run(ListenAsync);
    }

    public static bool TrySend(string filePath)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using NamedPipeClientStream client = new(
                    ".",
                    PipeName,
                    PipeDirection.Out,
                    PipeOptions.None);
                client.Connect(100);
                using StreamWriter writer = new(client, new UTF8Encoding(false));
                writer.WriteLine(filePath);
                writer.Flush();
                return true;
            }
            catch (Exception exception) when (exception is
                IOException or TimeoutException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }

        return false;
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                using NamedPipeServerStream server = new(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cancellation.Token);
                using StreamReader reader = new(server, Encoding.UTF8);
                string? filePath = await reader.ReadLineAsync(_cancellation.Token);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    ProjectPathReceived?.Invoke(this, filePath);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(100, _cancellation.Token);
            }
        }
    }
}
