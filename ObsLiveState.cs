using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// What OBS currently shows, for the per-button states of the scene, source, filter and audio
/// commands: the program and preview scene, which scene items are shown, which filters are
/// enabled and which inputs are muted.
/// </summary>
/// <remarks>
/// Nothing is polled. The host asks <see cref="IButtonStateCommand.GetButtonState"/> per button,
/// which answers from this cache. A scene, source or input nobody asked about yet is fetched once,
/// in the background, and the buttons of that command are asked again when it arrives. OBS events
/// keep everything fetched up to date and ask the host to refresh the affected commands.
/// </remarks>
internal sealed class ObsLiveState
{
    public const string SetSceneCommand = "System.ObsSetScene";
    public const string SetPreviewSceneCommand = "System.ObsSetPreviewScene";
    public const string ToggleSourceCommand = "System.ObsToggleSource";
    public const string ToggleFilterCommand = "System.ObsToggleFilter";
    public const string ToggleInputMuteCommand = "System.ObsToggleInputMute";

    private static readonly string[] AllCommands =
    [
        SetSceneCommand, SetPreviewSceneCommand, ToggleSourceCommand, ToggleFilterCommand, ToggleInputMuteCommand
    ];

    private readonly IObsController _obs;
    private readonly object _gate = new();

    // All guarded by _gate.
    private bool _scenesKnown;
    private string? _programScene;
    private string? _previewScene;
    private readonly Dictionary<string, Dictionary<string, (int Id, bool Enabled)>> _sceneItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, bool>> _filters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _muted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
    private int _generation;

    private IPluginHost? _host;

    public ObsLiveState(IObsController obs) => _obs = obs;

    public void Attach(IPluginHost host)
    {
        Detach();
        _host = host;

        _obs.ConnectionChanged += OnConnectionChanged;
        _obs.StudioModeChanged += OnStudioModeChanged;
        _obs.ProgramSceneChanged += OnProgramSceneChanged;
        _obs.PreviewSceneChanged += OnPreviewSceneChanged;
        _obs.SceneItemEnabledChanged += OnSceneItemEnabledChanged;
        _obs.SceneItemsChanged += OnSceneItemsChanged;
        _obs.FilterEnabledChanged += OnFilterEnabledChanged;
        _obs.FiltersChanged += OnFiltersChanged;
        _obs.InputMuteChanged += OnInputMuteChanged;
        _obs.NamesChanged += OnNamesChanged;
    }

    public void Detach()
    {
        _host = null;
        _obs.ConnectionChanged -= OnConnectionChanged;
        _obs.StudioModeChanged -= OnStudioModeChanged;
        _obs.ProgramSceneChanged -= OnProgramSceneChanged;
        _obs.PreviewSceneChanged -= OnPreviewSceneChanged;
        _obs.SceneItemEnabledChanged -= OnSceneItemEnabledChanged;
        _obs.SceneItemsChanged -= OnSceneItemsChanged;
        _obs.FilterEnabledChanged -= OnFilterEnabledChanged;
        _obs.FiltersChanged -= OnFiltersChanged;
        _obs.InputMuteChanged -= OnInputMuteChanged;
        _obs.NamesChanged -= OnNamesChanged;
        Clear();
    }

    // ───────── Answers for the commands ─────────

    /// <summary><see cref="ObsStates.Active"/> when <paramref name="sceneName"/> is on program.</summary>
    public string? GetSceneState(string sceneName)
    {
        lock (_gate)
        {
            if (!_scenesKnown)
                return null;

            return string.Equals(sceneName, _programScene, StringComparison.Ordinal)
                ? ObsStates.Active
                : ObsStates.Inactive;
        }
    }

    /// <summary><see cref="ObsStates.Preview"/> when <paramref name="sceneName"/> is the studio mode preview.</summary>
    public string? GetPreviewSceneState(string sceneName)
    {
        lock (_gate)
        {
            if (!_scenesKnown)
                return null;

            return string.Equals(sceneName, _previewScene, StringComparison.Ordinal)
                ? ObsStates.Preview
                : ObsStates.Inactive;
        }
    }

