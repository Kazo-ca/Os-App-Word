using System.Text.Json;

namespace Kazo.Word.Hosting;

/// <summary>
/// WordCraft's answer to one command of its page API (<c>window.wordcraft.request</c>), sent by the
/// window through the iframe proxy (message <c>command</c>, answered by <c>result</c>; section 5 of
/// doc/en/integration-plan.md).
/// </summary>
/// <param name="Ok">Whether WordCraft ran the command.</param>
/// <param name="Value">What the command returned (an undefined element when it failed).</param>
/// <param name="Error">Why it failed: WordCraft's own error, a timeout, or a lost connection.</param>
public sealed record FrameCommandResult(bool Ok, JsonElement Value, string? Error)
{
    /// <summary>A failed command.</summary>
    /// <param name="error">Why it failed.</param>
    /// <returns>The result.</returns>
    public static FrameCommandResult Failure(string error) => new(false, default, error);
}
