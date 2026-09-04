# LoupixDeck.Plugin.Obs

OBS Studio integration plugin for [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck),
built against [LoupixDeck.PluginSdk](https://github.com/RadiatorTwo/LoupixDeck.PluginSdk).

## Commands

Recording, replay buffer, virtual camera, streaming:
`System.ObsStartRecord`, `System.ObsStopRecord`, `System.ObsPauseRecord`,
`System.ObsStartReplay`, `System.ObsStopReplay`, `System.ObsSaveReplay`,
`System.ObsVirtualCam`, `System.ObsStartStream`, `System.ObsStopStream`.

Studio mode: `System.ObsToggleStudioMode`, `System.ObsTriggerTransition`,
`System.ObsSetPreviewScene`.

Scene, audio and source control, surfaced through the dynamic "Scenes",
"Preview Scenes", "Audio" and "Sources" submenus:
`System.ObsSetScene`, `System.ObsMuteInput`, `System.ObsUnmuteInput`,
`System.ObsToggleInputMute`, `System.ObsShowSource`, `System.ObsHideSource`,
`System.ObsToggleSource`.

The source commands take the source name plus an optional scene name; left at
its `<current>` default the command follows the current program scene.

## Button states

Recording, replay buffer, virtual camera, streaming and studio mode report
their live state to the host, so a stateful button bound to one of those
commands follows what OBS is actually doing — including changes made in OBS
itself. Create the states on the button and name them as you like: the plugin
matches common names ("Idle"/"Recording", "Off"/"On", "Aus"/"An", "Paused")
and otherwise goes by position — first state inactive, second active, third
(recording only) paused.

Scene, audio and source buttons carry no state: every one of them shares a
single command name, which the host's state API cannot tell apart.

## Settings

Configured in LoupixDeck's plugin settings: host/IP, port, password of the
OBS WebSocket server (OBS → Tools → WebSocket Server Settings). Stored in
`plugins/obs/settings.json`.

## Build & deploy

```bash
dotnet build LoupixDeck.Plugin.Obs.csproj -c Release
```

Copy the build output (the DLLs, `LoupixDeck.Plugin.Obs.deps.json`) together
with `plugin.json` into `LoupixDeck/plugins/obs/`.
