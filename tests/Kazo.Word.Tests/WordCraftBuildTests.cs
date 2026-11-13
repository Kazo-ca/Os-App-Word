using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kazo.Word.Tests;

/// <summary>
/// The WordCraft build in <c>src/Kazo.Word/wwwroot/wordcraft/</c> matches its provenance file
/// (decision 13): nothing was edited by hand, nothing is missing, nothing was added.
/// </summary>
public sealed partial class WordCraftBuildTests
{
    private static readonly string BuildFolder = Path.Combine(FindRepositoryRoot(), "src", "Kazo.Word", "wwwroot", "wordcraft");

    private static readonly JsonElement Provenance = ReadProvenance();

    [Fact]
    public void The_provenance_names_the_official_zip_or_the_commit_of_the_submodule()
    {
        JsonElement source = Provenance.GetProperty("source");
        string? kind = source.GetProperty("kind").GetString();

        if (kind == "release-zip")
        {
            // tools/vendor-wordcraft.sh without --dir: the pinned official zip.
            Assert.Equal("0.18.1", Provenance.GetProperty("version").GetString());
            Assert.Equal("3daa7c2d1f209a7eeb2527fcefa3fe31e875848fb1970d68f54edae55fe0bc9f", source.GetProperty("sha256").GetString());
            return;
        }

        // Work item 4: our own build. It must come from the commit the submodule points at, so
        // moving the submodule without rebuilding (or the reverse) fails here.
        Assert.Equal("source-build", kind);
        Assert.Equal(SubmoduleCommit(), source.GetProperty("commit").GetString());
        Assert.Equal("https://github.com/Kazo-ca/wordcraft.git", source.GetProperty("repository").GetString());
    }

    [Fact]
    public void Every_listed_file_has_its_size_and_SHA_256()
    {
        foreach (JsonElement file in Provenance.GetProperty("files").EnumerateArray())
        {
            string path = file.GetProperty("path").GetString()!;
            string full = Path.Combine(BuildFolder, path);
            Assert.True(File.Exists(full), $"{path} is listed but missing");
            Assert.Equal(file.GetProperty("size").GetInt64(), new FileInfo(full).Length);
            using FileStream stream = File.OpenRead(full);
            Assert.Equal(file.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(stream)));
        }
    }

    [Fact]
    public void No_file_is_there_without_being_listed()
    {
        HashSet<string> listed = Provenance.GetProperty("files").EnumerateArray()
            .Select(file => file.GetProperty("path").GetString()!)
            .Append("wordcraft.json")
            .ToHashSet(StringComparer.Ordinal);

        IEnumerable<string> present = Directory.EnumerateFiles(BuildFolder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(BuildFolder, file).Replace('\\', '/'));

        Assert.All(present, path => Assert.True(listed.Contains(path), $"{path} is not in wordcraft.json"));
    }

    [Fact]
    public void The_glue_and_the_wasm_are_listed_with_the_licences()
    {
        HashSet<string> listed = Provenance.GetProperty("files").EnumerateArray()
            .Select(file => file.GetProperty("path").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(Provenance.GetProperty("glue").GetString()!, listed);
        Assert.Contains(Provenance.GetProperty("wasm").GetString()!, listed);
        Assert.Contains("LICENSE-MIT", listed);
        Assert.Contains("LICENSE-APACHE", listed);
        Assert.Contains("NOTICE", listed);
        Assert.Contains("ATTRIBUTION.md", listed);
    }

    [Fact]
    public void Every_module_the_glue_imports_is_there()
    {
        string glue = File.ReadAllText(Path.Combine(BuildFolder, Provenance.GetProperty("glue").GetString()!));

        string[] imports = [.. RelativeImport().Matches(glue).Select(match => match.Groups["path"].Value)];

        Assert.NotEmpty(imports);
        Assert.All(imports, path => Assert.True(File.Exists(Path.Combine(BuildFolder, path)), $"{path} is imported but missing"));
    }

    // `import ... from './snippets/...'` in the wasm-bindgen glue.
    [GeneratedRegex(@"from\s+['""]\./(?<path>[^'""]+)['""]")]
    private static partial Regex RelativeImport();

    // The commit recorded for external/wordcraft in git's index (the gitlink), which is there even
    // when the submodule itself is not checked out (CI fetches only the Kazo OS one).
    private static string SubmoduleCommit()
    {
        System.Diagnostics.ProcessStartInfo start = new("git", ["ls-files", "--stage", "--", "external/wordcraft"])
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using System.Diagnostics.Process git = System.Diagnostics.Process.Start(start)!;
        string output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();

        // "160000 <commit> 0\texternal/wordcraft"
        Match match = Gitlink().Match(output);
        Assert.True(git.ExitCode == 0 && match.Success, $"external/wordcraft is not a submodule in git's index: {output}");
        return match.Groups["commit"].Value;
    }

    [GeneratedRegex(@"^160000 (?<commit>[0-9a-f]{40}) ")]
    private static partial Regex Gitlink();

    private static JsonElement ReadProvenance()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(BuildFolder, "wordcraft.json")));
        return document.RootElement.Clone();
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kazo.Word.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Kazo.Word.slnx not found above " + AppContext.BaseDirectory);
    }
}
