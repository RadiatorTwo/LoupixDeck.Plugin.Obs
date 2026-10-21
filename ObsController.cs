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

    /// <summary>Raised with the new program scene's name.</summary>
    event Action<string> ProgramSceneChanged;

    /// <summary>Raised with the new studio mode preview scene's name.</summary>
    event Action<string> PreviewSceneChanged;

    /// <summary>Raised when a scene item is shown or hidden: scene name, scene item id, enabled.</summary>
    event Action<string, int, bool> SceneItemEnabledChanged;

    /// <summary>Raised when an item was added to or removed from the named scene.</summary>
    event Action<string> SceneItemsChanged;

    /// <summary>Raised when a filter is enabled or disabled: source name, filter name, enabled.</summary>
    event Action<string, string, bool> FilterEnabledChanged;

    /// <summary>Raised when a filter was added to, removed from or renamed on the named source.</summary>
    event Action<string> FiltersChanged;

    /// <summary>Raised when an input is muted or unmuted: input name, muted.</summary>
    event Action<string, bool> InputMuteChanged;

    /// <summary>Raised when a scene or an input was renamed, or the scene list changed.</summary>
    event Action NamesChanged;

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

    /// <summary>Enables or disables the filter <paramref name="filterName"/> on a source or scene.</summary>
    Task SetFilterEnabled(string sourceName, string filterName, bool enabled);

    /// <inheritdoc cref="SetFilterEnabled"/>
    Task ToggleFilter(string sourceName, string filterName);

    /// <summary>Names of the filters on a source or scene, for the dynamic "Filters" submenu.</summary>
    Task<List<string>> GetFilterNames(string sourceName);

    // The queries below feed the live button states. Each returns null when OBS cannot be asked
    // (not connected, or the request failed), so the caller keeps no wrong answer.

    /// <summary>The program scene and the studio mode preview scene (null outside studio mode).</summary>
    Task<(string Program, string? Preview)?> GetCurrentScenes();

    /// <summary>The items of a scene by source name: their scene item id and whether they are shown.</summary>
    Task<Dictionary<string, (int Id, bool Enabled)>?> GetSceneItemStates(string sceneName);

    /// <summary>The filters of a source or scene by name, and whether each is enabled.</summary>
    Task<Dictionary<string, bool>?> GetFilterStates(string sourceName);

    /// <summary>Whether an input is muted.</summary>
    Task<bool?> GetInputMuted(string inputName);
}

