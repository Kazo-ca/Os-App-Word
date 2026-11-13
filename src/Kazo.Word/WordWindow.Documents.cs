using Kazo.Os.AppSdk.Apps;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.IO;
using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Word.Documents;
using Kazo.Word.Hosting;
using Microsoft.JSInterop;

namespace Kazo.Word;

/// <summary>
/// Opening, saving and closing (section 6 of doc/en/integration-plan.md, work item 2): the window
/// reads and writes the files with <see cref="KazoFile"/>, asks the OS dialogs, keeps the title and
/// guards the close. Every decision comes from <see cref="DocumentSession"/>.
/// </summary>
public sealed partial class WordWindow : IWindowCloseGuard
{
    /// <summary>The app's id, to start another Kazo Word window on a file.</summary>
    private const string AppId = "ca.kazo.word";

    private readonly DocumentSession _session = new();

    // Saves run one at a time: each may show a dialog, and the order is WordCraft's.
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    // Completed with the outcome of the next save (true = written), for the close guard.
    private TaskCompletionSource<bool>? _nextSave;

    // A message over WordCraft: a failed write, a cancelled save, a copy written.
    private string? _notice;
    private bool _noticeIsError;

    /// <summary>
    /// Gets whether the window still holds only its blank document, never changed: a file opened
    /// from it may replace it in place (work item 3, File › Open in WordCraft).
    /// </summary>
    internal bool IsUntouchedBlank => _session.IsUntouchedBlank;

    /// <summary>Gets the title the OS shows for this window: <c>*name — Kazo Word</c>.</summary>
    internal string DocumentTitle => _session.Title(L["Untitled"], _startup.State == FrameState.Running);

    /// <summary>
    /// Opens a file: in this window when it holds only an untouched blank document, otherwise in a
    /// new Kazo Word window (a file opened from the OS never joins a window that has a document).
    /// </summary>
    /// <param name="path">The file's absolute path.</param>
    /// <returns><see langword="true"/> when the file opened in this window.</returns>
    internal async Task<bool> OpenInNewWindowAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        if (_frame is not null && _startup.State == FrameState.Running && _session.IsUntouchedBlank)
        {
            return await OpenFileHereAsync(path);
        }

        try
        {
            await KazoApps.LaunchAsync(AppId, [path], AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is KazoAppLaunchException or KazoUnauthorizedAccessException)
        {
            await ShowNoticeAsync(L["OpenFailed", DocumentFormats.FileNameOf(path), exception.Message], error: true);
        }

        return false;
    }

    /// <summary>Shows the OS Open dialog, then opens the chosen file with <see cref="OpenInNewWindowAsync"/>.</summary>
    /// <returns>A task that completes once the file is open or the dialog cancelled.</returns>
    internal async Task ShowOpenDialogAsync()
    {
        OpenFileDialogResult result = await OpenFileAsync(new OpenFileDialogRequest
        {
            Title = L["OpenTitle"],
            InitialDirectory = DialogFolder(null),
            Filters =
            [
                new FileDialogFilter(L["FilterDocuments"], [.. DocumentFormats.OpenExtensions], []),
                new FileDialogFilter(L["FilterAll"], [], []),
            ],
            RequestedAccess = SelectedFileAccess.ReadWrite,
        });
        if (result.Status == FileDialogStatus.Selected && result.Resources.Count > 0)
        {
            await OpenInNewWindowAsync(result.Resources[0].Path.Value);
        }
    }

    /// <summary>
    /// Asks before closing a window with work that is not in a file (section 6, "Close"): WordCraft
    /// lists every document of its page (<c>ui.inspect</c>); "Yes" saves each one and closes only
    /// once every write succeeded.
    /// </summary>
    /// <param name="cancellationToken">Cancels the close request.</param>
    /// <returns><see langword="true"/> to close.</returns>
    public async ValueTask<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        if (_frame is not { } frame || _startup.State != FrameState.Running)
        {
            return true;
        }

