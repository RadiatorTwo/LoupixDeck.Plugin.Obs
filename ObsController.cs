using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;

namespace LoupixDeck.Plugin.Obs;

/// <summary>The three recording states OBS can be in.</summary>
public enum ObsRecordState
{
    Stopped,
    Recording,
    Paused
}

/// <summary>Thin wrapper over obs-websocket-dotnet used by the OBS plugin.</summary>
public interface IObsController
{
    /// <summary>Raised when the websocket connection is established or lost.</summary>
    event Action<bool> ConnectionChanged;

    /// <summary>Raised when OBS starts, stops, pauses or resumes recording.</summary>
    event Action<ObsRecordState> RecordStateChanged;

    /// <summary>Raised when the replay buffer is started or stopped.</summary>
    event Action<bool> ReplayBufferActiveChanged;

    /// <summary>Raised when the virtual camera is started or stopped.</summary>
    event Action<bool> VirtualCamActiveChanged;

    /// <summary>Raised when streaming is started or stopped.</summary>
    event Action<bool> StreamActiveChanged;

    /// <summary>Raised when studio mode is enabled or disabled.</summary>
    event Action<bool> StudioModeChanged;

    /// <summary>Sets the connection parameters used by subsequent connects.</summary>
    void Configure(string ip, int port, string password);

    /// <summary>Connects in the background (fire and forget).</summary>
    void Connect();

    /// <summary>Connects and waits for the result, with a short timeout.</summary>
    Task ConnectAndWaitAsync(CancellationToken cancellationToken = default);

    void Disconnect();

    Task ToggleVirtualCamera();
    Task ToggleRecording();
    Task StartRecording();
    Task StopRecording();
    Task PauseRecording();
    Task ToggleReplayBuffer();
    Task StartReplayBuffer();
    Task StopReplayBuffer();
    Task SaveReplayBuffer();
    Task SetScene(string sceneName);
    Task<List<SceneBasicInfo>> GetScenes();

    Task ToggleStream();
    Task StartStream();
    Task StopStream();

    Task ToggleStudioMode();
    Task SetPreviewScene(string sceneName);
    Task TriggerTransition();

    Task SetInputMuted(string inputName, bool muted);
    Task ToggleInputMute(string inputName);

    /// <summary>Names of all inputs OBS knows, for the dynamic "Audio" submenu.</summary>
    Task<List<string>> GetInputNames();

    /// <summary>
    /// Shows or hides <paramref name="sourceName"/>. An empty or placeholder
    /// <paramref name="sceneName"/> means the current program scene.
    /// </summary>
    Task SetSourceVisible(string sourceName, string sceneName, bool visible);

    /// <inheritdoc cref="SetSourceVisible"/>
    Task ToggleSource(string sourceName, string sceneName);

    /// <summary>Names of the sources in <paramref name="sceneName"/>, for the dynamic "Sources" submenu.</summary>
    Task<List<string>> GetSourceNames(string sceneName);
}

/// <inheritdoc cref="IObsController"/>
public sealed class ObsController : IObsController
{
    private readonly OBSWebsocket _obs = new();

    private string _ip = "127.0.0.1";
    private int _port = 4455;
    private string _password = string.Empty;

    private string Url => $"ws://{_ip}:{_port}";

    /// <summary>Scene-parameter value meaning "whatever is on program right now".</summary>
    public const string CurrentScenePlaceholder = "<current>";

    public event Action<bool>? ConnectionChanged;
    public event Action<ObsRecordState>? RecordStateChanged;
    public event Action<bool>? ReplayBufferActiveChanged;
    public event Action<bool>? VirtualCamActiveChanged;
    public event Action<bool>? StreamActiveChanged;
    public event Action<bool>? StudioModeChanged;

    /// <summary>
    /// Subscribes to the OBS state events once, for the lifetime of this controller.
    /// Doing it per connect would stack up duplicate handlers on every reconnect.
    /// </summary>
    public ObsController()
    {
        _obs.Connected += (_, _) =>
        {
            Raise(ConnectionChanged, true);
            // Off the websocket callback thread: the sync calls back into OBS.
            _ = Task.Run(SyncState);
        };

        _obs.Disconnected += (_, _) => ReportDisconnected();

        _obs.RecordStateChanged += (_, e) =>
        {
            ObsRecordState? state = e.OutputState.State switch
            {
                OutputState.OBS_WEBSOCKET_OUTPUT_STARTED => ObsRecordState.Recording,
                OutputState.OBS_WEBSOCKET_OUTPUT_RESUMED => ObsRecordState.Recording,
                OutputState.OBS_WEBSOCKET_OUTPUT_PAUSED => ObsRecordState.Paused,
                OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED => ObsRecordState.Stopped,
                // STARTING / STOPPING are transitional — wait for the final state.
                _ => null
            };

            if (state.HasValue)
                Raise(RecordStateChanged, state.Value);
        };

        _obs.ReplayBufferStateChanged += (_, e) => RaiseOnFinalState(ReplayBufferActiveChanged, e.OutputState);
        _obs.VirtualcamStateChanged += (_, e) => RaiseOnFinalState(VirtualCamActiveChanged, e.OutputState);
        _obs.StreamStateChanged += (_, e) => RaiseOnFinalState(StreamActiveChanged, e.OutputState);
        _obs.StudioModeStateChanged += (_, e) => Raise(StudioModeChanged, e.StudioModeEnabled);
    }

