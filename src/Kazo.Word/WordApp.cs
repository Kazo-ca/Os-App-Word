using Kazo.Os.App.Abstractions;
using Kazo.Word.Documents;

namespace Kazo.Word;

/// <summary>
/// The app's manifest (section 4 of doc/en/integration-plan.md). The build generates
/// <c>manifest.json</c> from this class (Kazo.Os.App.Build); it is never instantiated at run time.
/// </summary>
/// <remarks>
/// Work item 1 declared the base: the window and its size, several windows at once. Work item 2
/// adds the document formats (<see cref="DocumentFormats"/>), the OS dialogs and the capabilities
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
    /// <remarks>
    /// Work item 2: the OS Open and Save dialogs, the user's files (a file chosen in a dialog
    /// outside the home folder is reached through the dialog's grant), and <c>apps.launch</c> to
    /// open a file in another Kazo Word window. Work item 3: the files picked in WordCraft are read
    /// (pictures, fonts, recipients), and WordCraft's settings live in <c>~/.config/ca.kazo.word/</c>.
    /// </remarks>
    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.DialogFileOpen,
        AppCapabilities.DialogFileSave,
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
        AppCapabilities.FileSystemPrivateRead,
        AppCapabilities.FileSystemPrivateWrite,
        AppCapabilities.AppsLaunch,
    ];

    /// <inheritdoc />
    /// <remarks>
    /// The word-processing documents with <c>open</c> and <c>edit</c> (Kazo Word becomes their
    /// app); Markdown, text, HTML and TeX with <c>open</c> only, for "Open with" (doc/en/plan.md).
    /// </remarks>
    public override IReadOnlyList<FileHandler> FileHandlers => DocumentFormats.FileHandlers;

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
