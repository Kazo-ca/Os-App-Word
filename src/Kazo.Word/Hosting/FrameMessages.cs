namespace Kazo.Word.Hosting;

/// <summary>
/// Names of the messages between the window and the iframe proxy (section 5 of
/// doc/en/integration-plan.md). <c>wwwroot/js/word-host.js</c> passes them to
/// <see cref="WordFrame.OnFrameMessage"/> with a text value.
/// </summary>
public static class FrameMessages
{
    /// <summary>WordCraft is listening; the value is its version.</summary>
    public const string Ready = "ready";

    /// <summary>WordCraft did not start; the value is the error.</summary>
    public const string Failed = "failed";

    /// <summary>The shown document has changes, or not; the value is <c>true</c> or <c>false</c> (used from work item 2).</summary>
    public const string Dirty = "dirty";

    /// <summary>A reserved chord meant for the window was typed in WordCraft; the value is the key's JSON.</summary>
    public const string Key = "key";

    /// <summary>WordCraft opened its file picker; the value is <c>{ id, accept, multiple }</c>.</summary>
    public const string Pick = "pick";

    /// <summary>WordCraft wrote a setting; the value is <c>{ key, value }</c> (value null: removed).</summary>
    public const string Setting = "setting";

    /// <summary>The OS theme's color scheme changed; the value is <c>light</c> or <c>dark</c>.</summary>
    public const string Scheme = "scheme";
}