/// <inheritdoc cref="IObsController"/>
public sealed class ObsController : IObsController
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();

    // The connection currently in use, or null. A fresh OBSWebsocket is created for every
    // connect: the library keeps the password and the socket in shared fields, so reusing one
    // instance across overlapping connects sends the Identify of one socket over another
    // (with the password already cleared), which OBS answers with "Authentication failed"
    // or "already Identified".
    private volatile OBSWebsocket? _obs;
    private volatile bool _identified;
    private Task<bool>? _connectTask;
    private int _generation;
    private string _lastError = "OBS is not connected.";

    private string _ip = "127.0.0.1";
    private int _port = 4455;
    private string _password = string.Empty;
    private bool _settingsChanged;

    private string Url => $"ws://{_ip}:{_port}";

    /// <summary>Scene-parameter value meaning "whatever is on program right now".</summary>
    public const string CurrentScenePlaceholder = "<current>";

    public event Action<bool>? ConnectionChanged;
    public event Action<ObsRecordState>? RecordStateChanged;
    public event Action<bool>? ReplayBufferActiveChanged;
    public event Action<bool>? VirtualCamActiveChanged;
    public event Action<bool>? StreamActiveChanged;
    public event Action<bool>? StudioModeChanged;
    public event Action<string>? ProgramSceneChanged;
    public event Action<string>? PreviewSceneChanged;
    public event Action<string, int, bool>? SceneItemEnabledChanged;
    public event Action<string>? SceneItemsChanged;
    public event Action<string, string, bool>? FilterEnabledChanged;
    public event Action<string>? FiltersChanged;
    public event Action<string, bool>? InputMuteChanged;
    public event Action? NamesChanged;

    /// <summary>
    /// Subscribes to the events of one connection. Every handler ignores a connection that has
    /// since been replaced, so a late event from a closed socket cannot touch the current state.
    /// </summary>
    private void Attach(OBSWebsocket obs)
    {
        obs.Connected += (_, _) =>
        {
            if (!ReferenceEquals(obs, _obs))
                return;

            _identified = true;
            Raise(ConnectionChanged, true);
            // Off the websocket callback thread: the sync calls back into OBS.
            _ = Task.Run(() => SyncState(obs));
        };

        obs.Disconnected += (_, _) =>
        {
            if (!ReferenceEquals(obs, _obs))
                return;

            _identified = false;
            ReportDisconnected();
        };

        obs.RecordStateChanged += (_, e) =>
        {
            if (!ReferenceEquals(obs, _obs))
                return;

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

        obs.ReplayBufferStateChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                RaiseOnFinalState(ReplayBufferActiveChanged, e.OutputState);
        };
        obs.VirtualcamStateChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                RaiseOnFinalState(VirtualCamActiveChanged, e.OutputState);
        };
        obs.StreamStateChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                RaiseOnFinalState(StreamActiveChanged, e.OutputState);
        };
        obs.StudioModeStateChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(StudioModeChanged, e.StudioModeEnabled);
        };

        obs.CurrentProgramSceneChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(ProgramSceneChanged, e.SceneName);
        };
        obs.CurrentPreviewSceneChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(PreviewSceneChanged, e.SceneName);
        };
        obs.SceneItemEnableStateChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(SceneItemEnabledChanged, e.SceneName, e.SceneItemId, e.SceneItemEnabled);
        };
        obs.SceneItemCreated += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(SceneItemsChanged, e.SceneName);
        };
        obs.SceneItemRemoved += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(SceneItemsChanged, e.SceneName);
        };
        obs.SourceFilterEnableStateChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(FilterEnabledChanged, e.SourceName, e.FilterName, e.FilterEnabled);
        };
        obs.SourceFilterCreated += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(FiltersChanged, e.SourceName);
        };
        obs.SourceFilterRemoved += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(FiltersChanged, e.SourceName);
        };
        obs.SourceFilterNameChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(FiltersChanged, e.SourceName);
        };
        obs.InputMuteStateChanged += (_, e) =>
        {
            if (ReferenceEquals(obs, _obs))
                Raise(InputMuteChanged, e.InputName, e.InputMuted);
        };
        obs.SceneNameChanged += (_, _) => RaiseNamesChanged(obs);
        obs.InputNameChanged += (_, _) => RaiseNamesChanged(obs);
        obs.SceneListChanged += (_, _) => RaiseNamesChanged(obs);
    }

    private void RaiseNamesChanged(OBSWebsocket obs)
    {
        if (ReferenceEquals(obs, _obs))
            Raise(() => NamesChanged?.Invoke());
    }

    /// <summary>
    /// Reads the current state of every tracked output and republishes it. Called after a
    /// successful connect so the deck shows the truth even when OBS was already recording
    /// (or LoupixDeck was restarted mid-session).
    /// </summary>
    private void SyncState(OBSWebsocket obs)
    {
        try
        {
            RecordingStatus record = obs.GetRecordStatus();
            Raise(RecordStateChanged, record.IsRecording
                ? (record.IsRecordingPaused ? ObsRecordState.Paused : ObsRecordState.Recording)
                : ObsRecordState.Stopped);

            Raise(ReplayBufferActiveChanged, obs.GetReplayBufferStatus());
            Raise(VirtualCamActiveChanged, obs.GetVirtualCamStatus().IsActive);
            Raise(StreamActiveChanged, obs.GetStreamStatus().IsActive);
            Raise(StudioModeChanged, obs.GetStudioModeEnabled());
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
    private static void Raise<T>(Action<T>? handler, T value) => Raise(() => handler?.Invoke(value));

    private static void Raise<T1, T2>(Action<T1, T2>? handler, T1 first, T2 second) =>
        Raise(() => handler?.Invoke(first, second));

    private static void Raise<T1, T2, T3>(Action<T1, T2, T3>? handler, T1 first, T2 second, T3 third) =>
        Raise(() => handler?.Invoke(first, second, third));

    private static void Raise(Action dispatch)
    {
        try
        {
            dispatch();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error dispatching OBS state change: {ex.Message}");
        }
    }

    public void Configure(string ip, int port, string password)
    {
        string newIp = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
        int newPort = port > 0 ? port : 4455;
        string newPassword = password ?? string.Empty;

        lock (_gate)
        {
            if (newIp == _ip && newPort == _port && newPassword == _password)
                return;

            _ip = newIp;
            _port = newPort;
            _password = newPassword;
            _settingsChanged = true;
        }
    }

    /// <summary>
    /// Connects in the background. An established connection is only replaced when the
    /// connection settings changed, so saving or testing unchanged settings keeps the session.
    /// </summary>
    public void Connect()
    {
        _ = EnsureConnectedAsync(ConsumeSettingsChanged());
    }

    private bool ConsumeSettingsChanged()
    {
        lock (_gate)
        {
            bool changed = _settingsChanged;
            _settingsChanged = false;
            return changed;
        }
    }

    /// <summary>
    /// Returns the connect attempt every caller shares. While one is running, callers join it
    /// instead of starting another; <paramref name="reconnect"/> queues a new attempt after it.
    /// The returned task never faults — false means not connected, see <see cref="_lastError"/>.
    /// </summary>
    private Task<bool> EnsureConnectedAsync(bool reconnect)
    {
        lock (_gate)
        {
            Task<bool>? pending = _connectTask is { IsCompleted: false } ? _connectTask : null;

            if (!reconnect)
            {
                if (pending != null)
                    return pending;

                if (_identified)
                    return Task.FromResult(true);
            }

            return _connectTask = ConnectAfterAsync(pending, _generation);
        }
    }

    private async Task<bool> ConnectAfterAsync(Task<bool>? previous, int generation)
    {
        if (previous != null)
            await previous.ConfigureAwait(false);

        return await ConnectCoreAsync(generation).ConfigureAwait(false);
    }

    private async Task<bool> ConnectCoreAsync(int generation)
    {
        OBSWebsocket? obs = null;

        try
        {
            string url;
            string password;
            lock (_gate)
            {
                url = Url;
                password = _password;
            }

            if (!await IsObsReachableAsync(CancellationToken.None).ConfigureAwait(false))
            {
                _lastError = $"OBS is not reachable at {url}.";
                return false;
            }

            obs = new OBSWebsocket();
            OBSWebsocket? previous;

            lock (_gate)
            {
                // Disconnect() was called while probing: the plugin is shutting down.
                if (generation != _generation)
                    return false;

                previous = _obs;
                _obs = obs;
                _identified = false;
            }

            previous?.Disconnect();

            // Attach first so the state handlers have marked the connection identified
            // before the waiter below resumes.
            Attach(obs);

            var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            obs.Connected += (_, _) => result.TrySetResult(null);
            obs.Disconnected += (_, info) => result.TrySetResult(info.DisconnectReason ?? "OBS disconnected.");

            obs.ConnectAsync(url, password);

            string? error;
            try
            {
                error = await result.Task.WaitAsync(ConnectTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                error = $"OBS at {url} did not answer.";
            }

            if (error == null && ReferenceEquals(obs, _obs))
                return true;

            _lastError = error ?? "OBS was disconnected.";
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
        }

        if (obs != null)
        {
            lock (_gate)
            {
                if (ReferenceEquals(obs, _obs))
                {
                    _obs = null;
                    _identified = false;
                }
            }

            obs.Disconnect();
        }

        Console.WriteLine($"Error connecting to OBS: {_lastError}");
        return false;
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
        bool connected = await EnsureConnectedAsync(ConsumeSettingsChanged())
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        if (!connected)
            throw new InvalidOperationException(_lastError);
    }

    public void Disconnect()
    {
        OBSWebsocket? obs;
        bool wasIdentified;

        lock (_gate)
        {
            _generation++;
            obs = _obs;
            wasIdentified = _identified;
            _obs = null;
            _identified = false;
        }

        if (obs == null)
            return;

        obs.Disconnect();

        // The handlers ignore the replaced connection, so report the loss here.
        if (wasIdentified)
            ReportDisconnected();
    }

    /// <summary>The identified connection, connecting first when needed; null when OBS cannot be reached.</summary>
    private async Task<OBSWebsocket?> GetConnectionAsync()
    {
        if (!await EnsureConnectedAsync(reconnect: false).ConfigureAwait(false))
            return null;

        return _identified ? _obs : null;
    }

    public async Task ToggleVirtualCamera()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.ToggleVirtualCam(), "toggling virtual camera");
    }

    public async Task ToggleRecording()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.ToggleRecord(), "toggling recording");
    }

    public async Task StartRecording()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.StartRecord(), "starting recording");
    }

    public async Task StopRecording()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.StopRecord(), "stopping recording");
    }

    public async Task PauseRecording()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.ToggleRecordPause(), "pausing or resuming recording");
    }

    public async Task ToggleReplayBuffer()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.ToggleReplayBuffer(), "toggling the replay buffer");
    }

    public async Task StartReplayBuffer()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.StartReplayBuffer(), "starting replay buffer");
    }

    public async Task StopReplayBuffer()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.StopReplayBuffer(), "stopping replay buffer");
    }

    public async Task SaveReplayBuffer()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.SaveReplayBuffer(), "saving replay buffer");
    }

    public async Task SetScene(string sceneName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.SetCurrentProgramScene(sceneName), $"setting scene '{sceneName}'");
    }

    public async Task<List<SceneBasicInfo>> GetScenes()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return [];

        try
        {
            return obs.GetSceneList().Scenes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting OBS scenes: {ex.Message}");
            return [];
        }
    }

    public async Task ToggleStream()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.ToggleStream(), "toggling the stream");
    }

    public async Task StartStream()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.StartStream(), "starting the stream");
    }

    public async Task StopStream()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.StopStream(), "stopping the stream");
    }

    public async Task ToggleStudioMode()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.SetStudioModeEnabled(!obs.GetStudioModeEnabled()), "toggling studio mode");
    }

    public async Task SetPreviewScene(string sceneName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.SetCurrentPreviewScene(sceneName), $"setting preview scene '{sceneName}'");
    }

    public async Task TriggerTransition()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.TriggerStudioModeTransition(), "triggering the studio mode transition");
    }

    public async Task SetInputMuted(string inputName, bool muted)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.SetInputMute(inputName, muted),
                $"{(muted ? "muting" : "unmuting")} input '{inputName}'");
    }

    public async Task ToggleInputMute(string inputName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.ToggleInputMute(inputName), $"toggling mute of input '{inputName}'");
    }

    public async Task<List<string>> GetInputNames()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return [];

        try
        {
            return obs.GetInputList().Select(input => input.InputName).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting OBS inputs: {ex.Message}");
            return [];
        }
    }

    public async Task SetSourceVisible(string sourceName, string sceneName, bool visible)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return;

        Guarded(() =>
        {
            string scene = ResolveScene(obs, sceneName);
            obs.SetSceneItemEnabled(scene, obs.GetSceneItemId(scene, sourceName, 0), visible);
        }, $"{(visible ? "showing" : "hiding")} source '{sourceName}'");
    }

    public async Task ToggleSource(string sourceName, string sceneName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return;

        Guarded(() =>
        {
            string scene = ResolveScene(obs, sceneName);
            int itemId = obs.GetSceneItemId(scene, sourceName, 0);
            obs.SetSceneItemEnabled(scene, itemId, !obs.GetSceneItemEnabled(scene, itemId));
        }, $"toggling source '{sourceName}'");
    }

    /// <summary>
    /// Resolves the scene a source command works on. The host can only fill a single
    /// parameter from a menu selection, so the scene stays optional: unless the user pins
    /// a scene name in the command's settings, the command follows the program scene.
    /// </summary>
    private static string ResolveScene(OBSWebsocket obs, string sceneName) =>
        string.IsNullOrWhiteSpace(sceneName) || sceneName == CurrentScenePlaceholder
            ? obs.GetCurrentProgramScene()
            : sceneName;

    public async Task<List<string>> GetSourceNames(string sceneName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return [];

        try
        {
            return obs.GetSceneItemList(sceneName).Select(item => item.SourceName).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting sources of OBS scene '{sceneName}': {ex.Message}");
            return [];
        }
    }

    public async Task SetFilterEnabled(string sourceName, string filterName, bool enabled)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.SetSourceFilterEnabled(sourceName, filterName, enabled),
                $"{(enabled ? "enabling" : "disabling")} filter '{filterName}' on '{sourceName}'");
    }

    public async Task ToggleFilter(string sourceName, string filterName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is { } obs)
            Guarded(() => obs.SetSourceFilterEnabled(sourceName, filterName,
                !obs.GetSourceFilter(sourceName, filterName).IsEnabled),
                $"toggling filter '{filterName}' on '{sourceName}'");
    }

    public async Task<List<string>> GetFilterNames(string sourceName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return [];

        try
        {
            return obs.GetSourceFilterList(sourceName).Select(filter => filter.Name).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting filters of OBS source '{sourceName}': {ex.Message}");
            return [];
        }
    }

    public async Task<(string Program, string? Preview)?> GetCurrentScenes()
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return null;

        try
        {
            string program = obs.GetCurrentProgramScene();
            // Outside studio mode there is no preview scene, and OBS answers the request with an error.
            string? preview = obs.GetStudioModeEnabled() ? obs.GetCurrentPreviewScene() : null;
            return (program, preview);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting the current OBS scenes: {ex.Message}");
            return null;
        }
    }

    public async Task<Dictionary<string, (int Id, bool Enabled)>?> GetSceneItemStates(string sceneName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return null;

        try
        {
            Dictionary<string, (int Id, bool Enabled)> items = new(StringComparer.Ordinal);
            foreach (SceneItemDetails item in obs.GetSceneItemList(sceneName))
            {
                // A source placed twice in one scene: the commands act on the first one too.
                if (!items.ContainsKey(item.SourceName))
                    items[item.SourceName] = (item.ItemId, obs.GetSceneItemEnabled(sceneName, item.ItemId));
            }

            return items;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting the items of OBS scene '{sceneName}': {ex.Message}");
            return null;
        }
    }

    public async Task<Dictionary<string, bool>?> GetFilterStates(string sourceName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return null;

        try
        {
            Dictionary<string, bool> filters = new(StringComparer.Ordinal);
            foreach (FilterSettings filter in obs.GetSourceFilterList(sourceName))
                filters[filter.Name] = filter.IsEnabled;

            return filters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting the filters of OBS source '{sourceName}': {ex.Message}");
            return null;
        }
    }

    public async Task<bool?> GetInputMuted(string inputName)
    {
        if (await GetConnectionAsync().ConfigureAwait(false) is not { } obs)
            return null;

        try
        {
            return obs.GetInputMute(inputName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting the mute state of OBS input '{inputName}': {ex.Message}");
            return null;
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
