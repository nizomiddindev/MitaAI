using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MitaAI.Plugin;

internal sealed record VoiceResult(VoiceAudio? Audio, string Error = "", long GenerationMs = 0);

internal enum VoiceWorkerState { Stopped, Starting, Ready, Unavailable }

internal sealed record VoiceWorkerOptions
{
    internal TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(180);
    internal TimeSpan SynthesisTimeout { get; init; } = TimeSpan.FromSeconds(45);
    internal TimeSpan FailureRetryDelay { get; init; } = TimeSpan.FromSeconds(60);
    internal TimeSpan StopRetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    // Consecutive failures before ready; afterwards no automatic restart until voice is disabled or the game restarts.
    internal int MaxStartupFailures { get; init; } = 3;
    // Test seams. Production launches the F5 worker and kills its process tree.
    internal Func<string, ProcessStartInfo>? CreateStartInfo { get; init; }
    internal Action<Process>? Terminate { get; init; }
}

internal sealed class LocalVoiceClient : IDisposable
{
    private static readonly Regex SafeCode = new("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant);
    private readonly object sync = new();
    private readonly Action<string> log;
    private readonly VoiceWorkerOptions options;
    private Process? process;
    private TaskCompletionSource<bool>? ready;
    private TaskCompletionSource<VoiceResult>? pending;
    private string pendingId = "";
    private DateTime retryAfter;
    private int startupFailures;
    private bool unavailable;
    private Task cleanup = Task.CompletedTask;
    private bool disposed;
    internal bool IsReady { get { lock (sync) return ready?.Task.IsCompletedSuccessfully == true && ready.Task.Result; } }

    internal VoiceWorkerState State
    {
        get
        {
            lock (sync)
            {
                if (unavailable) return VoiceWorkerState.Unavailable;
                if (process == null) return VoiceWorkerState.Stopped;
                return IsReady ? VoiceWorkerState.Ready : VoiceWorkerState.Starting;
            }
        }
    }

    // Termination of the previous worker; a replacement never starts until it completes.
    internal Task Cleanup { get { lock (sync) return cleanup; } }

    internal LocalVoiceClient(Action<string> logger, VoiceWorkerOptions? workerOptions = null)
    {
        log = logger;
        options = workerOptions ?? new VoiceWorkerOptions();
    }

    internal void EnsureStarted(string projectDirectory)
    {
        lock (sync)
        {
            if (disposed || unavailable || process != null || !cleanup.IsCompleted || DateTime.UtcNow < retryAfter) return;
            Process? owned = null;
            try
            {
                var info = (options.CreateStartInfo ?? CreateWorkerStartInfo)(projectDirectory);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
                info.StandardInputEncoding = new UTF8Encoding(false);
                info.StandardOutputEncoding = info.StandardErrorEncoding = Encoding.UTF8;
                owned = new Process { StartInfo = info };
                if (!owned.Start()) throw new IOException();
                ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
                process = owned;
                log("MITAAI_VOICE_STARTING engine=f5-russian-v2 device=cuda");
                _ = Task.Run(() => ReadResponses(owned));
                // Engine progress can contain text. Drain it without logging conversation data.
                _ = Task.Run(async () => { try { while (await owned.StandardError.ReadLineAsync() != null) { } } catch { } });
                _ = WatchStartup(owned, ready.Task);
            }
            catch (Exception error)
            {
                owned?.Dispose();
                RecordStartupFailure("start", error is FileNotFoundException ? "worker_files_missing" : error.GetType().Name);
            }
        }
    }

    private static ProcessStartInfo CreateWorkerStartInfo(string projectDirectory)
    {
        string root = Path.GetFullPath(projectDirectory);
        string voice = Path.Combine(root, "local", "voice");
        string python = Path.Combine(voice, ".venv-f5-gpu", "Scripts", "python.exe");
        string script = Path.Combine(root, "tools", "voice-evaluation", "voice_worker.py");
        if (!File.Exists(python) || !File.Exists(script)) throw new FileNotFoundException();
        Directory.CreateDirectory(Path.Combine(voice, "tmp"));
        var info = new ProcessStartInfo(python) { WorkingDirectory = root };
        info.ArgumentList.Add("-B");
        info.ArgumentList.Add(script);
        info.Environment["PYTHONNOUSERSITE"] = "1";
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["TEMP"] = Path.Combine(voice, "tmp");
        info.Environment["TMP"] = Path.Combine(voice, "tmp");
        info.Environment["PYTHONPYCACHEPREFIX"] = Path.Combine(voice, "cache", "python-bytecode");
        return info;
    }

    // Caller holds sync.
    private void RecordStartupFailure(string stage, string code)
    {
        startupFailures++;
        if (startupFailures >= options.MaxStartupFailures)
        {
            unavailable = true;
            log($"MITAAI_VOICE_UNAVAILABLE stage={stage} error={code} failures={startupFailures} autoRetry=False recovery=fix-cause-then-disable-voice-or-restart-game");
            return;
        }
        retryAfter = DateTime.UtcNow + options.FailureRetryDelay;
        log($"MITAAI_VOICE_START_FAILED stage={stage} error={code} failures={startupFailures} retryInSeconds={(int)options.FailureRetryDelay.TotalSeconds}");
    }

    internal async Task<bool> WaitUntilReadyAsync(TimeSpan timeout)
    {
        Task<bool>? task;
        lock (sync) task = ready?.Task;
        if (task == null) return false;
        try { return await task.WaitAsync(timeout).ConfigureAwait(false); }
        catch { return false; }
    }

    private async Task WatchStartup(Process owned, Task<bool> task)
    {
        try { await task.WaitAsync(options.StartupTimeout).ConfigureAwait(false); }
        catch { StopOwned(owned, "startup-timeout"); }
    }

    internal async Task<VoiceResult> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        Process owned;
        TaskCompletionSource<VoiceResult> completion;
        string id;
        lock (sync)
        {
            if (disposed || !IsReady || process == null) return new(null, "warming-or-unavailable");
            if (cancellationToken.IsCancellationRequested) return new(null, "cancelled-before-submit");
            if (pending != null) return new(null, "busy");
            if (string.IsNullOrWhiteSpace(text) || text.Length > 600) return new(null, "invalid-text");
            owned = process;
            id = pendingId = Guid.NewGuid().ToString("N");
            completion = pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        bool submitted = false;
        CancellationTokenSource? timeout = null;
        try
        {
            timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Nothing has reached the worker yet, so an early cancellation leaves it warm.
            if (timeout.IsCancellationRequested) return new(null, "cancelled-before-submit");
            timeout.CancelAfter(options.SynthesisTimeout);
            // From here a partial frame may exist; only stopping the worker keeps the protocol safe.
            submitted = true;
            await owned.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, text })).ConfigureAwait(false);
            await owned.StandardInput.FlushAsync().ConfigureAwait(false);
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (submitted)
        {
            StopOwned(owned, cancellationToken.IsCancellationRequested ? "cancelled" : "synthesis-timeout");
            return new(null, "cancelled-or-timeout");
        }
        catch (Exception) when (submitted)
        {
            StopOwned(owned, "transport");
            return new(null, "worker-unavailable");
        }
        catch (Exception)
        {
            return new(null, "cancelled-before-submit");
        }
        finally
        {
            timeout?.Dispose();
            lock (sync) if (pending == completion) { pending = null; pendingId = ""; }
        }
    }

    private async Task ReadResponses(Process owned)
    {
        try
        {
            while (await owned.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > 4_100_000) throw new InvalidDataException();
                using var document = JsonDocument.Parse(line);
                var message = document.RootElement;
                string type = message.GetProperty("type").GetString() ?? "";
                if (type == "fatal")
                {
                    StopOwned(owned, "worker-fatal", ErrorCode(message));
                    return;
                }
                if (type == "ready")
                {
                    if (message.GetProperty("protocol").GetInt32() != 1) throw new InvalidDataException();
                    lock (sync)
                    {
                        if (process != owned) return;
                        ready?.TrySetResult(true);
                        startupFailures = 0;
                    }
                    log("MITAAI_VOICE_READY engine=f5-russian-v2");
                    continue;
                }
                if (type != "audio") throw new InvalidDataException();
                TaskCompletionSource<VoiceResult>? completion;
                lock (sync)
                {
                    if (process != owned || pending == null || message.GetProperty("id").GetString() != pendingId) continue;
                    completion = pending;
                }
                VoiceResult result = message.GetProperty("ok").GetBoolean()
                    ? new(VoiceAudio.Decode(Convert.FromBase64String(message.GetProperty("wav").GetString()!)),
                        GenerationMs: message.GetProperty("generationMs").GetInt64())
                    : new(null, "synthesis-failed:" + ErrorCode(message));
                completion.TrySetResult(result);
            }
            StopOwned(owned, "worker-exited");
        }
        catch { StopOwned(owned, "worker-protocol-or-runtime"); }
    }

    // Worker codes are identifiers only; anything else is reported as unknown rather than echoed.
    private static string ErrorCode(JsonElement message)
    {
        foreach (string name in new[] { "code", "error" })
        {
            if (message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                && value.GetString() is { } code && SafeCode.IsMatch(code)) return code;
        }
        return "unknown";
    }

    private void StopOwned(Process owned, string reason, string code = "")
    {
        lock (sync)
        {
            if (process != owned) return;
            bool wasReady = IsReady;
            process = null;
            ready?.TrySetResult(false);
            ready = null;
            pending?.TrySetResult(new(null, reason));
            pending = null;
            pendingId = "";
            // Killing a process tree and disposing its pipes can be slow; never do it on the caller's (Unity) thread.
            cleanup = Task.Run(() => Terminate(owned, reason));
            if (reason is "cancelled" or "disabled" or "shutdown" or "plugin-failed")
                retryAfter = DateTime.UtcNow + options.StopRetryDelay;
            else if (!wasReady) RecordStartupFailure(reason, code.Length > 0 ? code : reason);
            else retryAfter = DateTime.UtcNow + options.FailureRetryDelay;
        }
    }

    private void Terminate(Process owned, string reason)
    {
        try { (options.Terminate ?? TerminateTree)(owned); }
        catch { }
        try { owned.Dispose(); } catch { }
        log("MITAAI_VOICE_STOPPED reason=" + reason);
    }

    internal static void TerminateTree(Process owned)
    {
        try { if (!owned.HasExited) owned.Kill(entireProcessTree: true); } catch { }
        // Bounded wait so a replacement worker cannot overlap this one's GPU memory.
        try { owned.WaitForExit(5000); } catch { }
    }

    public void Dispose()
    {
        Process? owned;
        lock (sync) { disposed = true; owned = process; }
        if (owned != null) StopOwned(owned, "shutdown");
        // Quit only: a bounded wait lets the background kill finish before the runtime exits.
        try { Cleanup.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }

    internal void Stop(string reason = "disabled")
    {
        Process? owned;
        lock (sync)
        {
            owned = process;
            // Disabling voice is the in-session recovery path after the startup retry limit.
            if (reason == "disabled" && (unavailable || startupFailures > 0))
            {
                unavailable = false;
                startupFailures = 0;
                retryAfter = default;
                log("MITAAI_VOICE_RETRY_RESET reason=disabled");
            }
        }
        if (owned != null) StopOwned(owned, reason);
    }
}
