using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// The OBS plugin commands. Command names are kept identical to the former
/// built-in commands so existing button assignments in <c>config.json</c>
/// keep working.
/// </summary>
internal sealed class ObsStartRecordCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStartRecord",
        DisplayName = "Start Recording",
        Group = "OBS"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.StartRecording();
}

internal sealed class ObsStopRecordCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStopRecord",
        DisplayName = "Stop Recording",
        Group = "OBS"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.StopRecording();
}

internal sealed class ObsPauseRecordCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsPauseRecord",
        DisplayName = "Pause Recording",
        Group = "OBS"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.PauseRecording();
}

internal sealed class ObsVirtualCamCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsVirtualCam",
        DisplayName = "Toggle Virtual Camera",
        Group = "OBS"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.ToggleVirtualCamera();
}

internal sealed class ObsStartReplayCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStartReplay",
        DisplayName = "Start Replay",
        Group = "OBS"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.StartReplayBuffer();
}

internal sealed class ObsStopReplayCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsStopReplay",
        DisplayName = "Stop Replay",
        Group = "OBS"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.StopReplayBuffer();
}

internal sealed class ObsSaveReplayCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsSaveReplay",
        DisplayName = "Save Replay",
        Group = "OBS"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public Task Execute(CommandContext ctx) => obs.SaveReplayBuffer();
}

internal sealed class ObsSetSceneCommand(IObsController obs) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "System.ObsSetScene",
        DisplayName = "Set Scene",
        Group = "OBS",
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