    /// <summary>
    /// <see cref="ObsStates.Shown"/> or <see cref="ObsStates.Hidden"/> for a source in a scene; an
    /// empty or placeholder scene follows the program scene, like the command does.
    /// </summary>
    public string? GetSourceState(string sourceName, string? sceneName)
    {
        string? scene;
        lock (_gate)
        {
            scene = string.IsNullOrWhiteSpace(sceneName) || sceneName == ObsController.CurrentScenePlaceholder
                ? _programScene
                : sceneName;
            if (scene == null)
                return null;

            if (_sceneItems.TryGetValue(scene, out Dictionary<string, (int Id, bool Enabled)>? items))
            {
                return items.TryGetValue(sourceName, out (int Id, bool Enabled) item)
                    ? (item.Enabled ? ObsStates.Shown : ObsStates.Hidden)
                    : null;
            }
        }

        Fetch("scene:" + scene, ToggleSourceCommand, async () =>
        {
            Dictionary<string, (int Id, bool Enabled)>? items = await _obs.GetSceneItemStates(scene);
            if (items == null)
                return false;

            lock (_gate)
                _sceneItems[scene] = items;
            return true;
        });
        return null;
    }

    /// <summary><see cref="ObsStates.Shown"/> or <see cref="ObsStates.Hidden"/> for a filter on a source or scene.</summary>
    public string? GetFilterState(string sourceName, string filterName)
    {
        lock (_gate)
        {
            if (_filters.TryGetValue(sourceName, out Dictionary<string, bool>? filters))
            {
                return filters.TryGetValue(filterName, out bool enabled)
                    ? (enabled ? ObsStates.Shown : ObsStates.Hidden)
                    : null;
            }
        }

        Fetch("filters:" + sourceName, ToggleFilterCommand, async () =>
        {
            Dictionary<string, bool>? filters = await _obs.GetFilterStates(sourceName);
            if (filters == null)
                return false;

            lock (_gate)
                _filters[sourceName] = filters;
            return true;
        });
        return null;
    }

    /// <summary><see cref="ObsStates.Muted"/> or <see cref="ObsStates.Unmuted"/> for an input.</summary>
    public string? GetMuteState(string inputName)
    {
        lock (_gate)
        {
            if (_muted.TryGetValue(inputName, out bool muted))
                return muted ? ObsStates.Muted : ObsStates.Unmuted;
        }

        Fetch("mute:" + inputName, ToggleInputMuteCommand, async () =>
        {
            bool? muted = await _obs.GetInputMuted(inputName);
            if (muted == null)
                return false;

            lock (_gate)
                _muted[inputName] = muted.Value;
            return true;
        });
        return null;
    }

    /// <summary>
    /// Loads something not cached yet, once at a time per key, and asks the host to refresh
    /// <paramref name="commandName"/> when it arrived. Nothing arrives while OBS cannot be asked,
    /// so a failed load does not loop: the buttons are only asked again after a successful one.
    /// </summary>
    private void Fetch(string key, string commandName, Func<Task<bool>> load)
    {
        int generation;
        lock (_gate)
        {
            if (!_pending.Add(key))
                return;
            generation = _generation;
        }

        _ = Task.Run(async () =>
        {
            bool loaded = false;
            try
            {
                loaded = await load();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading OBS state for {key}: {ex.Message}");
            }
            finally
            {
                lock (_gate)
                {
                    _pending.Remove(key);
                    // Cleared meanwhile (disconnect, rename): the answer may already be stale.
                    loaded &= generation == _generation;
                }
            }

            if (loaded)
                Refresh(commandName);
        });
    }

    // ───────── OBS events ─────────

    private void OnConnectionChanged(bool connected)
    {
        Clear();
        if (connected)
            // Scene items, filters and mutes load on demand once the buttons ask.
            LoadCurrentScenes(AllCommands);
    }

