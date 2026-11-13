using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Kazo.Word.Hosting;

/// <summary>What the window gives the iframe when it creates it.</summary>
/// <param name="Author">Name WordCraft writes on new comments and tracked changes.</param>
/// <param name="Language">The user's language, two letters (WordCraft uses it from work item 3).</param>
/// <param name="WebGl">Forces WordCraft's WebGL 2 renderer (<c>?webgl</c>) instead of WebGPU.</param>
/// <param name="Title">Accessible name of the iframe.</param>
public sealed record FrameOptions(string Author, string Language, bool WebGl, string Title);

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
            new { author = options.Author, language = options.Language, webgl = options.WebGl, title = options.Title });
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

    /// <summary>Called by <c>word-host.js</c> for each message of the proxy.</summary>
    /// <param name="type">The message name, one of <see cref="FrameMessages"/>.</param>
    /// <param name="value">Its value, as text.</param>
    /// <returns>A task that completes once the window handled the message.</returns>
    [JSInvokable]
    public Task OnFrameMessage(string type, string? value) => type switch
    {
        FrameMessages.Ready => Ready?.Invoke(value) ?? Task.CompletedTask,
        FrameMessages.Failed => Failed?.Invoke(value) ?? Task.CompletedTask,
        _ => Task.CompletedTask,
    };

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
