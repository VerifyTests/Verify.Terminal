using System.Net;
using System.Net.Sockets;
using System.Text;
using DiffEngine;
using Environment = System.Environment;

namespace Verify.Terminal.IntegrationTests;

// A real inline queue owner, standing in for DiffEngineTray or DiffEngineViewer.
//
// Hosts DiffEngine's own InlineQueue behind DiffEngine's own wire protocol, on an ephemeral
// loopback port that DiffEngine_ViewerPort points every in-process client at. So the patch a
// failing Verify run produces arrives here over the same socket exchange it would arrive at a tray
// or viewer by, and Verify.Terminal's listing and accept run against an owner that behaves as they
// do, including applying an accepted patch with InlineApplier.
//
// The wire framing is mirrored here because it is internal to DiffEngine: line based, `name: value`,
// with every value base64. That duplication is deliberate pinning — a protocol change in DiffEngine
// breaks these tests loudly instead of silently stranding this tool's users.
public sealed class InlineQueueHost : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _loop;
    private readonly string? _previousPort;
    private readonly object _gate = new();

    private InlineQueue _queue = InlineQueue.Empty;

    public InlineQueueHost()
    {
        // Port 0 asks the OS to choose, so this never collides with a real tray or viewer.
        _listener = new(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint) _listener.LocalEndpoint).Port;

        _previousPort = Environment.GetEnvironmentVariable(DeadInlineQueue.PortVariable);
        Environment.SetEnvironmentVariable(DeadInlineQueue.PortVariable, port.ToString());

        _loop = Task.Run(Listen);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    private async Task Listen()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            // The client half-closes after writing, so the request is read to its end.
            var request = await reader.ReadToEndAsync();
            var response = Handle(request);
            var bytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(bytes, _cancellation.Token);
            await stream.FlushAsync(_cancellation.Token);
        }
    }

    private string Handle(string request)
    {
        string? verb = null;
        string? key = null;
        string? body = null;
        var versioned = false;

        foreach (var raw in request.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var separator = line.IndexOf(':');
            if (separator < 1)
            {
                continue;
            }

            var name = line[..separator];
            var value = line[(separator + 1)..].Trim();
            switch (name)
            {
                case "version":
                    versioned = value == "1";
                    break;
                case "verb":
                    verb = value;
                    break;
                case "key":
                    key = Decode(value);
                    break;
                case "body":
                    body = Decode(value);
                    break;
            }
        }

        if (!versioned || verb is null)
        {
            return Error("Unreadable request");
        }

        lock (_gate)
        {
            switch (verb)
            {
                case "inline":
                    if (body is null ||
                        !InlinePatchFile.TryParse(body, out var patch))
                    {
                        return Error("Unreadable patch");
                    }

                    _queue = _queue.Enqueue(patch);
                    return Ok();

                case "settle":
                    if (key is not null)
                    {
                        // The body carries the sending framework, so a multi targeted run only
                        // settles its own variant.
                        _queue = _queue.Settle(key, body);
                    }

                    return Ok();

                case "list":
                    return Listing(withPatches: false);

                case "listfull":
                    return Listing(withPatches: true);

                case "accept":
                {
                    if (key is null ||
                        _queue.Find(key) is null)
                    {
                        // No entry for the key: false with no message, per the owner contract.
                        return Error(null);
                    }

                    // The queue itself decides what accepting means, exactly as it does inside the
                    // tray and the viewer: a conflicted entry is refused, and the applier's outcome
                    // decides whether the entry goes or stays with a status.
                    var accepted = _queue.Accept(key, InlineApplier.Apply, out var message);
                    if (ReferenceEquals(accepted, _queue))
                    {
                        // Unchanged with a message is a refusal; nothing was attempted.
                        return Error(message);
                    }

                    _queue = accepted;
                    return Ok(message);
                }

                case "discard":
                {
                    if (key is null ||
                        _queue.Find(key) is null)
                    {
                        return Error(null);
                    }

                    _queue = _queue.Discard(key, out var message);
                    return Ok(message);
                }

                default:
                    // Focus, window verbs and anything newer: acknowledged, nothing to do.
                    return Ok();
            }
        }
    }

    private string Listing(bool withPatches)
    {
        var builder = new StringBuilder("version: 1\nstatus: ok\n");
        foreach (var entry in _queue.Items)
        {
            var status = entry.Status is null ? "" : Encode(entry.Status);
            var head = $"{Encode(entry.Key)}|{Encode(entry.Name)}|{status}";
            if (!withPatches)
            {
                builder.Append($"item: {head}\n");
                continue;
            }

            builder.Append($"full: {head}|{Origins(entry.Variants[0].Origins)}|{Encode(InlinePatchFile.Build(entry.Patch))}\n");
            foreach (var variant in entry.Variants.Skip(1))
            {
                builder.Append($"variant: {Encode(entry.Key)}|{Origins(variant.Origins)}|{Encode(InlinePatchFile.Build(variant.Patch))}\n");
            }
        }

        return builder.ToString();
    }

    private static string Ok(string? message = null)
    {
        if (message is null)
        {
            return "version: 1\nstatus: ok\n";
        }

        return $"version: 1\nstatus: ok\nmessage: {Encode(message)}\n";
    }

    private static string Error(string? message)
    {
        if (message is null)
        {
            return "version: 1\nstatus: error\n";
        }

        return $"version: 1\nstatus: error\nmessage: {Encode(message)}\n";
    }

    private static string Origins(IReadOnlyList<string> origins) =>
        origins.Count == 0 ? "" : Encode(string.Join(",", origins));

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string? Decode(string value) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(value));

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DeadInlineQueue.PortVariable, _previousPort);
        _cancellation.Cancel();
        _listener.Stop();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Shutdown races are not part of any scenario.
        }

        _cancellation.Dispose();
    }
}
