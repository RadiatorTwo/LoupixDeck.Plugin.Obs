using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// Base for the OBS commands that declare their own button states: the host creates the declared
/// states with their layers when the command is assigned, and <see cref="ObsStateReporter"/>
/// selects the active one from the live OBS state. Nothing is drawn by the plugin.
/// </summary>
/// <remarks>
/// State changes arrive as a push from OBS. A button that missed the push — one assigned, or
/// whose editor was open, after OBS last changed — is put right when the host asks
/// <see cref="GetButtonState"/>; nothing is polled.
/// </remarks>
internal abstract class ObsStatefulCommand(ObsStateReporter states) : IButtonStateCommand
{
    public abstract CommandDescriptor Descriptor { get; }

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public abstract Task Execute(CommandContext ctx);

    /// <summary>The state OBS last reported; null before it reported one, which leaves the button alone.</summary>
    public string? GetButtonState(CommandContext ctx) => states.GetState(Descriptor.CommandName);
}
