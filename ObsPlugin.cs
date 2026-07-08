using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// Entry point of the OBS Studio plugin. Contributes the OBS commands, a live
/// "Scenes" submenu and a connection settings page.
/// </summary>
public sealed class ObsPlugin : LoupixPlugin, IMenuContributor, IPluginSettingsPage
{
    private const string KeyIp = "ip";
    private const string KeyPort = "port";
    private const string KeyPassword = "password";

    private readonly ObsController _controller = new();
    private IPluginHost? _host;

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "obs",
        Name = "OBS Studio",
        Version = new Version(1, 0, 0),
        SdkVersion = new Version(1, 16, 0),
        Author = "RadiatorTwo",
        Description = "Control OBS Studio (recording, replay buffer, virtual camera, scenes) via obs-websocket."
    };

    public override void Initialize(IPluginHost host)
    {
        _host = host;
        ApplySettings();
        _controller.Connect();
    }

    public override void Shutdown()
    {
        _controller.Disconnect();
    }

    public override IEnumerable<IPluginCommand> GetCommands()
    {
        return
        [
            new ObsStartRecordCommand(_controller),
            new ObsStopRecordCommand(_controller),
            new ObsPauseRecordCommand(_controller),
            new ObsVirtualCamCommand(_controller),
            new ObsStartReplayCommand(_controller),
            new ObsStopReplayCommand(_controller),
            new ObsSaveReplayCommand(_controller),
            new ObsSetSceneCommand(_controller)
        ];
    }

    public override IReadOnlyList<CommandGroupDescriptor> GetCommandGroups() =>
    [
        new CommandGroupDescriptor
        {
            Group = "OBS",
            Description = "Scene, source and stream control",
            Icon = "\U000F0567", // mdi-video
            Section = CommandGroupSection.Plugins
        }
    ];

    // ───────── IMenuContributor — dynamic "Scenes" submenu ─────────

    public async Task<IReadOnlyList<MenuNode>> GetMenuNodes(ButtonTargets target)
    {
        var scenesChildren = new List<MenuNode>();

        try
        {
            var scenes = await _controller.GetScenes();
            foreach (var scene in scenes)
            {
                // The scene name is the command's first parameter; the host's
                // command builder fills it from the node name.
                scenesChildren.Add(new MenuNode
                {
                    Name = scene.Name,
                    CommandName = "System.ObsSetScene"
                });
            }
        }
        catch (Exception ex)
        {
            scenesChildren.Add(new MenuNode { Name = $"OBS not connected: {ex.Message}" });
        }

        var scenesFolder = new MenuNode { Name = "Scenes", Children = scenesChildren };

        // Returned under the "OBS" group so the host merges it into the OBS
        // group built from the static commands.
        return [new MenuNode { Name = "OBS", Children = [scenesFolder] }];
    }

    // ───────── IPluginSettingsPage — connection settings ─────────

    public IReadOnlyList<PluginSettingDescriptor> SettingsSchema { get; } =
    [
        new PluginSettingDescriptor
        {
            Key = KeyIp, Label = "Host / IP", Kind = PluginSettingKind.Text,
            DefaultValue = "127.0.0.1", Description = "Address of the OBS WebSocket server."
        },
        new PluginSettingDescriptor
        {
            Key = KeyPort, Label = "Port", Kind = PluginSettingKind.Number,
            DefaultValue = 4455L
        },
        new PluginSettingDescriptor
        {
            Key = KeyPassword, Label = "Password", Kind = PluginSettingKind.Password,
            DefaultValue = ""
        }
    ];

    public IReadOnlyList<PluginSettingAction> SettingsActions => _settingsActions ??=
    [
        new PluginSettingAction
        {
            Label = "Test Connection",
            Invoke = async () =>
            {
                ApplySettings();
                try
                {
                    await _controller.ConnectAndWaitAsync();
                    return "Connected";
                }
                catch (Exception ex)
                {
                    return $"Failed: {ex.Message}";
                }
            }
        }
    ];

    private IReadOnlyList<PluginSettingAction>? _settingsActions;

    public void OnSettingsSaved()
    {
        ApplySettings();
        _controller.Connect();
    }

    private void ApplySettings()
    {
        if (_host == null)
            return;

        var ip = _host.Settings.Get(KeyIp, "127.0.0.1") ?? "127.0.0.1";
        var port = (int)_host.Settings.Get(KeyPort, 4455L);
        var password = _host.Settings.Get(KeyPassword, string.Empty) ?? string.Empty;

        _controller.Configure(ip, port, password);
    }
}
