using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// Pushes the live OBS state onto the host's stateful buttons, so a button bound to an
/// OBS command shows what OBS is actually doing — including changes made in OBS itself.
/// </summary>
/// <remarks>
/// The user creates and names the button states, so a state can never be addressed by a
/// fixed name alone. Each state is resolved by name first (a small list of common English
/// and German synonyms) and by position otherwise, with the index clamped to what the
/// button actually offers.
/// </remarks>
internal sealed class ObsStateReporter : IDisposable
{
    /// <summary>Commands whose button reflects the recording state.</summary>
    private static readonly string[] RecordCommands =
    [
        "System.ObsStartRecord",
        "System.ObsStopRecord",
        "System.ObsPauseRecord"
    ];

    /// <summary>Commands whose button reflects the replay buffer state.</summary>
    private static readonly string[] ReplayCommands =
    [
        "System.ObsStartReplay",
        "System.ObsStopReplay",
        "System.ObsSaveReplay"
    ];

    private static readonly string[] VirtualCamCommands = ["System.ObsVirtualCam"];

    /// <summary>Commands whose button reflects the streaming state.</summary>
    private static readonly string[] StreamCommands =
    [
        "System.ObsStartStream",
        "System.ObsStopStream"
    ];

    private static readonly string[] StudioModeCommands = ["System.ObsToggleStudioMode"];

    private static readonly string[] InactiveNames =
        ["Idle", "Stopped", "Off", "Inactive", "Aus", "Gestoppt", "Inaktiv"];

    private static readonly string[] ActiveNames =
        ["Recording", "Active", "On", "Running", "Rec", "An", "Ein", "Aktiv", "Aufnahme"];

    private static readonly string[] PausedNames = ["Paused", "Pause", "Pausiert"];

    private readonly IPluginHost _host;
    private readonly IObsController _obs;

    public ObsStateReporter(IPluginHost host, IObsController obs)
    {
        _host = host;
        _obs = obs;

        _obs.RecordStateChanged += OnRecordStateChanged;
        _obs.ReplayBufferActiveChanged += OnReplayBufferActiveChanged;
        _obs.VirtualCamActiveChanged += OnVirtualCamActiveChanged;
        _obs.StreamActiveChanged += OnStreamActiveChanged;
        _obs.StudioModeChanged += OnStudioModeChanged;
    }

    public void Dispose()
    {
        _obs.RecordStateChanged -= OnRecordStateChanged;
        _obs.ReplayBufferActiveChanged -= OnReplayBufferActiveChanged;
        _obs.VirtualCamActiveChanged -= OnVirtualCamActiveChanged;
        _obs.StreamActiveChanged -= OnStreamActiveChanged;
        _obs.StudioModeChanged -= OnStudioModeChanged;
    }

    private void OnRecordStateChanged(ObsRecordState state)
    {
        (int index, string[] names) = state switch
        {
            ObsRecordState.Recording => (1, ActiveNames),
            ObsRecordState.Paused => (2, PausedNames),
            _ => (0, InactiveNames)
        };

        Apply(RecordCommands, index, names);
    }

    private void OnReplayBufferActiveChanged(bool active) => ApplyToggle(ReplayCommands, active);

    private void OnVirtualCamActiveChanged(bool active) => ApplyToggle(VirtualCamCommands, active);

    private void OnStreamActiveChanged(bool active) => ApplyToggle(StreamCommands, active);

    private void OnStudioModeChanged(bool enabled) => ApplyToggle(StudioModeCommands, enabled);

    private void ApplyToggle(string[] commandNames, bool active) =>
        Apply(commandNames, active ? 1 : 0, active ? ActiveNames : InactiveNames);

    /// <summary>
    /// Selects the state to show on every given command's button: by one of
    /// <paramref name="preferredNames"/> when the user named a state that way, by
    /// <paramref name="index"/> otherwise.
    /// </summary>
    private void Apply(string[] commandNames, int index, string[] preferredNames)
    {
        foreach (string commandName in commandNames)
        {
            try
            {
                IReadOnlyList<string> states = _host.GetButtonStates(commandName);
                if (states.Count == 0)
                    continue; // No stateful button bound to this command.

                string? target = states.FirstOrDefault(
                    state => preferredNames.Contains(state, StringComparer.OrdinalIgnoreCase));

                // A three-state signal (paused) on a two-state button falls back to "active".
                target ??= states[Math.Clamp(index, 0, states.Count - 1)];

                _host.SetActiveButtonState(commandName, target);
            }
            catch (Exception ex)
            {
                // The host may not be ready yet, or the button was unbound meanwhile.
                _host.Logger.Warn($"Could not update button state for {commandName}: {ex.Message}");
            }
        }
    }
}
