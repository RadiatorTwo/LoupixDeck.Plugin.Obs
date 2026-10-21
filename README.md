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

Scene, audio, source and filter control, surfaced through the dynamic "Scenes",
"Preview Scenes", "Audio", "Sources" and "Filters" submenus:
`System.ObsSetScene`, `System.ObsMuteInput`, `System.ObsUnmuteInput`,
`System.ObsToggleInputMute`, `System.ObsShowSource`, `System.ObsHideSource`,
`System.ObsToggleSource`, `System.ObsShowFilter`, `System.ObsHideFilter`,
`System.ObsToggleFilter`.

The source commands take the source name plus an optional scene name; left at
its `<current>` default the command follows the current program scene. The filter
commands take the scene or source that carries the filter, and the filter name.

## Button states

The five toggle commands declare their own states, so assigning one to a button
creates them: `Idle` / `Recording` for recording, `Off` / `On` for the
replay buffer, virtual camera and studio mode, `Offline` / `Live` for streaming.
The button then follows what OBS is actually doing, including changes made in OBS
itself. Each state starts with its own icon, caption and background colour; these
are ordinary layers, so restyle or replace them as you like. State management is
locked while such a command is assigned — the layers inside every state stay yours.

The starting layers are only created when the command is put on a button. Buttons
assigned with an older plugin version keep their states but no longer get a drawn
indicator; assign the command again to get the new layers.

A paused recording counts as recording — pausing has its own command, so the toggle
only says whether OBS is recording at all.

The discrete actions (start, stop, pause, save) carry no states: they are one-way
commands, and which state a button should show would be a guess. A button whose
states you built by hand keeps working as before — the plugin sets a state whose
name matches, and leaves everything else alone.

Scene, audio, source and filter buttons carry no state either: every one of them shares a
single command name, which the host's state API cannot tell apart.

Requires LoupixDeck with SDK 1.30.0 or newer.

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
