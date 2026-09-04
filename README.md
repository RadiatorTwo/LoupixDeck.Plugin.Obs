# LoupixDeck.Plugin.Obs

OBS Studio integration plugin for [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck),
built against [LoupixDeck.PluginSdk](https://github.com/RadiatorTwo/LoupixDeck.PluginSdk).

## Commands

Toggles, which also show the live state on a button (see below):
`System.ObsToggleRecord`, `System.ObsToggleReplay`, `System.ObsVirtualCam`,
`System.ObsToggleStream`, `System.ObsToggleStudioMode`.

The discrete actions behind them:
`System.ObsStartRecord`, `System.ObsStopRecord`, `System.ObsPauseRecord`,
`System.ObsStartReplay`, `System.ObsStopReplay`, `System.ObsSaveReplay`,
`System.ObsStartStream`, `System.ObsStopStream`.

Studio mode: `System.ObsTriggerTransition`, `System.ObsSetPreviewScene`.

Scene, audio and source control, surfaced through the dynamic "Scenes",
"Preview Scenes", "Audio" and "Sources" submenus:
`System.ObsSetScene`, `System.ObsMuteInput`, `System.ObsUnmuteInput`,
`System.ObsToggleInputMute`, `System.ObsShowSource`, `System.ObsHideSource`,
`System.ObsToggleSource`.

The source commands take the source name plus an optional scene name; left at
its `<current>` default the command follows the current program scene.

## Button states

The five toggle commands declare their own states, so assigning one to a button
creates them: `Idle` / `Recording` / `Paused` for recording, `Off` / `On` for the
replay buffer, virtual camera and studio mode, `Offline` / `Live` for streaming.
The button then follows what OBS is actually doing, including changes made in OBS
itself, and each state draws its own indicator. State management is locked while
such a command is assigned — the layers inside every state stay yours, so put your
own text, icon or background around the indicator.

The discrete actions (start, stop, pause, save) carry no states: they are one-way
commands, and which state a button should show would be a guess. A button whose
states you built by hand keeps working as before — the plugin sets a state whose
name matches, and leaves everything else alone.

Scene, audio and source buttons carry no state either: every one of them shares a
single command name, which the host's state API cannot tell apart.

Requires LoupixDeck with SDK 1.21.0 or newer.

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
