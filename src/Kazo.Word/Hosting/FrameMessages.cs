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

    /// <summary>The shown document has changes, or not; the value is <c>true</c> or <c>false</c>.</summary>
    public const string Dirty = "dirty";
}
