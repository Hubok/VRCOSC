﻿using Newtonsoft.Json;
using VRCOSC.Game.Modules.Modules.Heartrate.Pulsoid.Models;
using VRCOSC.Game.Util;

namespace VRCOSC.Game.Modules.Modules.Heartrate.Pulsoid;

public class PulsoidProvider : HeartRateProvider
{
    private readonly string accessToken;
    private readonly TerminalLogger terminal = new(nameof(PulsoidModule));

    protected override string WebSocketUrl => $"wss://dev.pulsoid.net/api/v1/data/real_time?access_token={accessToken}";
    protected override int WebSocketHeartBeat => 10000;

    public PulsoidProvider(string accessToken)
    {
        this.accessToken = accessToken;
    }

    protected override void HandleWsConnected()
    {
        terminal.Log("Successfully connected to the Pulsoid websocket");
    }

    protected override void HandleWsDisconnected()
    {
        terminal.Log("Disconnected from the Pulsoid websocket");
    }

    protected override void HandleWsMessage(string message)
    {
        var data = JsonConvert.DeserializeObject<PulsoidResponse>(message)!;
        OnHeartRateUpdate?.Invoke(data.Data.HeartRate);
    }

    protected override void HandleWsHeartBeat()
    {
        terminal.Log("Sending Pulsoid websocket heartbeat");
    }
}
