using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// The button states the OBS commands declare, and how each one looks. Every command of a group
/// declares the same states — a button is addressed by its command name, so any member of a group
/// may be the one the user assigned.
/// </summary>
/// <remarks>
/// The names are persisted in the button config and used by <see cref="ObsStateReporter"/> to
/// select the active state, so they must not change once shipped.
/// </remarks>
internal static class ObsStates
{
    // Recording.
    public const string Idle = "Idle";
    public const string Recording = "Recording";
    public const string Paused = "Paused";

    // Replay buffer, virtual camera, studio mode.
    public const string Off = "Off";
    public const string On = "On";

    // Streaming.
    public const string Offline = "Offline";
    public const string Live = "Live";

    private static readonly PluginColor Inactive = PluginColor.FromRgb(0x60, 0x66, 0x70);
    private static readonly PluginColor Red = PluginColor.FromRgb(0xE0, 0x3B, 0x3B);
    private static readonly PluginColor Amber = PluginColor.FromRgb(0xE0, 0x9B, 0x2B);
    private static readonly PluginColor Green = PluginColor.FromRgb(0x36, 0xB3, 0x5E);
    private static readonly PluginColor Blue = PluginColor.FromRgb(0x2E, 0x8B, 0xE0);
    private static readonly PluginColor Purple = PluginColor.FromRgb(0x8B, 0x5C, 0xE0);

    /// <summary>Recording: idle, running, paused — in the order the reporter's indices use.</summary>
    public static IReadOnlyList<ButtonStateDescriptor> Record { get; } =
    [
        new() { Name = Idle, Description = "Not recording" },
        new() { Name = Recording, Description = "Recording" },
        new() { Name = Paused, Description = "Recording paused" }
    ];

    /// <summary>A plain on/off output: replay buffer, virtual camera, studio mode.</summary>
    public static IReadOnlyList<ButtonStateDescriptor> Toggle { get; } =
    [
        new() { Name = Off, Description = "Not running" },
        new() { Name = On, Description = "Running" }
    ];

    /// <summary>Streaming.</summary>
    public static IReadOnlyList<ButtonStateDescriptor> Stream { get; } =
    [
        new() { Name = Offline, Description = "Not streaming" },
        new() { Name = Live, Description = "Streaming" }
    ];

    public static IReadOnlyDictionary<string, ObsStateVisual> RecordVisuals { get; } =
        new Dictionary<string, ObsStateVisual>(StringComparer.OrdinalIgnoreCase)
        {
            [Idle] = new("REC", Inactive, false),
            [Recording] = new("REC", Red, true),
            [Paused] = new("PAUSE", Amber, true)
        };

    public static IReadOnlyDictionary<string, ObsStateVisual> ReplayVisuals { get; } =
        new Dictionary<string, ObsStateVisual>(StringComparer.OrdinalIgnoreCase)
        {
            [Off] = new("REPLAY", Inactive, false),
            [On] = new("REPLAY", Blue, true)
        };

    public static IReadOnlyDictionary<string, ObsStateVisual> VirtualCamVisuals { get; } =
        new Dictionary<string, ObsStateVisual>(StringComparer.OrdinalIgnoreCase)
        {
            [Off] = new("CAM", Inactive, false),
            [On] = new("CAM", Green, true)
        };

    public static IReadOnlyDictionary<string, ObsStateVisual> StreamVisuals { get; } =
        new Dictionary<string, ObsStateVisual>(StringComparer.OrdinalIgnoreCase)
        {
            [Offline] = new("STREAM", Inactive, false),
            [Live] = new("LIVE", Red, true)
        };

    public static IReadOnlyDictionary<string, ObsStateVisual> StudioModeVisuals { get; } =
        new Dictionary<string, ObsStateVisual>(StringComparer.OrdinalIgnoreCase)
        {
            [Off] = new("STUDIO", Inactive, false),
            [On] = new("STUDIO", Purple, true)
        };
}

/// <summary>How one state is drawn: a label, an accent color and whether the dot is filled.</summary>
/// <param name="Label">Short caption under the indicator.</param>
/// <param name="Accent">Indicator and caption color.</param>
/// <param name="Active">True draws a filled indicator, false an outlined one.</param>
internal readonly record struct ObsStateVisual(string Label, PluginColor Accent, bool Active);
