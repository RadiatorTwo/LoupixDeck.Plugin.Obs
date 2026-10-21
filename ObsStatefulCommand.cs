using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// Base for the OBS commands that declare their own button states: the host creates the declared
/// states with their layers when the command is assigned, and <see cref="ObsStateReporter"/>
/// selects the active one from the live OBS state. Nothing is drawn by the plugin.
/// </summary>
/// <remarks>
/// State changes arrive as a push from OBS. The value poll only puts a button right that missed
/// the push — one assigned, reset or loaded after OBS last changed — so it reports no value and
/// only switches the state when it differs from what OBS last reported.
/// </remarks>
internal abstract class ObsStatefulCommand(ObsStateReporter states) : IValueDisplayCommand
{
    public abstract CommandDescriptor Descriptor { get; }

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(2);

    public abstract Task Execute(CommandContext ctx);

    public AdjustmentValue? GetValue(CommandContext ctx)
    {
        string? target = states.GetState(Descriptor.CommandName);

        // No state on this button (its states are the user's own), or OBS has not reported yet:
        // leave it alone.
        if (target != null && ctx.StateName != null &&
            !string.Equals(ctx.StateName, target, StringComparison.OrdinalIgnoreCase))
            ctx.Host.SetActiveButtonState(Descriptor.CommandName, target);

        return null;
    }
}
