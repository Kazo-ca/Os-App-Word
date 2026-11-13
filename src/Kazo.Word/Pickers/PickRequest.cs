using System.Text.Json;

namespace Kazo.Word.Pickers;

/// <summary>What WordCraft opened its file picker for (decided from the extensions it accepts).</summary>
public enum PickPurpose
{
    /// <summary>A document to open (File › Open): a new Kazo Word window opens it.</summary>
    Document,

    /// <summary>A picture to insert (Insert › Pictures, Change Picture).</summary>
    Picture,

    /// <summary>Fonts to add (one or more font files).</summary>
    Fonts,

    /// <summary>The recipients list of a mail merge.</summary>
    Recipients,

    /// <summary>Any other file WordCraft asks for: handed over as it is.</summary>
    Other,
}

/// <summary>
/// WordCraft opened its file picker (message <c>pick</c>, section 5 of doc/en/integration-plan.md):
/// the window shows the OS dialog instead and answers with <c>picked</c>.
/// </summary>
/// <param name="Id">The pick's id, given back with the answer.</param>
/// <param name="Accept">The input's <c>accept</c>: extensions separated by commas (<c>.png,.jpg</c>).</param>
/// <param name="Multiple">Whether several files may be chosen.</param>
public sealed record PickRequest(int Id, string Accept, bool Multiple)
{
    // Extensions that tell the purposes apart (WordCraft 0.18.1's filters).
    private static readonly HashSet<string> FontExtensions = [".ttf", ".otf", ".ttc", ".otc", ".woff", ".woff2"];
    private static readonly HashSet<string> PictureExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".wmf", ".emf", ".svg", ".tif", ".tiff"];
    private static readonly HashSet<string> RecipientExtensions = [".csv", ".tsv", ".xlsx", ".xls", ".ods", ".mdb", ".accdb", ".vcf"];
    private static readonly HashSet<string> DocumentExtensions = [".docx", ".doc", ".odt", ".rtf", ".dotx"];

    /// <summary>Gets the accepted extensions, lowercase with their dot, without duplicates.</summary>
    public IReadOnlyList<string> Extensions => ParseExtensions(Accept);

    /// <summary>
    /// Gets what the picker is for. Fonts and recipients are recognized by any of their extensions,
    /// pictures when every extension is a picture's, documents by a word-processing extension.
    /// </summary>
    public PickPurpose Purpose => Extensions switch
    {
        { Count: 0 } => PickPurpose.Other,
        var list when list.Any(FontExtensions.Contains) => PickPurpose.Fonts,
        var list when list.Any(RecipientExtensions.Contains) => PickPurpose.Recipients,
        var list when list.All(PictureExtensions.Contains) => PickPurpose.Picture,
        var list when list.Any(DocumentExtensions.Contains) => PickPurpose.Document,
        _ => PickPurpose.Other,
    };

    /// <summary>Reads the JSON <c>word-host.js</c> passes: <c>{ id, accept, multiple }</c>.</summary>
    /// <param name="json">The message's value.</param>
    /// <returns>The request, or <see langword="null"/> when the value is not one.</returns>
    public static PickRequest? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            string accept = root.TryGetProperty("accept", out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
            bool multiple = root.TryGetProperty("multiple", out JsonElement many) && many.ValueKind == JsonValueKind.True;
            return new PickRequest(id.GetInt32(), accept, multiple);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> ParseExtensions(string accept) =>
        [.. (accept ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => entry.StartsWith('.'))
            .Select(entry => entry.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)];
}