    /// <summary>
    /// Reads the current state of every tracked output and republishes it. Called after a
    /// successful connect so the deck shows the truth even when OBS was already recording
    /// (or LoupixDeck was restarted mid-session).
    /// </summary>
    private void SyncState()
    {
        try
        {
            RecordingStatus record = _obs.GetRecordStatus();
            Raise(RecordStateChanged, record.IsRecording
                ? (record.IsRecordingPaused ? ObsRecordState.Paused : ObsRecordState.Recording)
                : ObsRecordState.Stopped);

            Raise(ReplayBufferActiveChanged, _obs.GetReplayBufferStatus());
            Raise(VirtualCamActiveChanged, _obs.GetVirtualCamStatus().IsActive);
            Raise(StreamActiveChanged, _obs.GetStreamStatus().IsActive);
            Raise(StudioModeChanged, _obs.GetStudioModeEnabled());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading initial OBS state: {ex.Message}");
        }
    }

    /// <summary>Reports every tracked output as inactive — nothing is running once OBS is gone.</summary>
    private void ReportDisconnected()
    {
        Raise(ConnectionChanged, false);
        Raise(RecordStateChanged, ObsRecordState.Stopped);
        Raise(ReplayBufferActiveChanged, false);
        Raise(VirtualCamActiveChanged, false);
        Raise(StreamActiveChanged, false);
        Raise(StudioModeChanged, false);
    }

    private static void RaiseOnFinalState(Action<bool>? handler, OutputStateChanged state)
    {
        switch (state.State)
        {
            case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                Raise(handler, true);
                break;
            case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                Raise(handler, false);
                break;
            // STARTING / STOPPING are transitional — wait for the final state.
        }
    }

    /// <summary>Invokes a state handler without letting a subscriber's failure reach the websocket.</summary>
    private static void Raise<T>(Action<T>? handler, T value)
    {
        try
        {
            handler?.Invoke(value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error dispatching OBS state change: {ex.Message}");
        }
    }

    public void Configure(string ip, int port, string password)
    {
        _ip = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
        _port = port > 0 ? port : 4455;
        _password = password ?? string.Empty;
    }

    public void Connect()
    {
        if (_obs.IsConnected)
            Disconnect();

        // Fire-and-forget, but routed through a guarded async method so a failed
        // connection never escapes as an unobserved task exception.
        _ = ConnectGuardedAsync();
    }

    private async Task ConnectGuardedAsync()
    {
        try
        {
            if (await IsObsReachableAsync(CancellationToken.None).ConfigureAwait(false))
                _obs.ConnectAsync(Url, _password);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to OBS: {ex.Message}");
        }
    }

    /// <summary>
    /// Probes whether an OBS websocket server is actually listening before calling
    /// <see cref="OBSWebsocket.ConnectAsync"/>. The underlying websocket client starts the
    /// connection on a background task and rethrows a refused connection from the finalizer
    /// thread (<see cref="TaskScheduler.UnobservedTaskException"/>) when OBS is not running;
    /// skipping the connect attempt when the port is closed avoids that crash entirely.
    /// </summary>
    private async Task<bool> IsObsReachableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

            await client.ConnectAsync(_ip, _port, timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OBS is not reachable at {Url}: {ex.Message}");
            return false;
        }
    }

    public async Task ConnectAndWaitAsync(CancellationToken cancellationToken = default)
    {
        if (_obs.IsConnected)
            return;

        if (!await IsObsReachableAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"OBS is not reachable at {Url}.");

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _obs.Connected += OnConnected;
        _obs.Disconnected += OnDisconnected;

        _obs.ConnectAsync(Url, _password);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);

