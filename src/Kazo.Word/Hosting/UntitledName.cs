using System.Text.Json;

namespace Kazo.Word.Hosting;

/// <summary>
/// The name of a document that has no file yet, as WordCraft shows it (section 6 of
/// doc/en/integration-plan.md, "Title"). WordCraft names it in its interface language, which the
/// window sets to the Kazo user's language (<c>Document1</c> in English); the OS title bar takes the
/// same name, so the window and the page never disagree.
/// </summary>
public static class UntitledName
{
    /// <summary>The longest name taken from WordCraft: a title bar has no room for more.</summary>
    public const int MaxLength = 120;

    /// <summary>
    /// Reads the shown document's name from WordCraft's <c>ui.inspect</c> answer (its
    /// <c>title</c> property).
    /// </summary>
    /// <param name="inspect">The answer of <c>ui.inspect</c>.</param>
    /// <returns>The name, or <see langword="null"/> when the answer has none (keep the window's own).</returns>
    public static string? From(FrameCommandResult inspect)
    {
        ArgumentNullException.ThrowIfNull(inspect);
        if (!inspect.Ok || inspect.Value.ValueKind != JsonValueKind.Object
            || !inspect.Value.TryGetProperty("title", out JsonElement title)
            || title.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        // The page is untrusted input: no control characters, a bounded length.
        string name = new string([.. title.GetString()!.Where(c => !char.IsControl(c))]).Trim();
        if (name.Length > MaxLength)
        {
            name = name[..MaxLength];
        }

        return name.Length == 0 ? null : name;
    }
}
