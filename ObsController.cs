using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;

namespace LoupixDeck.Plugin.Obs;

/// <summary>Thin wrapper over obs-websocket-dotnet used by the OBS plugin.</summary>
public interface IObsController
{
    /// <summary>Sets the connection parameters used by subsequent connects.</summary>
    void Configure(string ip, int port, string password);

    /// <summary>Connects in the background (fire and forget).</summary>
    void Connect();

    /// <summary>Connects and waits for the result, with a short timeout.</summary>
    Task ConnectAndWaitAsync(CancellationToken cancellationToken = default);

    void Disconnect();

    Task ToggleVirtualCamera();
    Task StartRecording();
    Task StopRecording();
    Task PauseRecording();
    Task StartReplayBuffer();
    Task StopReplayBuffer();
    Task SaveReplayBuffer();
    Task SetScene(string sceneName);
    Task<List<SceneBasicInfo>> GetScenes();
}

/// <inheritdoc cref="IObsController"/>
public sealed class ObsController : IObsController
{
    private readonly OBSWebsocket _obs = new();

    private string _ip = "127.0.0.1";
    private int _port = 4455;
    private string _password = string.Empty;

    private string Url => $"ws://{_ip}:{_port}";

    public void Configure(string ip, int port, string password)
    {
        _ip = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
        _port = port > 0 ? port : 4455;
        _password = password ?? string.Empty;
    }

    public void Connect()
    {
        try
        {
            if (_obs.IsConnected)
                Disconnect();

            _obs.ConnectAsync(Url, _password);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to OBS: {ex.Message}");
        }
    }

    public async Task ConnectAndWaitAsync(CancellationToken cancellationToken = default)
    {
        if (_obs.IsConnected)
            return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _obs.Connected += OnConnected;
        _obs.Disconnected += OnDisconnected;

        _obs.ConnectAsync(Url, _password);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);

        await using (linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token)))
        {
            await tcs.Task.ConfigureAwait(false);
        }

        return;

        void OnConnected(object? _, EventArgs __)
        {
            Unsubscribe();
            tcs.TrySetResult();
        }

        void OnDisconnected(object? _, ObsDisconnectionInfo info)
        {
            Unsubscribe();
            tcs.TrySetException(new InvalidOperationException(info.DisconnectReason ?? "OBS disconnected."));
        }

        void Unsubscribe()
        {
            _obs.Connected -= OnConnected;
            _obs.Disconnected -= OnDisconnected;
        }
    }

    public void Disconnect()
    {
        if (_obs.IsConnected)
            _obs.Disconnect();
    }

    private async Task<bool> CheckConnection()
    {
        if (_obs.IsConnected)
            return true;

        try
        {
            await ConnectAndWaitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to OBS: {ex.Message}");
            return false;
        }

        return _obs.IsConnected;
    }

    public async Task ToggleVirtualCamera()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.ToggleVirtualCam(), "toggling virtual camera");
    }

    public async Task StartRecording()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StartRecord(), "starting recording");
    }

    public async Task StopRecording()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StopRecord(), "stopping recording");
    }

    public async Task PauseRecording()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.PauseRecord(), "pausing recording");
    }

    public async Task StartReplayBuffer()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StartReplayBuffer(), "starting replay buffer");
    }

    public async Task StopReplayBuffer()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.StopReplayBuffer(), "stopping replay buffer");
    }

    public async Task SaveReplayBuffer()
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.SaveReplayBuffer(), "saving replay buffer");
    }

    public async Task SetScene(string sceneName)
    {
        if (await CheckConnection().ConfigureAwait(false))
            Guarded(() => _obs.SetCurrentProgramScene(sceneName), $"setting scene '{sceneName}'");
    }

    public async Task<List<SceneBasicInfo>> GetScenes()
    {
        if (!await CheckConnection().ConfigureAwait(false))
            return [];

        try
        {
            return _obs.GetSceneList().Scenes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting OBS scenes: {ex.Message}");
            return [];
        }
    }

    private static void Guarded(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error {what}: {ex.Message}");
        }
    }
}
