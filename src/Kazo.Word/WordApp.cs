using Kazo.Os.App.Abstractions;

namespace Kazo.Word;

/// <summary>
/// The app's manifest (section 4 of doc/en/integration-plan.md). The build generates
/// <c>manifest.json</c> from this class (Kazo.Os.App.Build); it is never instantiated at run time.
/// </summary>
/// <remarks>
/// Work item 1 declares the base: the window and its size, several windows at once, no file
/// associations yet. Work item 2 adds the document formats, the OS dialogs and the capabilities
/// they need.
/// </remarks>
[AppManifest]
public sealed class WordApp : AppDefinition
{
    /// <summary>Smallest usable width of WordCraft, in pixels: its ribbon and a page margin.</summary>
    public const int ContentMinWidth = 758;

    /// <summary>Smallest usable height of WordCraft, in pixels.</summary>
    public const int ContentMinHeight = 440;

    /// <inheritdoc />
    public override string Id => "ca.kazo.word";

    /// <inheritdoc />
    public override string Name => "Kazo Word";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(WordWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.kazo";

    /// <inheritdoc />
    public override string Description => "A word processor for .docx and .odt documents, built on WordCraft";

    /// <inheritdoc />
    public override string Category => "productivity";

    /// <inheritdoc />
    /// <remarks>The tag that publishes to the Kazo OS Store must be v + this version.</remarks>
    public override string Version => "0.1.0";

    /// <inheritdoc />
    /// <remarks>
    /// Several windows at once: each one runs its own WordCraft, in its own iframe, with its own
    /// message port (section 2 of the integration plan).
    /// </remarks>
    public override bool SingleInstancePerUser => false;

    /// <inheritdoc />
    /// <remarks>Work item 1 reads and writes no file: WordCraft runs alone in its iframe.</remarks>
    public override IReadOnlyList<string> Capabilities => [];

    /// <inheritdoc />
    public override IReadOnlyList<string> Icons => ["icons/app.svg"];

    /// <inheritdoc />
    public override IReadOnlyList<AssetManifest> Assets =>
        [new("icons/app.svg", AssetKind.Image, new string('0', 64), "app-icon")];

    /// <inheritdoc />
    /// <remarks>
    /// Sizes are the outer size of the OS window (title bar included): 1100 × 760 by default, room
    /// for a page and WordCraft's ribbon; 760 × 480 at least, WordCraft's own minimum plus the frame.
    /// </remarks>
    public override WindowOptions? Window => new()
    {
        DefaultWidth = 1100,
        DefaultHeight = 760,
        MinWidth = ContentMinWidth + WindowChromeWidth,
        MinHeight = ContentMinHeight + WindowChromeHeight,
    };

    // Borders and title bar of an OS window (the tallest title bar of the Kazo OS themes, 2.35rem).
    private const int WindowChromeWidth = 2;
    private const int WindowChromeHeight = 40;
}
