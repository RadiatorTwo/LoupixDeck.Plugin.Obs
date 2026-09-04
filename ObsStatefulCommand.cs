using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// Base for the OBS commands that declare their own button states and draw them: the host creates
/// the declared states when the command is assigned, <see cref="ObsStateReporter"/> selects the
/// active one from the live OBS state, and this class renders it.
/// </summary>
/// <remarks>
/// The picture only changes when OBS changes, which arrives as a push
/// (<see cref="IPluginHost.SetActiveButtonState"/> plus the host's own re-render on a state
/// switch), so the poll interval is only a slow safety net.
/// </remarks>
internal abstract class ObsStatefulCommand : IDisplayImageCommand
{
    public abstract CommandDescriptor Descriptor { get; }

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

    public abstract Task Execute(CommandContext ctx);

    /// <summary>How each declared state is drawn, keyed by state name.</summary>
    protected abstract IReadOnlyDictionary<string, ObsStateVisual> Visuals { get; }

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        // No state (a button whose states the user manages himself) → leave his content alone.
        if (ctx.StateName == null || !Visuals.TryGetValue(ctx.StateName, out ObsStateVisual visual))
            return false;

        int size = Math.Min(canvas.Width, canvas.Height);
        int radius = Math.Max(6, size / 6);
        int centerX = canvas.Width / 2;
        int centerY = (canvas.Height / 2) - (size / 10);

        // Nothing is cleared: the canvas starts fully transparent and the indicator is composited
        // over whatever the state already shows — the button's background color and the user's own
        // layers stay visible.
        if (visual.Active)
            canvas.FillCircle(centerX, centerY, radius, visual.Accent);
        else
            canvas.DrawCircle(centerX, centerY, radius, Math.Max(2, radius / 4), visual.Accent);

        int labelHeight = Math.Max(14, size / 4);
        canvas.DrawText(visual.Label, 0, canvas.Height - labelHeight - (size / 12), canvas.Width, labelHeight,
            visual.Accent, size / 6f, bold: true);

        return true;
    }
}