    private void OnStudioModeChanged(bool enabled)
    {
        if (enabled)
        {
            // Turning studio mode on does not reliably announce the preview scene.
            LoadCurrentScenes(SetPreviewSceneCommand);
            return;
        }

        lock (_gate)
            _previewScene = null;
        Refresh(SetPreviewSceneCommand);
    }

    /// <summary>Reads the program and preview scene, then refreshes <paramref name="commandNames"/>.</summary>
    private void LoadCurrentScenes(params string[] commandNames) => _ = Task.Run(async () =>
    {
        int generation;
        lock (_gate)
            generation = _generation;

        (string Program, string? Preview)? scenes = await _obs.GetCurrentScenes();
        if (scenes == null)
            return;

        lock (_gate)
        {
            if (generation != _generation)
                return; // Disconnected meanwhile.

            _programScene = scenes.Value.Program;
            _previewScene = scenes.Value.Preview;
            _scenesKnown = true;
        }

        Refresh(commandNames);
    });

    private void OnProgramSceneChanged(string sceneName)
    {
        lock (_gate)
        {
            _programScene = sceneName;
            _scenesKnown = true;
        }

        // A source command without a pinned scene follows the program scene.
        Refresh(SetSceneCommand, ToggleSourceCommand);
    }

    private void OnPreviewSceneChanged(string sceneName)
    {
        lock (_gate)
            _previewScene = sceneName;
        Refresh(SetPreviewSceneCommand);
    }

    private void OnSceneItemEnabledChanged(string sceneName, int itemId, bool enabled)
    {
        lock (_gate)
        {
            if (!_sceneItems.TryGetValue(sceneName, out Dictionary<string, (int Id, bool Enabled)>? items))
                return; // No button asked about this scene yet.

            foreach ((string source, (int Id, bool Enabled) item) in items.ToList())
            {
                if (item.Id == itemId)
                    items[source] = (item.Id, enabled);
            }
        }

        Refresh(ToggleSourceCommand);
    }

    private void OnSceneItemsChanged(string sceneName)
    {
        lock (_gate)
        {
            if (!_sceneItems.Remove(sceneName))
                return;
        }

        Refresh(ToggleSourceCommand);
    }

    private void OnFilterEnabledChanged(string sourceName, string filterName, bool enabled)
    {
        lock (_gate)
        {
            if (!_filters.TryGetValue(sourceName, out Dictionary<string, bool>? filters))
                return;
            filters[filterName] = enabled;
        }

        Refresh(ToggleFilterCommand);
    }

    private void OnFiltersChanged(string sourceName)
    {
        lock (_gate)
        {
            if (!_filters.Remove(sourceName))
                return;
        }

        Refresh(ToggleFilterCommand);
    }

    private void OnInputMuteChanged(string inputName, bool muted)
    {
        lock (_gate)
            _muted[inputName] = muted;
        Refresh(ToggleInputMuteCommand);
    }

    /// <summary>A rename invalidates everything keyed by name; it is fetched again on demand.</summary>
    private void OnNamesChanged()
    {
        lock (_gate)
        {
            _generation++;
            _sceneItems.Clear();
            _filters.Clear();
            _muted.Clear();
        }

        Refresh(ToggleSourceCommand, ToggleFilterCommand, ToggleInputMuteCommand);
    }

    private void Clear()
    {
        lock (_gate)
        {
            _generation++;
            _scenesKnown = false;
            _programScene = null;
            _previewScene = null;
            _sceneItems.Clear();
            _filters.Clear();
            _muted.Clear();
        }
    }

    private void Refresh(params string[] commandNames)
    {
        if (_host is not { } host)
            return;

        foreach (string commandName in commandNames)
        {
            try
            {
                host.RefreshButtonStates(commandName);
            }
            catch (Exception ex)
            {
                host.Logger.Warn($"Could not refresh button states for {commandName}: {ex.Message}");
            }
        }
    }
}
