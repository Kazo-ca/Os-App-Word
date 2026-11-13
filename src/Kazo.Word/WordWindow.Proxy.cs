using System.Text.Json;
using Kazo.Os.AppSdk.Apps;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.IO;
using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Word.Hosting;
using Kazo.Word.Pickers;
using Kazo.Word.Settings;

namespace Kazo.Word;

/// <summary>
/// Work item 3 of doc/en/integration-plan.md, the complete proxy: what the window does with
/// WordCraft's file picker, the chords it reserves, the user's WordCraft settings, and the OS
/// theme and language.
/// </summary>
/// <remarks>
/// The proxy's messages arrive through a <c>[JSInvokable]</c> callback, outside the scope the OS opens
/// for app code: each handler that reaches the SDK (files, launching) enters the app's context itself.
/// </remarks>
public sealed partial class WordWindow
{
    /// <summary>How long WordCraft's settings writes are gathered before the file is written.</summary>
    public static readonly TimeSpan SettingsWriteDelay = TimeSpan.FromMilliseconds(500);

    private WebStorageFile? _settingsFile;
    private CancellationTokenSource? _settingsWrite;

    // Reads the user's WordCraft settings before the iframe starts: WordCraft reads them as it starts.
    private async Task<IReadOnlyDictionary<string, string>?> LoadSettingsAsync()
    {
        _settingsFile ??= new WebStorageFile(AppContext.HomeDirectory);
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            return new Dictionary<string, string>(await _settingsFile.LoadAsync(AppContext.CancellationToken), StringComparer.Ordinal);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // The proxy's events of one frame (subscribed in StartFrameAsync, removed in StopFrameAsync).
    private void ConnectProxy(WordFrame frame, bool connect)
    {
        if (connect)
        {
            frame.KeyPressed += OnKeyPressedAsync;
            frame.PickRequested += OnPickRequestedAsync;
            frame.SettingChanged += OnSettingChangedAsync;
            frame.SchemeChanged += OnSchemeChangedAsync;
        }
        else
        {
            frame.KeyPressed -= OnKeyPressedAsync;
            frame.PickRequested -= OnPickRequestedAsync;
            frame.SettingChanged -= OnSettingChangedAsync;
            frame.SchemeChanged -= OnSchemeChangedAsync;
        }
    }

    // WordCraft is listening: it takes the OS theme and the user's language (section 6, "Theme and
    // language"). A refused command leaves WordCraft as it was: the document matters more.
    private async Task ApplyThemeAndLanguageAsync(WordFrame frame)
    {
        await OnSchemeChangedAsync(await frame.GetColorSchemeAsync());
        FrameCommandResult languages = await frame.RequestAsync("ui.language", cancellationToken: AppContext.CancellationToken);
        if (!languages.Ok || languages.Value.ValueKind != JsonValueKind.Object
            || !languages.Value.TryGetProperty("available", out JsonElement available)
            || available.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        List<string> codes = [.. available.EnumerateArray()
            .Select(entry => entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("code", out JsonElement code) ? code.GetString() : null)
            .OfType<string>()];
        string language = WordCraftLanguage.Pick(AppContext.Culture, codes);
        await frame.RequestAsync("ui.language", new Dictionary<string, object?> { ["value"] = language }, cancellationToken: AppContext.CancellationToken);
    }

    private async Task OnSchemeChangedAsync(string scheme)
    {
        if (_frame is { } frame)
        {
            string value = scheme == "light" ? "light" : "dark";
            await frame.RequestAsync("ui.theme", new Dictionary<string, object?> { ["value"] = value }, cancellationToken: AppContext.CancellationToken);
        }
    }

    // Ctrl+N in WordCraft: a new Kazo Word window, as File › New does in a desktop word processor
    // (section 6, "File › New inside WordCraft").
    private async Task OnKeyPressedAsync(ReservedKey key)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        if (key with { Target = ReservedKeyTarget.App } == ReservedKeys.NewWindow)
        {
            await InvokeAsync(() => LaunchNewWindowAsync(null));
        }
    }

    // A new Kazo Word window, empty or on a file.
    private async Task LaunchNewWindowAsync(string? path)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await KazoApps.LaunchAsync(Manifest.Id, path is null ? [] : [path], AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is KazoAppLaunchException or UnauthorizedAccessException)
        {
            await ShowProblemAsync(L["NewWindowFailed", exception.Message]);
        }
    }

