using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// The OBS plugin commands. Command names are kept identical to the former
/// built-in commands so existing button assignments in <c>config.json</c>
/// keep working.
/// </summary>
internal sealed class ObsStartRecordCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStartRecord",
        DisplayName = "Start Recording",
        Group = "OBS",
        Icon = "\U000F044A", // mdi-record
        Description = "Start recording",
        States = ObsStates.Record
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.RecordVisuals;

    public override Task Execute(CommandContext ctx) => obs.StartRecording();
}

internal sealed class ObsStopRecordCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStopRecord",
        DisplayName = "Stop Recording",
        Group = "OBS",
        Icon = "\U000F04DB", // mdi-stop
        Description = "Stop recording",
        States = ObsStates.Record
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.RecordVisuals;

    public override Task Execute(CommandContext ctx) => obs.StopRecording();
}

internal sealed class ObsPauseRecordCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsPauseRecord",
        DisplayName = "Pause Recording",
        Group = "OBS",
        Icon = "\U000F03E4", // mdi-pause
        Description = "Pause or resume recording",
        States = ObsStates.Record
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.RecordVisuals;

    public override Task Execute(CommandContext ctx) => obs.PauseRecording();
}

internal sealed class ObsVirtualCamCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsVirtualCam",
        DisplayName = "Toggle Virtual Camera",
        Group = "OBS",
        Icon = "\U000F05A0", // mdi-webcam
        Description = "Toggle the virtual camera",
        States = ObsStates.Toggle
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.VirtualCamVisuals;

    public override Task Execute(CommandContext ctx) => obs.ToggleVirtualCamera();
}

internal sealed class ObsStartReplayCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStartReplay",
        DisplayName = "Start Replay",
        Group = "OBS",
        Icon = "\U000F040A", // mdi-play
        Description = "Start the replay buffer",
        States = ObsStates.Toggle
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.ReplayVisuals;

    public override Task Execute(CommandContext ctx) => obs.StartReplayBuffer();
}

internal sealed class ObsStopReplayCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStopReplay",
        DisplayName = "Stop Replay",
        Group = "OBS",
        Icon = "\U000F04DB", // mdi-stop
        Description = "Stop the replay buffer",
        States = ObsStates.Toggle
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.ReplayVisuals;

    public override Task Execute(CommandContext ctx) => obs.StopReplayBuffer();
}

internal sealed class ObsSaveReplayCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsSaveReplay",
        DisplayName = "Save Replay",
        Group = "OBS",
        Icon = "\U000F0193", // mdi-content-save
        Description = "Save the replay buffer",
        States = ObsStates.Toggle
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.ReplayVisuals;

    public override Task Execute(CommandContext ctx) => obs.SaveReplayBuffer();
}

internal sealed class ObsSetSceneCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsSetScene",
        DisplayName = "Set Scene",
        Group = "OBS",
        Icon = "\U000F0FCF", // mdi-movie-open-outline
        Description = "Switch to a scene",
        ParameterTemplate = "({SceneName})",
        Parameters = [new CommandParameter("SceneName", typeof(string))],
        // Surfaced per scene through the dynamic "Scenes" submenu.
        HiddenFromMenu = true
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx)
    {
        if (ctx.Parameters.Length != 1)
        {
            Console.WriteLine("System.ObsSetScene: invalid parameter count");
            return Task.CompletedTask;
        }

        return obs.SetScene(ctx.Parameters[0]);
    }
}

internal sealed class ObsStartStreamCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStartStream",
        DisplayName = "Start Streaming",
        Group = "OBS",
        Icon = "\U000F071B", // mdi-broadcast
        Description = "Start streaming",
        States = ObsStates.Stream
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.StreamVisuals;

    public override Task Execute(CommandContext ctx) => obs.StartStream();
}

internal sealed class ObsStopStreamCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStopStream",
        DisplayName = "Stop Streaming",
        Group = "OBS",
        Icon = "\U000F071C", // mdi-broadcast-off
        Description = "Stop streaming",
        States = ObsStates.Stream
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.StreamVisuals;

    public override Task Execute(CommandContext ctx) => obs.StopStream();
}

internal sealed class ObsToggleStudioModeCommand(IObsController obs) : ObsStatefulCommand
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsToggleStudioMode",
        DisplayName = "Toggle Studio Mode",
        Group = "OBS",
        Icon = "\U000F0493", // mdi-view-split-vertical
        Description = "Enable or disable studio mode",
        States = ObsStates.Toggle
    };

    protected override IReadOnlyDictionary<string, ObsStateVisual> Visuals => ObsStates.StudioModeVisuals;

    public override Task Execute(CommandContext ctx) => obs.ToggleStudioMode();
}

internal sealed class ObsSetPreviewSceneCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsSetPreviewScene",
        DisplayName = "Set Preview Scene",
        Group = "OBS",
        Icon = "\U000F0FCF", // mdi-movie-open-outline
        Description = "Switch the studio mode preview to a scene",
        ParameterTemplate = "({SceneName})",
        Parameters = [new CommandParameter("SceneName", typeof(string))],
        // Surfaced per scene through the dynamic "Preview Scenes" submenu.
        HiddenFromMenu = true
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx)
    {
        if (ctx.Parameters.Length != 1)
        {
            Console.WriteLine("System.ObsSetPreviewScene: invalid parameter count");
            return Task.CompletedTask;
        }

        return obs.SetPreviewScene(ctx.Parameters[0]);
    }
}

