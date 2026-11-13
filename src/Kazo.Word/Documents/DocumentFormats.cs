using Kazo.Os.App.Abstractions;

namespace Kazo.Word.Documents;

/// <summary>
/// One file format Kazo Word knows (section 6 of doc/en/integration-plan.md, "Save", and the
/// "Associated formats" of doc/en/plan.md).
/// </summary>
/// <param name="Extension">Extension with its dot, lowercase.</param>
/// <param name="MediaType">Media type written in the manifest and given to the OS dialogs.</param>
/// <param name="IsDefault">
/// Kazo Word handles <c>open</c> and <c>edit</c> for this format (a word-processing document);
/// otherwise only <c>open</c>, for "Open with" (the OS text editor or browser stays the default).
/// </param>
/// <param name="IsCopy">
/// WordCraft writes this format as a copy (PDF, PNG, HTML, Markdown, text): the document stays bound
/// to its own file.
/// </param>
/// <param name="Opens">WordCraft opens this format.</param>
public sealed record DocumentFormat(string Extension, string MediaType, bool IsDefault, bool IsCopy, bool Opens = true);

/// <summary>The formats Kazo Word opens and writes, and what a file name says about them.</summary>
/// <remarks>
/// WordCraft decides the format of every file it saves: the name it hands over carries the
/// extension. Kazo Word only needs the extension to tell a document (bound to its file) from a copy
/// (an export that leaves the binding alone), and to build the OS dialogs' filters.
/// </remarks>
public static class DocumentFormats
{
    /// <summary>Every known format, the word-processing documents first.</summary>
    public static IReadOnlyList<DocumentFormat> All { get; } =
    [
        new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", IsDefault: true, IsCopy: false),
        new(".docm", "application/vnd.ms-word.document.macroenabled.12", IsDefault: true, IsCopy: false),
        new(".dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template", IsDefault: true, IsCopy: false),
        new(".dotm", "application/vnd.ms-word.template.macroenabled.12", IsDefault: true, IsCopy: false),
        new(".doc", "application/msword", IsDefault: true, IsCopy: false),
        new(".dot", "application/msword", IsDefault: true, IsCopy: false),
        new(".odt", "application/vnd.oasis.opendocument.text", IsDefault: true, IsCopy: false),
        new(".ott", "application/vnd.oasis.opendocument.text-template", IsDefault: true, IsCopy: false),
        new(".fodt", "application/vnd.oasis.opendocument.text-flat-xml", IsDefault: true, IsCopy: false),
        new(".rtf", "application/rtf", IsDefault: true, IsCopy: false),
        new(".md", "text/markdown", IsDefault: false, IsCopy: true),
        new(".txt", "text/plain", IsDefault: false, IsCopy: true),
        new(".html", "text/html", IsDefault: false, IsCopy: true),
        new(".htm", "text/html", IsDefault: false, IsCopy: true),
        new(".tex", "application/x-tex", IsDefault: false, IsCopy: false),
        new(".pdf", "application/pdf", IsDefault: false, IsCopy: true, Opens: false),
        new(".png", "image/png", IsDefault: false, IsCopy: true, Opens: false),
    ];

    /// <summary>Gets the extensions Kazo Word opens, for the Open dialog's filter.</summary>
    public static IReadOnlyList<string> OpenExtensions { get; } =
        [.. All.Where(format => format.Opens).Select(format => format.Extension)];

    /// <summary>
    /// Gets the manifest's file handlers: one per media type, <c>open</c> and <c>edit</c> for the
    /// word-processing documents, <c>open</c> alone for the formats offered in "Open with" only.
    /// </summary>
    public static IReadOnlyList<FileHandler> FileHandlers { get; } =
    [
        .. All
            .Where(format => format.Opens)
            .GroupBy(format => format.MediaType, StringComparer.Ordinal)
            .Select(group => new FileHandler(
                group.Key,
                [.. group.Select(format => format.Extension)],
                group.First().IsDefault ? ["open", "edit"] : ["open"])),
    ];

    /// <summary>Gets the extension of a file name or path, lowercase with its dot; empty when it has none.</summary>
    /// <param name="fileName">A file name or a path.</param>
    /// <returns>The extension, such as <c>.docx</c>.</returns>
    public static string ExtensionOf(string fileName)
    {
        string name = FileNameOf(fileName);
        int dot = name.LastIndexOf('.');
        return dot > 0 ? name[dot..].ToLowerInvariant() : string.Empty;
    }

    /// <summary>Gets the last segment of a path (the whole text when it has no <c>/</c>).</summary>
    /// <param name="path">A path or a file name.</param>
    /// <returns>The file name.</returns>
    public static string FileNameOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path[(path.LastIndexOf('/') + 1)..];
    }

    /// <summary>Gets the folder of an absolute path, or <see langword="null"/> for a bare name.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>The folder, <c>/</c> for a file at the root.</returns>
    public static string? FolderOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int slash = path.LastIndexOf('/');
        return slash switch
        {
            < 0 => null,
            0 => "/",
            _ => path[..slash],
        };
    }

    /// <summary>Finds the format of a file name, or <see langword="null"/> for an unknown extension.</summary>
    /// <param name="fileName">A file name or a path.</param>
    /// <returns>The format.</returns>
    public static DocumentFormat? Find(string fileName)
    {
        string extension = ExtensionOf(fileName);
        return All.FirstOrDefault(format => format.Extension == extension);
    }

    /// <summary>Gets whether WordCraft writes this file as a copy (PDF, PNG, HTML, Markdown, text).</summary>
    /// <param name="fileName">The name WordCraft gave the file.</param>
    /// <returns><see langword="true"/> for a copy.</returns>
    public static bool IsCopy(string fileName) => Find(fileName)?.IsCopy == true;

    /// <summary>
    /// Keeps the extension WordCraft chose: the bytes are in that format, so a path picked in the
    /// save dialog with another extension (or none) gets it appended.
    /// </summary>
    /// <param name="chosenPath">The path the user picked.</param>
    /// <param name="name">The name WordCraft gave the file.</param>
    /// <returns>The path to write.</returns>
    public static string WithExtensionOf(string chosenPath, string name)
    {
        ArgumentNullException.ThrowIfNull(chosenPath);
        string wanted = ExtensionOf(name);
        return wanted.Length == 0 || ExtensionOf(chosenPath) == wanted ? chosenPath : chosenPath + wanted;
    }
}