    // WordCraft opened its file picker (section 6, "File › Open inside WordCraft"): the OS dialog
    // instead. A document opens in a new Kazo Word window and WordCraft hears "cancelled"; any other
    // file (a picture, fonts, a recipients list) is read and handed to WordCraft.
    private Task OnPickRequestedAsync(PickRequest request) => InvokeAsync(async () =>
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        WordFrame? frame = _frame;
        if (frame is null)
        {
            return;
        }

        IReadOnlyList<PickedFile>? files = null;
        try
        {
            OpenFileDialogResult choice = await OpenFileAsync(
                new OpenFileDialogRequest
                {
                    Title = L[TitleOf(request.Purpose)],
                    InitialDirectory = VirtualPath.Parse(AppContext.HomeDirectory),
                    AllowMultiple = request.Multiple && request.Purpose != PickPurpose.Document,
                    Filters = FiltersOf(request),
                },
                AppContext.CancellationToken);
            if (choice.Status == FileDialogStatus.Selected && choice.Resources.Count > 0)
            {
                if (request.Purpose == PickPurpose.Document)
                {
                    await LaunchNewWindowAsync(choice.Resources[0].Path.Value);
                }
                else
                {
                    files = await ReadPickedAsync(choice.Resources);
                }
            }
        }
        finally
        {
            // WordCraft waits on its picker until it hears back: it always does, even on a failure.
            await frame.AnswerPickAsync(request.Id, files);
        }
    });

    private async Task<IReadOnlyList<PickedFile>?> ReadPickedAsync(IReadOnlyList<SelectedFileResource> resources)
    {
        List<PickedFile> files = [];
        foreach (SelectedFileResource resource in resources)
        {
            string path = resource.Path.Value;
            string name = KazoPath.GetFileName(path) ?? path;
            try
            {
                byte[] bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
                files.Add(new PickedFile(name, string.Empty, bytes));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await ShowProblemAsync(L["ReadFailed", name, exception.Message]);
                return null;
            }
        }

        return files;
    }

    private static string TitleOf(PickPurpose purpose) => purpose switch
    {
        PickPurpose.Document => "PickDocumentTitle",
        PickPurpose.Picture => "PickPictureTitle",
        PickPurpose.Fonts => "PickFontsTitle",
        PickPurpose.Recipients => "PickRecipientsTitle",
        _ => "PickOtherTitle",
    };

    // The extensions WordCraft accepts, then every file (WordCraft checks what it gets).
    private List<FileDialogFilter> FiltersOf(PickRequest request)
    {
        string name = request.Purpose switch
        {
            PickPurpose.Document => "DocumentsFilter",
            PickPurpose.Picture => "PicturesFilter",
            PickPurpose.Fonts => "FontsFilter",
            PickPurpose.Recipients => "RecipientsFilter",
            _ => "AllFiles",
        };
        List<FileDialogFilter> filters = [];
        if (request.Extensions.Count > 0)
        {
            filters.Add(new FileDialogFilter(L[name], request.Extensions, []));
        }

        filters.Add(new FileDialogFilter(L["AllFiles"], [], []));
        return filters;
    }

    // WordCraft wrote a setting: the user's file is written once the writes stop for a moment
    // (WordCraft rewrites wordcraft.ui on each change of its interface).
    private Task OnSettingChangedAsync(string key, string? value)
    {
        if (_settingsFile is null || !_settingsFile.Set(key, value))
        {
            return Task.CompletedTask;
        }

        _settingsWrite?.Cancel();
        _settingsWrite?.Dispose();
        CancellationTokenSource write = CancellationTokenSource.CreateLinkedTokenSource(AppContext.CancellationToken);
        _settingsWrite = write;
        _ = WriteSettingsLaterAsync(write.Token);
        return Task.CompletedTask;
    }

    private async Task WriteSettingsLaterAsync(CancellationToken cancellationToken)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await Task.Delay(SettingsWriteDelay, cancellationToken);
            await _settingsFile!.SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer write replaces this one, or the window closed.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The settings stay in this window's WordCraft; the next write tries again.
        }
    }

    // Writes what is still waiting when the window closes.
    private async Task FlushSettingsAsync()
    {
        if (_settingsWrite is null || _settingsFile is null)
        {
            return;
        }

        _settingsWrite.Cancel();
        _settingsWrite.Dispose();
        _settingsWrite = null;
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await _settingsFile.SaveAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Closing: nothing more to do.
        }
    }

    private async Task ShowProblemAsync(string message)
    {
        try
        {
            await MessageBox(L["ProblemTitle"], message, MessageBoxType.Ok, MessageBoxIcon.Error, AppContext.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The window closed.
        }
    }
}