        FrameCommandResult inspect = await frame.RequestAsync("ui.inspect", cancellationToken: cancellationToken);
        if (!inspect.Ok)
        {
            // WordCraft does not answer, so it cannot save either: closing without saving is the
            // only choice left, and it is the user's.
            if (!_session.IsDirty && _session.UnsavedNames.Count == 0)
            {
                return true;
            }

            MessageBoxDialogResult discard = await MessageBox(
                L["CloseTitle"], L["CloseUnanswered", _session.CurrentName ?? L["Untitled"]], MessageBoxType.YesNo, cancellationToken);
            return discard.Result == MessageBoxResult.Yes;
        }

        IReadOnlyList<OpenDocument> unsaved = _session.WithUnsavedWork(OpenDocument.ListFrom(inspect.Value));
        if (unsaved.Count == 0)
        {
            return true;
        }

        string names = string.Join(", ", unsaved.Select(document => document.Name ?? document.Title));
        MessageBoxDialogResult answer = await MessageBox(
            L["CloseTitle"], L["CloseUnsaved", names], MessageBoxType.YesNoCancel, cancellationToken);
        return answer.Result switch
        {
            MessageBoxResult.Yes => await SaveAllAsync(frame, unsaved, cancellationToken),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    // After `ready`: the window's file (after Retry), else the file asked for, else a blank document.
    private async Task PrepareDocumentAsync()
    {
        // Runs after `ready`, a [JSInvokable] callback: the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        string? path = (_session.CurrentName is { } current ? _session.PathOf(current) : null) ?? _request.Path;
        if (path is null || !await OpenFileHereAsync(path))
        {
            await StartBlankAsync();
        }

        await UpdateTitleAsync();
    }

    // WordCraft starts on its start screen in host mode: a blank document, start screen closed.
    private async Task StartBlankAsync()
    {
        if (_frame is not { } frame)
        {
            return;
        }

        await frame.RequestAsync("file.new", new { }, cancellationToken: AppContext.CancellationToken);
        await frame.RequestAsync("ui.backstage", new { value = false }, cancellationToken: AppContext.CancellationToken);
    }

    // Reads a file and sends it to WordCraft; a file that does not read leaves a message.
    private async Task<bool> OpenFileHereAsync(string path)
    {
        if (_frame is not { } frame)
        {
            return false;
        }

        string name = DocumentFormats.FileNameOf(path);
        byte[] bytes;
        try
        {
            bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await ShowNoticeAsync(L["OpenFailed", name, exception.Message], error: true);
            return false;
        }

        if (!await frame.OpenAsync(name, bytes))
        {
            await ShowNoticeAsync(L["OpenFailed", name, L["NotConnected"]], error: true);
            return false;
        }

        _session.Opened(name, path);
        await frame.RequestAsync("ui.backstage", new { value = false }, cancellationToken: AppContext.CancellationToken);
        await UpdateTitleAsync();
        return true;
    }

    private async Task OnDirtyChangedAsync(bool dirty)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        bool before = _session.IsDirty;
        _session.SetFrameDirty(dirty);
        if (before != _session.IsDirty)
        {
            await UpdateTitleAsync();
        }
    }

    // One file WordCraft saves: decided by the session, written here (section 6, "Save").
    private async Task OnSavingAsync(FrameSave save)
    {
        // Called from JavaScript ([JSInvokable]): the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        await _saveGate.WaitAsync(AppContext.CancellationToken);
        bool written = false;
        try
        {
            written = await SaveAsync(save);
        }
        finally
        {
            _saveGate.Release();
            Interlocked.Exchange(ref _nextSave, null)?.TrySetResult(written);
            await UpdateTitleAsync();
        }
    }

