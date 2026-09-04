using LoupixDeck.PluginSdk;
using OBSWebsocketDotNet.Types;

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
    private ObsStateReporter? _stateReporter;

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "obs",
        Name = "OBS Studio",
        Version = new Version(1, 2, 0),
        SdkVersion = new Version(1, 21, 0),
        Author = "RadiatorTwo",
        Description = "Control OBS Studio (recording, replay buffer, virtual camera, scenes) via obs-websocket."
    };

    public override void Initialize(IPluginHost host)
    {
        _host = host;
        ApplySettings();
        _stateReporter = new ObsStateReporter(host, _controller);
        _controller.Connect();
    }

    public override void Shutdown()
    {
        _stateReporter?.Dispose();
        _stateReporter = null;
        _controller.Disconnect();
    }

    public override IEnumerable<IPluginCommand> GetCommands()
    {
        return
        [
            new ObsToggleRecordCommand(_controller),
            new ObsStartRecordCommand(_controller),
            new ObsStopRecordCommand(_controller),
            new ObsPauseRecordCommand(_controller),
            new ObsVirtualCamCommand(_controller),
            new ObsToggleReplayCommand(_controller),
            new ObsStartReplayCommand(_controller),
            new ObsStopReplayCommand(_controller),
            new ObsSaveReplayCommand(_controller),
            new ObsSetSceneCommand(_controller),
            new ObsToggleStreamCommand(_controller),
            new ObsStartStreamCommand(_controller),
            new ObsStopStreamCommand(_controller),
            new ObsToggleStudioModeCommand(_controller),
            new ObsSetPreviewSceneCommand(_controller),
            new ObsTriggerTransitionCommand(_controller),
            new ObsMuteInputCommand(_controller),
            new ObsUnmuteInputCommand(_controller),
            new ObsToggleInputMuteCommand(_controller),
            new ObsShowSourceCommand(_controller),
            new ObsHideSourceCommand(_controller),
            new ObsToggleSourceCommand(_controller)
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

    // ───────── IMenuContributor — dynamic submenus ─────────

    public async Task<IReadOnlyList<MenuNode>> GetMenuNodes(ButtonTargets target)
    {
        List<SceneBasicInfo> scenes;

        try
        {
            scenes = await _controller.GetScenes();
        }
        catch (Exception ex)
        {
            return
            [
                new MenuNode
                {
                    Name = "OBS",
                    Children = [new MenuNode { Name = $"OBS not connected: {ex.Message}" }]
                }
            ];
        }

        // Returned under the "OBS" group so the host merges it into the OBS
        // group built from the static commands.
        return
        [
            new MenuNode
            {
                Name = "OBS",
                Children =
                [
                    BuildSceneFolder("Scenes", "System.ObsSetScene", scenes),
                    BuildSceneFolder("Preview Scenes", "System.ObsSetPreviewScene", scenes),
                    await BuildAudioFolder(),
                    await BuildSourcesFolder(scenes)
                ]
            }
        ];
    }

    /// <summary>One leaf per scene; the scene name is the command's only parameter.</summary>
    private static MenuNode BuildSceneFolder(string folderName, string commandName,
        List<SceneBasicInfo> scenes)
    {
        List<MenuNode> children = scenes
            .Select(scene => new MenuNode
            {
                Name = scene.Name,
                CommandName = commandName,
                Parameters = new Dictionary<string, string> { ["SceneName"] = scene.Name }
            })
            .ToList();

        return new MenuNode { Name = folderName, Children = children };
    }

    /// <summary>One folder per input, offering mute / unmute / toggle.</summary>
    private async Task<MenuNode> BuildAudioFolder()
    {
        List<MenuNode> children = [];

        try
        {
            foreach (string input in await _controller.GetInputNames())
            {
                Dictionary<string, string> parameters = new() { ["InputName"] = input };

                children.Add(new MenuNode
                {
                    Name = input,
                    Children =
                    [
                        new MenuNode { Name = "Mute", CommandName = "System.ObsMuteInput", Parameters = parameters },
                        new MenuNode { Name = "Unmute", CommandName = "System.ObsUnmuteInput", Parameters = parameters },
                        new MenuNode
                        {
                            Name = "Toggle Mute",
                            CommandName = "System.ObsToggleInputMute",
                            Parameters = parameters
                        }
                    ]
                });
            }
        }
        catch (Exception ex)
        {
            children.Add(new MenuNode { Name = $"Could not read inputs: {ex.Message}" });
        }

        return new MenuNode { Name = "Audio", Children = children };
    }

    /// <summary>One folder per scene, each holding a folder per source with show / hide / toggle.</summary>
    private async Task<MenuNode> BuildSourcesFolder(List<SceneBasicInfo> scenes)
    {
        List<MenuNode> sceneFolders = [];

        foreach (SceneBasicInfo scene in scenes)
        {
            List<MenuNode> sourceFolders = [];

            try
            {
                foreach (string source in await _controller.GetSourceNames(scene.Name))
                {
                    // Only the first parameter is filled from a menu selection, so the
                    // scene stays at its "current program scene" default; the scene folders
                    // are there to find the source, not to pin it.
                    Dictionary<string, string> parameters = new() { ["SourceName"] = source };

                    sourceFolders.Add(new MenuNode
                    {
                        Name = source,
                        Children =
                        [
                            new MenuNode { Name = "Show", CommandName = "System.ObsShowSource", Parameters = parameters },
                            new MenuNode { Name = "Hide", CommandName = "System.ObsHideSource", Parameters = parameters },
                            new MenuNode { Name = "Toggle", CommandName = "System.ObsToggleSource", Parameters = parameters }
                        ]
                    });
                }
            }
            catch (Exception ex)
            {
                sourceFolders.Add(new MenuNode { Name = $"Could not read sources: {ex.Message}" });
            }

            sceneFolders.Add(new MenuNode { Name = scene.Name, Children = sourceFolders });
        }

        return new MenuNode { Name = "Sources", Children = sceneFolders };
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
