﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using VRCOSC.Game.Config;
using VRCOSC.Game.Design;
using VRCOSC.Game.Graphics.Notifications;
using VRCOSC.OSC;

namespace VRCOSC.Game.Modules;

public partial class GameManager : Component, IOscListener
{
    private const double vrchat_process_check_interval = 5000;
    private const double openvr_interface_init_delay = 50;
    private const int startstop_delay = 250;

    [Resolved]
    private VRCOSCConfigManager configManager { get; set; } = null!;

    [Resolved]
    private NotificationContainer notifications { get; set; } = null!;

    private Bindable<bool> autoStartStop = null!;
    private CancellationTokenSource? startTokenSource;
    private bool hasAutoStarted;

    public readonly OscClient OscClient = new();
    public readonly ModuleManager ModuleManager = new();
    public readonly Bindable<GameManagerState> State = new(GameManagerState.Stopped);
    public Player Player = null!;
    public OpenVRInterface OpenVRInterface = null!;
    public ChatBoxInterface ChatBoxInterface = null!;

    [BackgroundDependencyLoader]
    private void load(GameHost host, Storage storage)
    {
        autoStartStop = configManager.GetBindable<bool>(VRCOSCSetting.AutoStartStop);

        Player = new Player(OscClient);
        OpenVRInterface = new OpenVRInterface(storage);
        ChatBoxInterface = new ChatBoxInterface(OscClient, configManager.GetBindable<int>(VRCOSCSetting.ChatBoxTimeSpan));

        ModuleManager.Initialise(host, storage, this);
    }

    protected override void Update()
    {
        OpenVRInterface.Update();
        ChatBoxInterface.Update();
    }

    protected override void LoadComplete()
    {
        Scheduler.AddDelayed(() => Task.Run(() => OpenVRInterface.Init()), openvr_interface_init_delay, true);
        Scheduler.AddDelayed(checkForVRChat, vrchat_process_check_interval, true);

        State.BindValueChanged(e => Logger.Log($"{nameof(GameManager)} state changed to {e.NewValue}"));

        // We reset hasAutoStarted here so that turning auto start off and on again will cause it to work normally
        autoStartStop.BindValueChanged(e =>
        {
            if (!e.NewValue) hasAutoStarted = false;
        });
    }

    public void Start() => _ = startAsync();

    private async Task startAsync()
    {
        if (State.Value is not (GameManagerState.Stopping or GameManagerState.Stopped))
            throw new InvalidOperationException($"Cannot start {nameof(GameManager)} when state is {State.Value}");

        try
        {
            startTokenSource = new CancellationTokenSource();

            State.Value = GameManagerState.Starting;

            await Task.Delay(startstop_delay, startTokenSource.Token);

            if (!initialiseOscClient())
            {
                State.Value = GameManagerState.Stopped;
                return;
            }

            OscClient.RegisterListener(this);
            Player.Initialise();
            ChatBoxInterface.Initialise();
            sendControlValues();
            await ModuleManager.Start(startTokenSource.Token);

            State.Value = GameManagerState.Started;
        }
        catch (TaskCanceledException) { }
    }

    public void Stop() => _ = stopAsync();

    private async Task stopAsync()
    {
        if (State.Value is not (GameManagerState.Starting or GameManagerState.Started))
            throw new InvalidOperationException($"Cannot stop {nameof(GameManager)} when state is {State.Value}");

        startTokenSource?.Cancel();
        startTokenSource = null;

        State.Value = GameManagerState.Stopping;

        await OscClient.DisableReceive();
        await ModuleManager.Stop();
        ChatBoxInterface.Shutdown();
        Player.ResetAll();
        OscClient.DeRegisterListener(this);
        OscClient.DisableSend();

        await Task.Delay(startstop_delay);

        State.Value = GameManagerState.Stopped;
    }

    private bool initialiseOscClient()
    {
        try
        {
            var ipAddress = configManager.Get<string>(VRCOSCSetting.IPAddress);
            var sendPort = configManager.Get<int>(VRCOSCSetting.SendPort);
            var receivePort = configManager.Get<int>(VRCOSCSetting.ReceivePort);

            OscClient.Initialise(ipAddress, sendPort, receivePort);
            OscClient.Enable();
            return true;
        }
        catch (SocketException)
        {
            notifications.Notify(new InvalidOSCAttributeNotification("IP address"));
            return false;
        }
        catch (FormatException)
        {
            notifications.Notify(new InvalidOSCAttributeNotification("IP address"));
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            notifications.Notify(new InvalidOSCAttributeNotification("port"));
            return false;
        }
    }

    private void checkForVRChat()
    {
        if (!configManager.Get<bool>(VRCOSCSetting.AutoStartStop)) return;

        static bool isVRChatOpen() => Process.GetProcessesByName("vrchat").Any();

        // hasAutoStarted is checked here to ensure that modules aren't started immediately
        // after a user has manually stopped the modules
        if (isVRChatOpen() && State.Value == GameManagerState.Stopped && !hasAutoStarted)
        {
            Start();
            hasAutoStarted = true;
        }

        if (!isVRChatOpen() && State.Value == GameManagerState.Started)
        {
            Stop();
            hasAutoStarted = false;
        }
    }

    private void sendControlValues()
    {
        OscClient.SendValue($"{Constants.OSC_ADDRESS_AVATAR_PARAMETERS_PREFIX}/VRCOSC/Controls/ChatBox", ChatBoxInterface.SendEnabled);
    }

    void IOscListener.OnDataSent(OscData data) { }

    void IOscListener.OnDataReceived(OscData data)
    {
        if (data.Address == Constants.OSC_ADDRESS_AVATAR_CHANGE)
        {
            sendControlValues();
            return;
        }

        if (!data.Address.StartsWith(Constants.OSC_ADDRESS_AVATAR_PARAMETERS_PREFIX)) return;

        var parameterName = data.Address.Remove(0, Constants.OSC_ADDRESS_AVATAR_PARAMETERS_PREFIX.Length + 1);
        Player.Update(parameterName, data.Values[0]);

        switch (parameterName)
        {
            case "VRCOSC/Controls/ChatBox":
                ChatBoxInterface.SendEnabled = (bool)data.Values[0];
                break;
        }
    }
}

public enum GameManagerState
{
    Starting,
    Started,
    Stopping,
    Stopped
}
