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
    public void The_provenance_names_the_pinned_official_release()
    {
        Assert.Equal("0.18.1", Provenance.GetProperty("version").GetString());
        JsonElement source = Provenance.GetProperty("source");
        Assert.Equal("release-zip", source.GetProperty("kind").GetString());
        Assert.Equal("3daa7c2d1f209a7eeb2527fcefa3fe31e875848fb1970d68f54edae55fe0bc9f", source.GetProperty("sha256").GetString());
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
