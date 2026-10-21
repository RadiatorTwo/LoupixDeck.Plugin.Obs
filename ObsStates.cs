using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// The button states the OBS commands declare, and the layers each one starts with. Every command
/// of a group declares the same state names — a button is addressed by its command name, so any
/// member of a group may be the one the user assigned.
/// </summary>
/// <remarks>
/// The names are persisted in the button config and used by <see cref="ObsStateReporter"/> to
/// select the active state, so they must not change once shipped. The layouts are only the
/// starting point: the host creates them once when the command is put on a button, and from then
/// on they are ordinary layers the user can restyle.
/// </remarks>
internal static class ObsStates
{
    // Recording.
    public const string Idle = "Idle";
    public const string Recording = "Recording";

    // Replay buffer, virtual camera, studio mode.
    public const string Off = "Off";
    public const string On = "On";

    // Streaming.
    public const string Offline = "Offline";
    public const string Live = "Live";

    // Scenes: on program (Active) or in the studio mode preview (Preview).
    public const string Inactive = "Inactive";
    public const string Active = "Active";
    public const string Preview = "Preview";

    // Sources and filters.
    public const string Hidden = "Hidden";
    public const string Shown = "Shown";

    // Audio inputs.
    public const string Unmuted = "Unmuted";
    public const string Muted = "Muted";

    private const string Foreground = "#FFFFFF";
    private const string InactiveBackground = "#2B2F36";
    private const string LiveBackground = "#B3261E";
    private const string EnabledBackground = "#1F6E43";

    private const string RecordGlyph = "\U000F044B"; // mdi-record-rec
    private const string BroadcastGlyph = "\U000F1720"; // mdi-broadcast
    private const string BroadcastOffGlyph = "\U000F1721"; // mdi-broadcast-off
    private const string ReplayGlyph = "\U000F0459"; // mdi-replay
    private const string WebcamGlyph = "\U000F05A0"; // mdi-webcam
    private const string WebcamOffGlyph = "\U000F1737"; // mdi-webcam-off
    private const string StudioModeGlyph = "\U000F0BCC"; // mdi-view-split-vertical
    private const string SceneGlyph = "\U000F0FCF"; // mdi-movie-open-outline
    private const string EyeGlyph = "\U000F0208"; // mdi-eye
    private const string EyeOffGlyph = "\U000F0209"; // mdi-eye-off
    private const string VolumeGlyph = "\U000F057E"; // mdi-volume-high
    private const string VolumeOffGlyph = "\U000F075F"; // mdi-volume-off

    /// <summary>
    /// Recording. A paused recording is still a recording, and pausing has its own command, so
    /// there is no separate paused state here.
    /// </summary>
    public static IReadOnlyList<ButtonStateDescriptor> Record { get; } =
    [
        State(Idle, "Not recording", RecordGlyph, "Record", InactiveBackground),
        State(Recording, "Recording", RecordGlyph, "Recording", LiveBackground)
    ];

    /// <summary>Streaming.</summary>
    public static IReadOnlyList<ButtonStateDescriptor> Stream { get; } =
    [
        State(Offline, "Not streaming", BroadcastOffGlyph, "Go live", InactiveBackground),
        State(Live, "Streaming", BroadcastGlyph, "Live", LiveBackground)
    ];

    public static IReadOnlyList<ButtonStateDescriptor> Replay { get; } =
    [
        State(Off, "Not running", ReplayGlyph, "Replay", InactiveBackground),
        State(On, "Running", ReplayGlyph, "Replay", EnabledBackground)
    ];

    public static IReadOnlyList<ButtonStateDescriptor> VirtualCam { get; } =
    [
        State(Off, "Not running", WebcamOffGlyph, "Virtual cam", InactiveBackground),
        State(On, "Running", WebcamGlyph, "Virtual cam", EnabledBackground)
    ];

    public static IReadOnlyList<ButtonStateDescriptor> StudioMode { get; } =
    [
        State(Off, "Disabled", StudioModeGlyph, "Studio mode", InactiveBackground),
        State(On, "Enabled", StudioModeGlyph, "Studio mode", EnabledBackground)
    ];

    // The captions below name the button's own scene, source, filter or input: the host replaces
    // the {ParameterName} placeholder with the button's parameter when it creates the layers.

    public static IReadOnlyList<ButtonStateDescriptor> Scene { get; } =
    [
        State(Inactive, "Not on program", SceneGlyph, "{SceneName}", InactiveBackground),
        State(Active, "On program", SceneGlyph, "{SceneName}", LiveBackground)
    ];

    public static IReadOnlyList<ButtonStateDescriptor> PreviewScene { get; } =
    [
        State(Inactive, "Not in the preview", SceneGlyph, "{SceneName}", InactiveBackground),
        State(Preview, "In the studio mode preview", SceneGlyph, "{SceneName}", EnabledBackground)
    ];

    public static IReadOnlyList<ButtonStateDescriptor> Source { get; } =
    [
        State(Hidden, "Hidden", EyeOffGlyph, "{SourceName}", InactiveBackground),
        State(Shown, "Shown", EyeGlyph, "{SourceName}", EnabledBackground)
    ];

    public static IReadOnlyList<ButtonStateDescriptor> Filter { get; } =
    [
        State(Hidden, "Disabled", EyeOffGlyph, "{FilterName}", InactiveBackground),
        State(Shown, "Enabled", EyeGlyph, "{FilterName}", EnabledBackground)
    ];

    public static IReadOnlyList<ButtonStateDescriptor> InputMute { get; } =
    [
        State(Unmuted, "Not muted", VolumeGlyph, "{InputName}", InactiveBackground),
        State(Muted, "Muted", VolumeOffGlyph, "{InputName}", LiveBackground)
    ];

    /// <summary>
    /// One state with an icon over a caption on a coloured background — the host's standard
    /// icon-and-caption geometry, so the key looks like any other until the user restyles it.
    /// </summary>
    private static ButtonStateDescriptor State(string name, string description, string glyph, string caption,
        string background) => new()
    {
        Name = name,
        Description = description,
        Layout = new ButtonLayoutDescriptor
        {
            Mode = ButtonLayoutMode.Custom,
            BackgroundColor = background,
            Layers =
            [
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Symbol, Name = "Icon", Glyph = glyph,
                    IconScale = 46 / 90.0, OffsetY = -8, Color = Foreground
                },
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Text, Name = "Caption", Text = caption, TextSize = 11,
                    OffsetY = 27, BoxWidth = 88, BoxHeight = 22, Color = Foreground
                }
            ]
        }
    };
}