        await using (linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token)))
        {
            await tcs.Task.ConfigureAwait(false);
        }

        return;

        void OnConnected(object? _, EventArgs __)
        {
            Unsubscribe();
            tcs.TrySetResult();
        }

        void OnDisconnected(object? _, ObsDisconnectionInfo info)
        {
            Unsubscribe();
            tcs.TrySetException(new InvalidOperationException(info.DisconnectReason ?? "OBS disconnected."));
        }

        void Unsubscribe()
        {
            _obs.Connected -= OnConnected;
            _obs.Disconnected -= OnDisconnected;
        }
    }

    public void Disconnect()
    {
        if (_obs.IsConnected)
            _obs.Disconnect();
    }

    private async Task<bool> CheckConnection()
    {
        if (_obs.IsConnected)
            return true;

        try
        {
            await ConnectAndWaitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to OBS: {ex.Message}");
            return false;
        }

        return _obs.IsConnected;
    }

    public async Task ToggleVirtualCamera()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.ToggleVirtualCam(), "toggling virtual camera");
    }

    public async Task ToggleRecording()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.ToggleRecord(), "toggling recording");
    }

    public async Task StartRecording()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StartRecord(), "starting recording");
    }

    public async Task StopRecording()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StopRecord(), "stopping recording");
    }

    public async Task PauseRecording()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.ToggleRecordPause(), "pausing or resuming recording");
    }

    public async Task ToggleReplayBuffer()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.ToggleReplayBuffer(), "toggling the replay buffer");
    }

    public async Task StartReplayBuffer()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StartReplayBuffer(), "starting replay buffer");
    }

    public async Task StopReplayBuffer()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StopReplayBuffer(), "stopping replay buffer");
    }

    public async Task SaveReplayBuffer()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.SaveReplayBuffer(), "saving replay buffer");
    }

    public async Task SetScene(string sceneName)
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.SetCurrentProgramScene(sceneName), $"setting scene '{sceneName}'");
    }

    public async Task<List<SceneBasicInfo>> GetScenes()
    {
        if (!await CheckConnection().ConfigureAwait(false))
            return [];

        try
        {
            return _obs.GetSceneList().Scenes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting OBS scenes: {ex.Message}");
            return [];
        }
    }

    public async Task ToggleStream()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.ToggleStream(), "toggling the stream");
    }

    public async Task StartStream()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StartStream(), "starting the stream");
    }

    public async Task StopStream()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StopStream(), "stopping the stream");
    }

    public async Task ToggleStudioMode()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.SetStudioModeEnabled(!_obs.GetStudioModeEnabled()), "toggling studio mode");
    }

    public async Task SetPreviewScene(string sceneName)
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.SetCurrentPreviewScene(sceneName), $"setting preview scene '{sceneName}'");
    }

    public async Task TriggerTransition()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.TriggerStudioModeTransition(), "triggering the studio mode transition");
    }

    public async Task SetInputMuted(string inputName, bool muted)
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.SetInputMute(inputName, muted),
                $"{(muted ? "muting" : "unmuting")} input '{inputName}'");
    }

    public async Task ToggleInputMute(string inputName)
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.ToggleInputMute(inputName), $"toggling mute of input '{inputName}'");
    }

    public async Task<List<string>> GetInputNames()
    {
        if (!await CheckConnection().ConfigureAwait(false))
            return [];

        try
        {
            return _obs.GetInputList().Select(input => input.InputName).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting OBS inputs: {ex.Message}");
            return [];
        }
    }

    public async Task SetSourceVisible(string sourceName, string sceneName, bool visible)
    {
        if (!await CheckConnection().ConfigureAwait(false))
            return;

        Guarded(() =>
        {
            string scene = ResolveScene(sceneName);
            _obs.SetSceneItemEnabled(scene, _obs.GetSceneItemId(scene, sourceName, 0), visible);
        }, $"{(visible ? "showing" : "hiding")} source '{sourceName}'");
    }

    public async Task ToggleSource(string sourceName, string sceneName)
    {
        if (!await CheckConnection().ConfigureAwait(false))
            return;

        Guarded(() =>
        {
            string scene = ResolveScene(sceneName);
            int itemId = _obs.GetSceneItemId(scene, sourceName, 0);
            _obs.SetSceneItemEnabled(scene, itemId, !_obs.GetSceneItemEnabled(scene, itemId));
        }, $"toggling source '{sourceName}'");
    }

    /// <summary>
    /// Resolves the scene a source command works on. The host can only fill a single
    /// parameter from a menu selection, so the scene stays optional: unless the user pins
    /// a scene name in the command's settings, the command follows the program scene.
    /// </summary>
    private string ResolveScene(string sceneName) =>
        string.IsNullOrWhiteSpace(sceneName) || sceneName == CurrentScenePlaceholder
            ? _obs.GetCurrentProgramScene()
            : sceneName;

    public async Task<List<string>> GetSourceNames(string sceneName)
    {
        if (!await CheckConnection().ConfigureAwait(false))
            return [];

        try
        {
            return _obs.GetSceneItemList(sceneName).Select(item => item.SourceName).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting sources of OBS scene '{sceneName}': {ex.Message}");
            return [];
        }
    }

    private static void Guarded(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error {what}: {ex.Message}");
        }
    }
}
