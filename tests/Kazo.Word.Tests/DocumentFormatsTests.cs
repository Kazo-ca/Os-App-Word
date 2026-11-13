using Kazo.Os.App.Abstractions;
using Kazo.Word.Documents;
using Kazo.Word.Opening;

namespace Kazo.Word.Tests;

/// <summary>The formats, the file associations and the launch arguments (work item 2).</summary>
public sealed class DocumentFormatsTests
{
    [Theory]
    [InlineData(".docx")]
    [InlineData(".docm")]
    [InlineData(".dotx")]
    [InlineData(".dotm")]
    [InlineData(".doc")]
    [InlineData(".dot")]
    [InlineData(".odt")]
    [InlineData(".ott")]
    [InlineData(".fodt")]
    [InlineData(".rtf")]
    public void Kazo_Word_opens_and_edits_the_word_processing_documents(string extension)
    {
        FileHandler handler = Assert.Single(new WordApp().FileHandlers, candidate => candidate.Extensions.Contains(extension));

        Assert.Equal(["open", "edit"], handler.Actions);
    }

    [Theory]
    [InlineData(".md")]
    [InlineData(".txt")]
    [InlineData(".html")]
    [InlineData(".tex")]
    public void Text_formats_are_offered_in_Open_with_only(string extension)
    {
        FileHandler handler = Assert.Single(new WordApp().FileHandlers, candidate => candidate.Extensions.Contains(extension));

        Assert.Equal(["open"], handler.Actions);
    }

    [Fact]
    public void Each_media_type_has_one_handler_and_copies_only_formats_have_none()
    {
        IReadOnlyList<FileHandler> handlers = new WordApp().FileHandlers;

        Assert.Equal(handlers.Count, handlers.Select(handler => handler.MediaType).Distinct().Count());
        Assert.DoesNotContain(handlers, handler => handler.Extensions.Contains(".pdf") || handler.Extensions.Contains(".png"));
    }

    [Theory]
    [InlineData("report.pdf", true)]
    [InlineData("REPORT.PDF", true)]
    [InlineData("page.png", true)]
    [InlineData("notes.md", true)]
    [InlineData("notes.txt", true)]
    [InlineData("page.htm", true)]
    [InlineData("report.docx", false)]
    [InlineData("report.odt", false)]
    [InlineData("report.rtf", false)]
    [InlineData("no-extension", false)]
    public void Copies_are_told_by_their_extension(string name, bool copy)
    {
        Assert.Equal(copy, DocumentFormats.IsCopy(name));
    }

    [Theory]
    [InlineData("/home/neo/letter.docx", "Hello.docx", "/home/neo/letter.docx")]
    [InlineData("/home/neo/letter", "Hello.docx", "/home/neo/letter.docx")]
    [InlineData("/home/neo/letter.odt", "Hello.docx", "/home/neo/letter.odt.docx")]
    [InlineData("/home/neo/LETTER.DOCX", "Hello.docx", "/home/neo/LETTER.DOCX")]
    public void A_chosen_path_keeps_the_format_WordCraft_wrote(string chosen, string name, string written)
    {
        Assert.Equal(written, DocumentFormats.WithExtensionOf(chosen, name));
    }

    [Theory]
    [InlineData("/home/neo/report.docx", "/home/neo", "report.docx")]
    [InlineData("/report.docx", "/", "report.docx")]
    [InlineData("report.docx", null, "report.docx")]
    public void Paths_split_into_folder_and_name(string path, string? folder, string name)
    {
        Assert.Equal(folder, DocumentFormats.FolderOf(path));
        Assert.Equal(name, DocumentFormats.FileNameOf(path));
    }

    [Fact]
    public void The_first_absolute_path_is_the_file_to_open()
    {
        OpenRequest request = OpenRequest.FromArguments(["--webgl", "/home/neo/report.docx", "/home/neo/other.docx"]);

        Assert.Equal("/home/neo/report.docx", request.Path);
        Assert.True(request.WebGl);
    }

    [Fact]
    public void A_plain_launch_opens_a_blank_document()
    {
        OpenRequest request = OpenRequest.FromArguments([]);

        Assert.Null(request.Path);
        Assert.False(request.WebGl);
    }
}
