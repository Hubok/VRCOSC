﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;

namespace VRCOSC.Game.Modules.Modules.Clock;

public sealed class ClockModule : Module
{
    public override string Title => "Clock";
    public override string Description => "Sends your local time as hours, minutes, and seconds";
    public override string Author => "VolcanicArts";
    public override string Prefab => "VRCOSC-Watch";
    public override ModuleType ModuleType => ModuleType.General;
    protected override int DeltaUpdate => GetSetting<bool>(ClockSetting.SmoothSecond) ? 50 : 1000;

    protected override void CreateAttributes()
    {
        CreateSetting(ClockSetting.SmoothSecond, "Smooth Second", "If the seconds hand should be smooth", false);
        CreateSetting(ClockSetting.Timezone, "Timezone", "The timezone the clock should follow", ClockTimeZone.Local);

        CreateOutgoingParameter(ClockOutgoingParameter.Hours, "Hour", "The current hour normalised", "/avatar/parameters/ClockHour");
        CreateOutgoingParameter(ClockOutgoingParameter.Minutes, "Minute", "The current minute normalised", "/avatar/parameters/ClockMinute");
        CreateOutgoingParameter(ClockOutgoingParameter.Seconds, "Second", "The current second normalised", "/avatar/parameters/ClockSecond");
    }

    protected override void OnUpdate()
    {
        var time = timezoneToTime(GetSetting<ClockTimeZone>(ClockSetting.Timezone));

        var hours = (float)time.Hour;
        var minutes = (float)time.Minute;
        var seconds = (float)time.Second;

        // smooth hands
        if (GetSetting<bool>(ClockSetting.SmoothSecond)) seconds += time.Millisecond / 1000f;
        minutes += seconds / 60f;
        hours += minutes / 60f;

        var hourNormalised = (hours % 12f) / 12f;
        var minuteNormalised = minutes / 60f;
        var secondNormalised = seconds / 60f;

        SendParameter(ClockOutgoingParameter.Hours, hourNormalised);
        SendParameter(ClockOutgoingParameter.Minutes, minuteNormalised);
        SendParameter(ClockOutgoingParameter.Seconds, secondNormalised);
    }

    private static DateTime timezoneToTime(ClockTimeZone timeZone)
    {
        return timeZone switch
        {
            ClockTimeZone.Local => DateTime.Now,
            ClockTimeZone.UTC => DateTime.UtcNow,
            ClockTimeZone.GMT => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")),
            ClockTimeZone.EST => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")),
            ClockTimeZone.CST => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time")),
            ClockTimeZone.MNT => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time")),
            ClockTimeZone.PST => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")),
            _ => throw new ArgumentOutOfRangeException(nameof(timeZone), timeZone, null)
        };
    }

    private enum ClockOutgoingParameter
    {
        Hours,
        Minutes,
        Seconds,
    }

    private enum ClockSetting
    {
        Timezone,
        SmoothSecond
    }

    private enum ClockTimeZone
    {
        Local,
        UTC,
        GMT,
        EST,
        CST,
        MNT,
        PST
    }
}