    private async Task<bool> SaveAsync(FrameSave save)
    {
        if (string.IsNullOrEmpty(save.Name))
        {
            return false;
        }

        SaveDecision decision = _session.Decide(save.Name, save.SaveAs);
        try
        {
            return await CarryOutAsync(decision, save.Bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever went wrong (a dialog, the file system), the document must not pass for saved.
            _session.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", save.Name, exception.Message], error: true);
            return false;
        }
    }

    // Asks for the path when the decision says so, writes the bytes, and records the outcome.
    private async Task<bool> CarryOutAsync(SaveDecision decision, byte[] bytes)
    {
        string? path = decision.AsksForPath ? await AskSavePathAsync(decision) : decision.Path;
        if (path is null)
        {
            _session.SaveFailed(decision);
            if (decision.SavesTheDocument)
            {
                await ShowNoticeAsync(L["SaveCancelled", decision.Name], error: false);
            }

            return false;
        }

        try
        {
            await KazoFile.WriteAllBytesAsync(path, bytes, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _session.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", DocumentFormats.FileNameOf(path), exception.Message], error: true);
            return false;
        }

        _session.Saved(decision, path);
        if (decision.Action == SaveAction.AskForCopy)
        {
            await ShowNoticeAsync(L["CopySaved", DocumentFormats.FileNameOf(path)], error: false);
            return true;
        }

        await ClearNoticeAsync();
        if (decision.Action == SaveAction.AskAndBind)
        {
            await RenameDocumentAsync(decision.Name, DocumentFormats.FileNameOf(path));
        }

        return true;
    }

    // The OS save dialog, in the document's folder, for the format WordCraft chose.
    private async Task<string?> AskSavePathAsync(SaveDecision decision)
    {
        string extension = DocumentFormats.ExtensionOf(decision.Name);
        DocumentFormat? format = DocumentFormats.Find(decision.Name);
        SaveFileDialogResult result = await SaveFileAsync(new SaveFileDialogRequest
        {
            Title = decision.Action == SaveAction.AskForCopy ? L["SaveCopyTitle"] : L["SaveTitle"],
            InitialDirectory = DialogFolder(decision.Folder),
            SuggestedFileName = decision.Name,
            DefaultExtension = extension.Length > 0 ? extension : null,
            Filters = extension.Length > 0
                ? [new FileDialogFilter(L["FilterFormat", extension], [extension], format is null ? [] : [format.MediaType])]
                : [],
            ConfirmOverwrite = true,
        });
        return result.Status == FileDialogStatus.Selected && result.Resource is { } chosen
            ? DocumentFormats.WithExtensionOf(chosen.Path.Value, decision.Name)
            : null;
    }

    // After a save to a file of another name, WordCraft's document takes that name: its title, and
    // the name its next saves carry, match the file. Best effort: the binding works either way.
    private async Task RenameDocumentAsync(string oldName, string newName)
    {
        if (_frame is not { } frame || string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            return;
        }

        FrameCommandResult inspect = await frame.RequestAsync("ui.inspect", cancellationToken: AppContext.CancellationToken);
        OpenDocument? document = inspect.Ok
            ? OpenDocument.ListFrom(inspect.Value).FirstOrDefault(candidate => candidate.Name == oldName)
            : null;
        if (document is null)
        {
            return;
        }

        FrameCommandResult renamed = await frame.RequestAsync(
            "engine.execute",
            new { command = "file.rename", @params = new { name = newName }, doc = document.Id },
            cancellationToken: AppContext.CancellationToken);
        if (renamed.Ok)
        {
            _session.Renamed(oldName, newName);
        }
    }

    // "Yes" in the close dialog: each document is saved in turn; the window closes only when all
    // of them reached a file.
    private async Task<bool> SaveAllAsync(WordFrame frame, IReadOnlyList<OpenDocument> documents, CancellationToken cancellationToken)
    {
        foreach (OpenDocument document in documents)
        {
            TaskCompletionSource<bool> outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _nextSave = outcome;
            FrameCommandResult started = await frame.RequestAsync(
                "engine.execute",
                new { command = "file.save", @params = new { }, doc = document.Id },
                cancellationToken: cancellationToken);
            if (!started.Ok)
            {
                _nextSave = null;
                await ShowNoticeAsync(L["SaveFailed", document.Name ?? document.Title, started.Error ?? string.Empty], error: true);
                return false;
            }

            if (!await outcome.Task.WaitAsync(cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private VirtualPath DialogFolder(string? folder) => VirtualPath.Parse(folder ?? AppContext.HomeDirectory);

    private async Task UpdateTitleAsync()
    {
        try
        {
            await SetWindowTitleAsync(DocumentTitle);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSDisconnectedException)
        {
            // The window is closing.
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ShowNoticeAsync(string text, bool error)
    {
        _notice = text;
        _noticeIsError = error;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearNoticeAsync()
    {
        if (_notice is not null)
        {
            _notice = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void DismissNotice() => _notice = null;
}
