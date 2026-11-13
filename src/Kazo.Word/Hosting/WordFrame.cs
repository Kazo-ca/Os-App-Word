using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Kazo.Word.Hosting;

/// <summary>What the window gives the iframe when it creates it.</summary>
/// <param name="Author">Name WordCraft writes on new comments and tracked changes.</param>
/// <param name="Language">The user's language, two letters (WordCraft uses it from work item 3).</param>
/// <param name="WebGl">Forces WordCraft's WebGL 2 renderer (<c>?webgl</c>) instead of WebGPU.</param>
/// <param name="Title">Accessible name of the iframe.</param>
/// <param name="ReservedKeys">Chords the iframe gives back instead of WordCraft (<see cref="Hosting.ReservedKeys"/>).</param>
/// <param name="Settings">WordCraft's storage for this Kazo user; <see langword="null"/> keeps the browser's own.</param>
public sealed record FrameOptions(
    string Author,
    string Language,
    bool WebGl,
    string Title,
    IReadOnlyList<ReservedKey>? ReservedKeys = null,
    IReadOnlyDictionary<string, string>? Settings = null);

/// <summary>One file handed to WordCraft's file picker.</summary>
/// <param name="Name">The file name, with its extension.</param>
/// <param name="Type">The media type, empty when unknown.</param>
/// <param name="Bytes">The content.</param>
public sealed record PickedFile(string Name, string Type, byte[] Bytes);

/// <summary>
/// The C# side of one iframe: it starts <c>wwwroot/js/word-host.js</c> in an element of the
/// window and raises the proxy's messages as .NET events (section 2 of doc/en/integration-plan.md).
/// </summary>
/// <remarks>
/// One <see cref="WordFrame"/> per iframe: "Retry" disposes it and creates another, so a late
/// message from the old iframe never reaches the window.
/// </remarks>
public sealed class WordFrame : IAsyncDisposable
{
    /// <summary>Path of the host module, served by the app's Razor library.</summary>
    public const string ModulePath = "./_content/Kazo.Word/js/word-host.js";

    private readonly IJSRuntime _javaScript;
    private DotNetObjectReference<WordFrame>? _self;
    private IJSObjectReference? _module;
    private IJSObjectReference? _frame;

    /// <summary>Creates a frame; <see cref="StartAsync"/> puts the iframe in the window.</summary>
    /// <param name="javaScript">The window's JavaScript runtime.</param>
    public WordFrame(IJSRuntime javaScript)
    {
        ArgumentNullException.ThrowIfNull(javaScript);
        _javaScript = javaScript;
    }

    /// <summary>Raised with WordCraft's version when it is listening.</summary>
    public event Func<string?, Task>? Ready;

    /// <summary>Raised with the error when WordCraft did not start.</summary>
    public event Func<string?, Task>? Failed;

    /// <summary>Raised when a reserved chord meant for the window (<see cref="ReservedKeyTarget.App"/>) was typed.</summary>
    public event Func<ReservedKey, Task>? KeyPressed;

    /// <summary>Raised when WordCraft opened its file picker; answer with <see cref="AnswerPickAsync"/>.</summary>
    public event Func<Pickers.PickRequest, Task>? PickRequested;

    /// <summary>Raised when WordCraft wrote a setting: its key, and its value (<see langword="null"/> when removed).</summary>
    public event Func<string, string?, Task>? SettingChanged;

    /// <summary>Raised with <c>light</c> or <c>dark</c> when the OS theme's color scheme changed.</summary>
    public event Func<string, Task>? SchemeChanged;

    /// <summary>Creates the iframe in <paramref name="container"/>; WordCraft starts loading in it.</summary>
    /// <param name="container">The element of the window that holds the iframe.</param>
    /// <param name="options">Author, language, renderer and title.</param>
    /// <param name="cancellationToken">Cancels the start (the window closes).</param>
    /// <returns>A task that completes once the iframe is in the window.</returns>
    public async Task StartAsync(ElementReference container, FrameOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        _self = DotNetObjectReference.Create(this);
        _module = await _javaScript.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        _frame = await _module.InvokeAsync<IJSObjectReference>(
            "create",
            cancellationToken,
            container,
            _self,
            new Dictionary<string, object?>
            {
                ["author"] = options.Author,
                ["language"] = options.Language,
                ["webgl"] = options.WebGl,
                ["title"] = options.Title,
                ["reservedKeys"] = (options.ReservedKeys ?? []).Select(key => key.ToMessage()).ToArray(),
                ["settings"] = options.Settings,
            });
    }

    /// <summary>Gives the keyboard to WordCraft (the window was activated, or WordCraft is ready).</summary>
    /// <returns>A task that completes once the iframe has the focus.</returns>
    public async Task FocusAsync()
    {
        if (_frame is not null)
        {
            try
            {
                await _frame.InvokeVoidAsync("focus");
            }
            catch (JSDisconnectedException)
            {
                // The window is closing.
            }
        }
    }

