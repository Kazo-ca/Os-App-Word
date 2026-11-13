using Kazo.Os.App.Abstractions;

namespace Kazo.Word.Tests;

/// <summary>The app's manifest class, the source of the generated manifest.json (section 4 of the integration plan).</summary>
public sealed class ManifestTests
{
    private readonly WordApp _app = new();

    [Fact]
    public void The_app_is_the_Kazo_Word_window_app()
    {
        Assert.Equal("ca.kazo.word", _app.Id);
        Assert.Equal("Kazo Word", _app.Name);
        Assert.Equal("pub.kazo", _app.PublisherId);
        Assert.Equal("productivity", _app.Category);
        Assert.Equal(AppKind.Window, _app.Kind);
        Assert.Equal(typeof(WordWindow), _app.EntryPoint);
    }

    [Fact]
    public void Several_windows_can_run_at_once()
    {
        Assert.False(_app.SingleInstancePerUser);
    }

    [Fact]
    public void The_window_opens_at_1100_by_760_and_keeps_WordCraft_minimum()
    {
        WindowOptions window = Assert.IsType<WindowOptions>(_app.Window);
        Assert.Equal(1100, window.DefaultWidth);
        Assert.Equal(760, window.DefaultHeight);
        Assert.Equal(760, window.MinWidth);
        Assert.Equal(480, window.MinHeight);
    }

    [Fact]
    public void The_app_asks_for_the_dialogs_the_home_folder_its_settings_folder_and_launching_itself()
    {
        // Work item 2: Open and Save dialogs, the user's files, other windows. Work item 3: WordCraft's
        // settings in ~/.config/ca.kazo.word (the app's private folder).
        Assert.Equal(
            [
                AppCapabilities.DialogFileOpen,
                AppCapabilities.DialogFileSave,
                AppCapabilities.FileSystemUserHomeRead,
                AppCapabilities.FileSystemUserHomeWrite,
                AppCapabilities.FileSystemPrivateRead,
                AppCapabilities.FileSystemPrivateWrite,
                AppCapabilities.AppsLaunch,
            ],
            _app.Capabilities);
        Assert.NotEmpty(_app.FileHandlers);
    }

    [Fact]
    public void The_icon_is_declared_as_the_app_icon()
    {
        Assert.Equal(["icons/app.svg"], _app.Icons);
        AssetManifest icon = Assert.Single(_app.Assets);
        Assert.Equal("icons/app.svg", icon.Path);
        Assert.Equal("app-icon", icon.Role);
    }
}
