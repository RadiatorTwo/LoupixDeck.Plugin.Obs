# LoupixDeck.Plugin.Obs

OBS Studio integration plugin for [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck),
built against [LoupixDeck.PluginSdk](https://github.com/RadiatorTwo/LoupixDeck.PluginSdk).

## Commands

`System.ObsStartRecord`, `System.ObsStopRecord`, `System.ObsPauseRecord`,
`System.ObsVirtualCam`, `System.ObsStartReplay`, `System.ObsStopReplay`,
`System.ObsSaveReplay`, `System.ObsSetScene` (one menu entry per live scene).

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
