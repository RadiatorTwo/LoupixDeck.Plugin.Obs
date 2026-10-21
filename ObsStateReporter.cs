using System.Collections.Concurrent;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// Pushes the live OBS state onto the host's stateful buttons, so a button bound to an
/// OBS command shows what OBS is actually doing — including changes made in OBS itself.
/// </summary>
/// <remarks>
/// The commands declare their states (see <see cref="ObsStates"/>), so the host creates them
/// and a state can be addressed by its name directly. A button whose states were built by hand
/// before that (or edited afterwards) simply does not match and stays untouched.
/// The last reported state of every command is kept, so a button that missed the push can be
/// put right later (see <see cref="ObsStatefulCommand"/>).
/// </remarks>
internal sealed class ObsStateReporter
{
    /// <summary>Commands whose button reflects the recording state.</summary>
    private static readonly string[] RecordCommands =
    [
        "System.ObsToggleRecord",
        "System.ObsStartRecord",
        "System.ObsStopRecord",
        "System.ObsPauseRecord"
    ];

    /// <summary>Commands whose button reflects the replay buffer state.</summary>
    private static readonly string[] ReplayCommands =
    [
        "System.ObsToggleReplay",
        "System.ObsStartReplay",
        "System.ObsStopReplay",
        "System.ObsSaveReplay"
    ];

    private static readonly string[] VirtualCamCommands = ["System.ObsVirtualCam"];

    /// <summary>Commands whose button reflects the streaming state.</summary>
    private static readonly string[] StreamCommands =
    [
        "System.ObsToggleStream",
        "System.ObsStartStream",
        "System.ObsStopStream"
    ];

    private static readonly string[] StudioModeCommands = ["System.ObsToggleStudioMode"];

    private readonly IObsController _obs;
    private readonly ConcurrentDictionary<string, string> _states = new(StringComparer.Ordinal);
    private IPluginHost? _host;

    /// <remarks>
    /// Created before the plugin is initialized, because the commands hold it; it only listens
    /// to OBS between <see cref="Attach"/> and <see cref="Detach"/>.
    /// </remarks>
    public ObsStateReporter(IObsController obs) => _obs = obs;

    /// <summary>Starts pushing OBS state changes to the host's buttons.</summary>
    public void Attach(IPluginHost host)
    {
        Detach();
        _host = host;

        _obs.RecordStateChanged += OnRecordStateChanged;
        _obs.ReplayBufferActiveChanged += OnReplayBufferActiveChanged;
        _obs.VirtualCamActiveChanged += OnVirtualCamActiveChanged;
        _obs.StreamActiveChanged += OnStreamActiveChanged;
        _obs.StudioModeChanged += OnStudioModeChanged;
    }

    public void Detach()
    {
        _host = null;
        _states.Clear();
        _obs.RecordStateChanged -= OnRecordStateChanged;
        _obs.ReplayBufferActiveChanged -= OnReplayBufferActiveChanged;
        _obs.VirtualCamActiveChanged -= OnVirtualCamActiveChanged;
        _obs.StreamActiveChanged -= OnStreamActiveChanged;
        _obs.StudioModeChanged -= OnStudioModeChanged;
    }

    /// <summary>The state last reported for <paramref name="commandName"/>, or null before OBS reported one.</summary>
    public string? GetState(string commandName) => _states.GetValueOrDefault(commandName);

    private void OnRecordStateChanged(ObsRecordState state)
    {
        // A paused recording is still running, so it stays on "Recording" — pausing is its own
        // command, and the toggle button only says whether OBS is recording at all.
        string target = state switch
        {
            ObsRecordState.Recording or ObsRecordState.Paused => ObsStates.Recording,
            _ => ObsStates.Idle
        };

        Apply(RecordCommands, target);
    }

    private void OnReplayBufferActiveChanged(bool active) => ApplyToggle(ReplayCommands, active);

    private void OnVirtualCamActiveChanged(bool active) => ApplyToggle(VirtualCamCommands, active);

    private void OnStreamActiveChanged(bool active) =>
        Apply(StreamCommands, active ? ObsStates.Live : ObsStates.Offline);

    private void OnStudioModeChanged(bool enabled) => ApplyToggle(StudioModeCommands, enabled);

    private void ApplyToggle(string[] commandNames, bool active) =>
        Apply(commandNames, active ? ObsStates.On : ObsStates.Off);

    /// <summary>
    /// Shows <paramref name="stateName"/> on every button bound to one of the given commands.
    /// A button that does not offer that state (its states are the user's own) is left alone.
    /// </summary>
    private void Apply(string[] commandNames, string stateName)
    {
        foreach (string commandName in commandNames)
        {
            _states[commandName] = stateName;

            if (_host is not { } host)
                continue;

            try
            {
                if (!host.SetActiveButtonState(commandName, stateName))
                    continue; // No button bound to this command, or it already shows the state.
            }
            catch (Exception ex)
            {
                // The host may not be ready yet, or the button was unbound meanwhile.
                host.Logger.Warn($"Could not update button state for {commandName}: {ex.Message}");
            }
        }
    }
}
