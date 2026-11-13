using System.Text.Json;
using System.Xml.Linq;

namespace Kazo.Word.Tests;

/// <summary>
/// The licenses travel with the app (work item 5, THIRD-PARTY-NOTICES.md): the project links the
/// repository's license and notices into the package's web root, WordCraft's own notices are in
/// its build folder, and the notices name the WordCraft version actually bundled.
/// </summary>
public sealed class LegalFilesTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string WordCraftFolder = Path.Combine(Root, "src", "Kazo.Word", "wwwroot", "wordcraft");

    [Theory]
    [InlineData("LICENSE", "wwwroot/legal/LICENSE.txt")]
    [InlineData("THIRD-PARTY-NOTICES.md", "wwwroot/legal/THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md", "wwwroot/legal/THIRD-PARTY-NOTICES-fr.md")]
    public void The_project_puts_the_repository_legal_files_in_the_package(string file, string link)
    {
        XDocument project = XDocument.Load(Path.Combine(Root, "src", "Kazo.Word", "Kazo.Word.csproj"));

        // A Content item linked under wwwroot/ is a static web asset: KazoOsPack packs it.
        XElement? item = project.Descendants("Content")
            .FirstOrDefault(element => (string?)element.Attribute("Link") == link);
        Assert.NotNull(item);
        Assert.Equal($"../../{file}", (string?)item.Attribute("Include"));
        Assert.True(File.Exists(Path.Combine(Root, file)), $"{file} is missing at the repository root");
    }

    [Theory]
    [InlineData("LICENSE-MIT")]
    [InlineData("LICENSE-APACHE")]
    [InlineData("NOTICE")]
    [InlineData("ATTRIBUTION.md")]
    public void WordCraft_ships_its_own_notices(string file)
    {
        Assert.True(File.Exists(Path.Combine(WordCraftFolder, file)), $"wordcraft/{file} is missing");
    }

    [Theory]
    [InlineData("THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md")]
    public void The_notices_name_the_bundled_WordCraft_version(string file)
    {
        // Moving to another WordCraft version without updating the notices fails here.
        using JsonDocument provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(WordCraftFolder, "wordcraft.json")));
        string version = provenance.RootElement.GetProperty("version").GetString()!;

        string notices = File.ReadAllText(Path.Combine(Root, file));
        Assert.Contains($"| {version}, ", notices, StringComparison.Ordinal);
        Assert.Contains($"kazo/v{version}", notices, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("description.en.md", "WordCraft")]
    [InlineData("description.fr.md", "WordCraft")]
    [InlineData("description.en.md", "ArtCraft")]
    [InlineData("description.fr.md", "ArtCraft")]
    public void The_store_listing_credits_WordCraft(string file, string name)
    {
        Assert.Contains(name, File.ReadAllText(Path.Combine(Root, "store", file)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_store_tags_are_ones_the_catalog_accepts()
    {
        // Os-AppMarketPlace refuses any other tag (lower case, digits and '-', 30 characters at most):
        // v0.1.0 had "word processor" and its catalog folder had to be corrected by hand.
        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "store", "store.json")));
        foreach (JsonElement tag in store.RootElement.GetProperty("tags").EnumerateArray())
        {
            Assert.Matches("^[a-z0-9][a-z0-9-]{0,29}$", tag.GetString());
        }
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
