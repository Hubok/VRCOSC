// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.Game.Modules;
using VRCOSC.Game.Modules.ChatBox;

namespace VRCOSC.Modules.Heartrate;

public abstract class HeartRateModule : ChatBoxModule
{
    private static readonly TimeSpan heartrate_timeout = TimeSpan.FromSeconds(10);

    public override string Author => @"VolcanicArts";
    public override string Prefab => @"VRCOSC-Heartrate";
    public override ModuleType Type => ModuleType.Health;

    protected HeartRateProvider? HeartRateProvider;
    private int currentHeartrate;
    private int targetHeartrate;
    private TimeSpan targetInterval;
    private DateTimeOffset lastIntervalUpdate;
    private DateTimeOffset lastHeartrateTime;
    private int connectionCount;

    private bool isReceiving => lastHeartrateTime + heartrate_timeout >= DateTimeOffset.Now;

    protected abstract HeartRateProvider CreateHeartRateProvider();

    protected override void CreateAttributes()
    {
        CreateSetting(HeartrateSetting.Smoothed, @"Smoothed", @"Whether the heartrate value should jump to the correct value, or smoothly climb over a set period", false);
        CreateSetting(HeartrateSetting.SmoothingLength, @"Smoothing Length", @"The length of time (in milliseconds) the heartrate value should take to reach the correct value", 1000, () => GetSetting<bool>(HeartrateSetting.Smoothed));
        CreateSetting(HeartrateSetting.NormalisedLowerbound, @"Normalised Lowerbound", @"The lower bound BPM the normalised parameter should use", 0);
        CreateSetting(HeartrateSetting.NormalisedUpperbound, @"Normalised Upperbound", @"The upper bound BPM the normalised parameter should use", 240);

        CreateParameter<bool>(HeartrateParameter.Enabled, ParameterMode.Write, @"VRCOSC/Heartrate/Enabled", @"Enabled", @"Whether this module is attempting to emit values");
        CreateParameter<float>(HeartrateParameter.Normalised, ParameterMode.Write, @"VRCOSC/Heartrate/Normalised", @"Normalised", @"The heartrate value normalised to the set bounds");
        CreateParameter<float>(HeartrateParameter.Units, ParameterMode.Write, @"VRCOSC/Heartrate/Units", @"Units", @"The units digit 0-9 mapped to a float");
        CreateParameter<float>(HeartrateParameter.Tens, ParameterMode.Write, @"VRCOSC/Heartrate/Tens", @"Tens", @"The tens digit 0-9 mapped to a float");
        CreateParameter<float>(HeartrateParameter.Hundreds, ParameterMode.Write, @"VRCOSC/Heartrate/Hundreds", @"Hundreds", @"The hundreds digit 0-9 mapped to a float");

        CreateVariable(HeartrateVariable.Heartrate, @"Heartrate", @"hr");

        CreateState(HeartrateState.Default, @"Default", $@"Heartrate/n{GetVariableFormat(HeartrateVariable.Heartrate)} bpm");
    }

    protected override void OnModuleStart()
    {
        attemptConnection();
        lastHeartrateTime = DateTimeOffset.Now - heartrate_timeout;
        lastIntervalUpdate = DateTimeOffset.Now;
        currentHeartrate = 0;
        targetHeartrate = 0;
        ChangeStateTo(HeartrateState.Default);
        SendParameter(HeartrateParameter.Enabled, false);
    }

    private void attemptConnection()
    {
        if (connectionCount >= 3)
        {
            Log(@"Connection cannot be established");
            return;
        }

        connectionCount++;
        HeartRateProvider = CreateHeartRateProvider();
        HeartRateProvider.OnHeartRateUpdate += handleHeartRateUpdate;
        HeartRateProvider.OnConnected += () => connectionCount = 0;

        HeartRateProvider.OnDisconnected += () =>
        {
            Task.Run(async () =>
            {
                if (IsStopping || HasStopped) return;

                SendParameter(HeartrateParameter.Enabled, false);
                await Task.Delay(2000);
                attemptConnection();
            });
        };
        HeartRateProvider.Initialise();
        HeartRateProvider.Connect();
    }

    protected override void OnModuleStop()
    {
        if (HeartRateProvider is null) return;

        if (HeartRateProvider.IsConnected) HeartRateProvider.Disconnect();
        SendParameter(HeartrateParameter.Enabled, false);
    }

    protected override void OnFrameUpdate()
    {
        if (GetSetting<bool>(HeartrateSetting.Smoothed))
        {
            if (lastIntervalUpdate + targetInterval <= DateTimeOffset.Now)
            {
                lastIntervalUpdate = DateTimeOffset.Now;
                currentHeartrate += Math.Sign(targetHeartrate - currentHeartrate);
            }
        }
        else
        {
            currentHeartrate = targetHeartrate;
        }

        sendParameters();
    }

    private void handleHeartRateUpdate(int heartrate)
    {
        targetHeartrate = heartrate;
        lastHeartrateTime = DateTimeOffset.Now;

        try
        {
            targetInterval = TimeSpan.FromTicks(TimeSpan.FromMilliseconds(GetSetting<int>(HeartrateSetting.SmoothingLength)).Ticks / Math.Abs(currentHeartrate - targetHeartrate));
        }
        catch (DivideByZeroException)
        {
            targetInterval = TimeSpan.Zero;
        }
    }

    private void sendParameters()
    {
        SendParameter(HeartrateParameter.Enabled, isReceiving);

        if (isReceiving)
        {
            var normalisedHeartRate = Map(currentHeartrate, GetSetting<int>(HeartrateSetting.NormalisedLowerbound), GetSetting<int>(HeartrateSetting.NormalisedUpperbound), 0, 1);
            var individualValues = toDigitArray(currentHeartrate, 3);

            SendParameter(HeartrateParameter.Normalised, normalisedHeartRate);
            SendParameter(HeartrateParameter.Units, individualValues[2] / 10f);
            SendParameter(HeartrateParameter.Tens, individualValues[1] / 10f);
            SendParameter(HeartrateParameter.Hundreds, individualValues[0] / 10f);
            SetVariableValue(HeartrateVariable.Heartrate, currentHeartrate.ToString());
        }
        else
        {
            SendParameter(HeartrateParameter.Normalised, 0);
            SendParameter(HeartrateParameter.Units, 0);
            SendParameter(HeartrateParameter.Tens, 0);
            SendParameter(HeartrateParameter.Hundreds, 0);
            SetVariableValue(HeartrateVariable.Heartrate, @"0");
        }
    }

    private static int[] toDigitArray(int num, int totalWidth)
    {
        return num.ToString().PadLeft(totalWidth, '0').Select(digit => int.Parse(digit.ToString())).ToArray();
    }

    protected enum HeartrateSetting
    {
        NormalisedLowerbound,
        NormalisedUpperbound,
        Smoothed,
        SmoothingLength
    }

    protected enum HeartrateParameter
    {
        Enabled,
        Normalised,
        Units,
        Tens,
        Hundreds
    }

    private enum HeartrateState
    {
        Default
    }

    private enum HeartrateVariable
    {
        Heartrate
    }
}