internal sealed class ObsTriggerTransitionCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsTriggerTransition",
        DisplayName = "Trigger Transition",
        Group = "OBS",
        Icon = "\U000F0526", // mdi-transition
        Description = "Transition the studio mode preview to program"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.TriggerTransition();
}

/// <summary>
/// Base for the per-input audio commands. They all take the input name as their only
/// parameter and are surfaced through the dynamic "Audio" submenu.
/// </summary>
internal abstract class ObsInputCommandBase(IObsController obs) : IPluginCommand
{
    protected IObsController Obs { get; } = obs;

    public abstract CommandDescriptor Descriptor { get; }

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx)
    {
        if (ctx.Parameters.Length != 1)
        {
            Console.WriteLine($"{Descriptor.CommandName}: invalid parameter count");
            return Task.CompletedTask;
        }

        return Execute(ctx.Parameters[0]);
    }

    protected abstract Task Execute(string inputName);

    protected static CommandDescriptor Describe(string commandName, string displayName, string icon,
        string description) => new()
    {
        CommandName = commandName,
        DisplayName = displayName,
        Group = "OBS",
        Icon = icon,
        Description = description,
        ParameterTemplate = "({InputName})",
        Parameters = [new CommandParameter("InputName", typeof(string))],
        HiddenFromMenu = true
    };
}

internal sealed class ObsMuteInputCommand(IObsController obs) : ObsInputCommandBase(obs)
{
    public override CommandDescriptor Descriptor { get; } = Describe(
        "System.ObsMuteInput", "Mute Input", "\U000F075F", "Mute an audio input"); // mdi-volume-off

    protected override Task Execute(string inputName) => Obs.SetInputMuted(inputName, true);
}

internal sealed class ObsUnmuteInputCommand(IObsController obs) : ObsInputCommandBase(obs)
{
    public override CommandDescriptor Descriptor { get; } = Describe(
        "System.ObsUnmuteInput", "Unmute Input", "\U000F057E", "Unmute an audio input"); // mdi-volume-high

    protected override Task Execute(string inputName) => Obs.SetInputMuted(inputName, false);
}

internal sealed class ObsToggleInputMuteCommand(IObsController obs) : ObsInputCommandBase(obs)
{
    public override CommandDescriptor Descriptor { get; } = Describe(
        "System.ObsToggleInputMute", "Toggle Input Mute", "\U000F0580", // mdi-volume-medium
        "Mute or unmute an audio input");

    protected override Task Execute(string inputName) => Obs.ToggleInputMute(inputName);
}

/// <summary>
/// Base for the per-source visibility commands. They take the scene and the source name
/// and are surfaced through the dynamic "Sources" submenu.
/// </summary>
internal abstract class ObsSourceCommandBase(IObsController obs) : IPluginCommand
{
    protected IObsController Obs { get; } = obs;

    public abstract CommandDescriptor Descriptor { get; }

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx)
    {
        if (ctx.Parameters.Length != 2)
        {
            Console.WriteLine($"{Descriptor.CommandName}: invalid parameter count");
            return Task.CompletedTask;
        }

        return Execute(ctx.Parameters[0], ctx.Parameters[1]);
    }

    protected abstract Task Execute(string sourceName, string sceneName);

    protected static CommandDescriptor Describe(string commandName, string displayName, string icon,
        string description) => new()
    {
        CommandName = commandName,
        DisplayName = displayName,
        Group = "OBS",
        Icon = icon,
        Description = description,
        ParameterTemplate = "({SourceName},{SceneName})",
        Parameters =
        [
            new CommandParameter("SourceName", typeof(string)),
            // Optional: the placeholder means "the current program scene". The host fills
            // only the first parameter from a menu selection, so the scene has to default.
            new CommandParameter("SceneName", typeof(string))
            {
                DefaultValue = ObsController.CurrentScenePlaceholder
            }
        ],
        HiddenFromMenu = true
    };
}

internal sealed class ObsShowSourceCommand(IObsController obs) : ObsSourceCommandBase(obs)
{
    public override CommandDescriptor Descriptor { get; } = Describe(
        "System.ObsShowSource", "Show Source", "\U000F0208", // mdi-eye
        "Show a source (in the current scene unless a scene is set)");

    protected override Task Execute(string sourceName, string sceneName) =>
        Obs.SetSourceVisible(sourceName, sceneName, true);
}

internal sealed class ObsHideSourceCommand(IObsController obs) : ObsSourceCommandBase(obs)
{
    public override CommandDescriptor Descriptor { get; } = Describe(
        "System.ObsHideSource", "Hide Source", "\U000F0209", // mdi-eye-off
        "Hide a source (in the current scene unless a scene is set)");

    protected override Task Execute(string sourceName, string sceneName) =>
        Obs.SetSourceVisible(sourceName, sceneName, false);
}

internal sealed class ObsToggleSourceCommand(IObsController obs) : ObsSourceCommandBase(obs)
{
    public override CommandDescriptor Descriptor { get; } = Describe(
        "System.ObsToggleSource", "Toggle Source", "\U000F0D04", // mdi-eye-check
        "Show or hide a source (in the current scene unless a scene is set)");

    protected override Task Execute(string sourceName, string sceneName) =>
        Obs.ToggleSource(sourceName, sceneName);
}
