// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using Valve.VR;
using VRCOSC.Game.Config;
using VRCOSC.Game.Graphics;
using VRCOSC.Game.Graphics.Notifications;
using VRCOSC.Game.Graphics.Settings;
using VRCOSC.Game.Graphics.TabBar;
using VRCOSC.Game.Graphics.Updater;
using VRCOSC.Game.Modules;
using VRCOSC.Game.Modules.Util;

// ReSharper disable InconsistentNaming

namespace VRCOSC.Game;

[Cached]
public abstract partial class VRCOSCGame : VRCOSCGameBase
{
    private const string latest_release_url = "https://github.com/volcanicarts/vrcosc/releases/latest";
    private const string discord_invite_url = "https://discord.gg/vj4brHyvT5";

    [Cached]
    private ModuleManager moduleManager = new();

    [Resolved]
    private GameHost host { get; set; } = null!;

    public VRCOSCUpdateManager UpdateManager = null!;

    private NotificationContainer notificationContainer = null!;
    private OpenVrInterface openVrInterface = null!;

    public Bindable<string> SearchTermFilter = new(string.Empty);
    public Bindable<ModuleType?> TypeFilter = new();

    [Cached]
    private Bindable<Tab> SelectedTab = new();

    [Cached]
    private BindableBool ModulesRunning = new();

    [Cached(name: "EditingModule")]
    private Bindable<Module?> EditingModule = new();

    [Cached(name: "InfoModule")]
    private IBindable<Module?> InfoModule = new Bindable<Module?>();

    [BackgroundDependencyLoader]
    private void load()
    {
        notificationContainer = new NotificationContainer();
        DependencyContainer.CacheAs(notificationContainer);

        openVrInterface = new OpenVrInterface();
        DependencyContainer.CacheAs(openVrInterface);

        Children = new Drawable[]
        {
            notificationContainer,
            new MainContent(),
            UpdateManager = CreateUpdateManager()
        };

        ChangeChildDepth(notificationContainer, float.MinValue);
    }

    protected override void Update()
    {
        openVrInterface.Poll();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        checkUpdates();
        checkVersion();

        openVrInterface.Init();

        notificationContainer.Notify(new TimedNotification
        {
            Title = "Join The Community!",
            Description = "Click to join the Discord server",
            Icon = FontAwesome.Brands.Discord,
            Colour = Colour4.FromHex(@"7289DA"),
            ClickCallback = () => host.OpenUrlExternally(discord_invite_url),
            Delay = 5000d
        });

        ModulesRunning.BindValueChanged(e =>
        {
            if (e.NewValue) SelectedTab.Value = Tab.Modules;
        }, true);

        // Add the module manager after everything is complete for force it to be the last thing to load
        Add(moduleManager);
    }

    private void checkUpdates()
    {
        var updateMode = ConfigManager.Get<UpdateMode>(VRCOSCSetting.UpdateMode);
        if (updateMode != UpdateMode.Off) UpdateManager.CheckForUpdate(ConfigManager.Get<string>(VRCOSCSetting.UpdateRepo));
    }

    private void checkVersion()
    {
        var lastVersion = ConfigManager.Get<string>(VRCOSCSetting.Version);

        if (Version != lastVersion && !string.IsNullOrEmpty(lastVersion))
        {
            notificationContainer.Notify(new BasicNotification
            {
                Title = "VRCOSC Updated",
                Description = "Click to see the changes",
                Icon = FontAwesome.Solid.Download,
                Colour = VRCOSCColour.GreenDark,
                ClickCallback = () => host.OpenUrlExternally(latest_release_url),
            });
        }

        ConfigManager.SetValue(VRCOSCSetting.Version, Version);
    }

    protected override bool OnExiting()
    {
        moduleManager.State.BindValueChanged(e =>
        {
            if (e.NewValue == ManagerState.Stopped) Exit();
        }, true);

        ModulesRunning.Value = false;

        OpenVR.Shutdown();

        return true;
    }

    protected abstract VRCOSCUpdateManager CreateUpdateManager();
}
