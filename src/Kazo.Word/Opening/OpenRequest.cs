namespace Kazo.Word.Opening;

/// <summary>
/// What a Kazo Word window was started for (section 6 of doc/en/integration-plan.md, "Open from
/// the OS"): a double-click in the File Explorer, "Open with", <c>open &lt;file&gt;</c> or another
/// Kazo Word window start it with the file's absolute path as an argument (Kazo OS ADR 0050), and
/// <c>--webgl</c> forces WordCraft's WebGL 2 renderer.
/// </summary>
/// <param name="Path">The file to open, or <see langword="null"/> for a blank document.</param>
/// <param name="WebGl">Whether <see cref="WebGlArgument"/> was given.</param>
public sealed record OpenRequest(string? Path, bool WebGl)
{
    /// <summary>Launch argument that forces WordCraft's WebGL 2 renderer.</summary>
    public const string WebGlArgument = "--webgl";

    /// <summary>Reads the launch arguments: the first absolute path is the file, in any position.</summary>
    /// <param name="arguments">The arguments, without the app id.</param>
    /// <returns>The request.</returns>
    public static OpenRequest FromArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string? path = arguments.FirstOrDefault(argument => argument.Length > 1 && argument[0] == '/');
        bool webGl = arguments.Contains(WebGlArgument, StringComparer.Ordinal);
        return new OpenRequest(path, webGl);
    }
}