    /// <summary>
    /// Runs a command of WordCraft's page API (<c>window.wordcraft.request</c>; WordCraft's
    /// <c>docs/control-protocol.md</c> lists them), such as <c>ui.inspect</c>, <c>file.save</c> or
    /// <c>ui.theme</c>. Any other method name is a command id of WordCraft's engine.
    /// </summary>
    /// <param name="method">The method or command id.</param>
    /// <param name="parameters">Its parameters, serialized to JSON; <see langword="null"/> for none.</param>
    /// <param name="timeout">How long WordCraft has to answer; the proxy's default (10 s) when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops waiting (the window closes).</param>
    /// <returns>
    /// WordCraft's answer. A failure is a result, never an exception: WordCraft refused the command,
    /// did not answer in time, is not started yet, or its page reloaded.
    /// </returns>
    public async Task<FrameCommandResult> RequestAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (_frame is null)
        {
            return FrameCommandResult.Failure("WordCraft is not connected");
        }

        int timeoutMs = timeout is { } limit ? (int)Math.Ceiling(limit.TotalMilliseconds) : 0;
        try
        {
            JsonElement value = await _frame.InvokeAsync<JsonElement>(
                "request",
                cancellationToken,
                method,
                parameters ?? new Dictionary<string, object?>(),
                timeoutMs);
            return new FrameCommandResult(true, value, null);
        }
        catch (JSException exception)
        {
            return FrameCommandResult.Failure(FirstLine(exception.Message));
        }
        catch (JSDisconnectedException)
        {
            return FrameCommandResult.Failure("the window closed");
        }
    }

    /// <summary>Called by <c>word-host.js</c> for each message of the proxy.</summary>
    /// <param name="type">The message name, one of <see cref="FrameMessages"/>.</param>
    /// <param name="value">Its value, as text.</param>
    /// <returns>A task that completes once the window handled the message.</returns>
    [JSInvokable]
    public Task OnFrameMessage(string type, string? value) => type switch
    {
        FrameMessages.Ready => Ready?.Invoke(value) ?? Task.CompletedTask,
        FrameMessages.Failed => Failed?.Invoke(value) ?? Task.CompletedTask,
        FrameMessages.Key => OnKeyAsync(value),
        FrameMessages.Pick => Pickers.PickRequest.FromJson(value) is { } pick && PickRequested is { } picked
            ? picked(pick)
            : Task.CompletedTask,
        FrameMessages.Setting => OnSettingAsync(value),
        FrameMessages.Scheme => value is { } scheme ? SchemeChanged?.Invoke(scheme) ?? Task.CompletedTask : Task.CompletedTask,
        _ => Task.CompletedTask,
    };

    /// <summary>Answers WordCraft's file picker (message <c>picked</c>).</summary>
    /// <param name="id">The pick's id (<see cref="Pickers.PickRequest.Id"/>).</param>
    /// <param name="files">The chosen files; <see langword="null"/> or empty when the user cancelled.</param>
    /// <returns>A task that completes once the answer is sent.</returns>
    public async Task AnswerPickAsync(int id, IReadOnlyList<PickedFile>? files)
    {
        if (_frame is null)
        {
            return;
        }

        try
        {
            object? payload = files is { Count: > 0 }
                ? files.Select(file => new Dictionary<string, object> { ["name"] = file.Name, ["type"] = file.Type, ["bytes"] = file.Bytes }).ToArray()
                : null;
            await _frame.InvokeVoidAsync("answerPick", id, payload);
        }
        catch (JSDisconnectedException)
        {
            // The window is closing: WordCraft goes with it.
        }
    }

    /// <summary>Reads the color scheme of the OS theme around the window.</summary>
    /// <returns><c>light</c> or <c>dark</c>.</returns>
    public async Task<string> GetColorSchemeAsync()
    {
        if (_frame is null)
        {
            return "dark";
        }

        try
        {
            return await _frame.InvokeAsync<string>("colorScheme");
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            return "dark";
        }
    }

    private Task OnKeyAsync(string? json)
    {
        if (KeyPressed is null || string.IsNullOrWhiteSpace(json))
        {
            return Task.CompletedTask;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            bool Flag(string name) => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
            ReservedKey key = new(Text("key"), Text("code"), Flag("ctrl"), Flag("alt"), Flag("shift"), Flag("meta"), ReservedKeyTarget.App);
            return KeyPressed(key);
        }
        catch (JsonException)
        {
            return Task.CompletedTask;
        }
    }

    private Task OnSettingAsync(string? json)
    {
        if (SettingChanged is null || string.IsNullOrWhiteSpace(json))
        {
            return Task.CompletedTask;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("key", out JsonElement key) || key.ValueKind != JsonValueKind.String)
            {
                return Task.CompletedTask;
            }

            string? value = root.TryGetProperty("value", out JsonElement text) && text.ValueKind == JsonValueKind.String
                ? text.GetString()
                : null;
            return SettingChanged(key.GetString()!, value);
        }
        catch (JsonException)
        {
            return Task.CompletedTask;
        }
    }

    // A JSException message carries the JavaScript stack after the first line.
    private static string FirstLine(string message)
    {
        int end = message.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? message : message[..end]).Trim();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_frame is not null)
            {
                await _frame.InvokeVoidAsync("dispose");
                await _frame.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is gone: so is the iframe.
        }
        finally
        {
            _frame = null;
            _module = null;
            _self?.Dispose();
            _self = null;
        }
    }
}
