using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WriteFluency.ExerciseTransfer;

internal sealed class KubectlClient(string? context, string kubernetesNamespace)
{
    public async Task<string> GetCurrentContextAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context))
        {
            return context;
        }

        return (await RunAsync(["config", "current-context"], cancellationToken)).Trim();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken)
    {
        var output = await RunAsync(
            BuildNamespacedArguments(["get", "secret", secretName, "-o", "json"]),
            cancellationToken);

        using var document = JsonDocument.Parse(output);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            throw new InvalidOperationException(
                $"Kubernetes secret '{secretName}' does not contain a data section.");
        }

        return data.EnumerateObject().ToDictionary(
            item => item.Name,
            item => Encoding.UTF8.GetString(Convert.FromBase64String(item.Value.GetString()!)),
            StringComparer.Ordinal);
    }

    public async Task<KubectlPortForward> StartPortForwardAsync(
        string service,
        int remotePort,
        CancellationToken cancellationToken)
    {
        var localPort = GetAvailablePort();
        var arguments = WithContext(BuildNamespacedArguments(
            ["port-forward", "--address", "127.0.0.1", $"service/{service}", $"{localPort}:{remotePort}"]))
            .ToArray();

        return await KubectlPortForward.StartAsync(arguments, localPort, cancellationToken);
    }

    private async Task<string> RunAsync(
        IReadOnlyCollection<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = CreateProcess(arguments);
        process.Start();

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"kubectl failed with exit code {process.ExitCode}: {error.Trim()}");
        }

        return output;
    }

    private Process CreateProcess(IReadOnlyCollection<string> arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "kubectl",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in WithContext(arguments))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        return process;
    }

    private IReadOnlyList<string> BuildNamespacedArguments(IReadOnlyCollection<string> arguments)
    {
        return ["--namespace", kubernetesNamespace, .. arguments];
    }

    private IEnumerable<string> WithContext(IEnumerable<string> arguments)
    {
        if (!string.IsNullOrWhiteSpace(context))
        {
            yield return "--context";
            yield return context;
        }

        foreach (var argument in arguments)
        {
            yield return argument;
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class KubectlPortForward : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output;

    private KubectlPortForward(Process process, int localPort, StringBuilder output)
    {
        _process = process;
        LocalPort = localPort;
        _output = output;
    }

    public int LocalPort { get; }

    public static async Task<KubectlPortForward> StartAsync(
        IReadOnlyCollection<string> arguments,
        int localPort,
        CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "kubectl",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var output = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => AppendOutput(output, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendOutput(output, eventArgs.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var portForward = new KubectlPortForward(process, localPort, output);
        await portForward.WaitUntilReadyAsync(cancellationToken);
        return portForward;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and shutdown.
        }

        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(20));

        while (!timeoutSource.IsCancellationRequested)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"kubectl port-forward exited before becoming ready: {_output}");
            }

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, LocalPort, timeoutSource.Token);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(150, timeoutSource.Token);
            }
        }

        throw new TimeoutException($"Timed out starting kubectl port-forward: {_output}");
    }

    private static void AppendOutput(StringBuilder output, string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            output.AppendLine(line);
        }
    }
}
