using System.Text.Json;
using Kazo.Word.Hosting;

namespace Kazo.Word.Tests;

public sealed class UntitledNameTests
{
    private static FrameCommandResult Answer(string json) => new(true, JsonDocument.Parse(json).RootElement.Clone(), null);

    [Fact]
    public void Takes_WordCrafts_name_in_its_language()
    {
        Assert.Equal("Document1", UntitledName.From(Answer("""{"title":"Document1","documents":[]}""")));
        Assert.Equal("Documento1", UntitledName.From(Answer("""{"title":"Documento1"}""")));
    }

    [Theory]
    [InlineData("""{"documents":[]}""")]
    [InlineData("""{"title":42}""")]
    [InlineData("""{"title":"  "}""")]
    [InlineData("""[]""")]
    public void Keeps_the_windows_name_when_the_answer_has_none(string json)
    {
        Assert.Null(UntitledName.From(Answer(json)));
    }

    [Fact]
    public void Keeps_the_windows_name_when_WordCraft_did_not_answer()
    {
        Assert.Null(UntitledName.From(FrameCommandResult.Failure("timeout")));
    }

    [Fact]
    public void Cleans_and_bounds_the_name()
    {
        Assert.Equal("Doc1", UntitledName.From(Answer("{\"title\":\"Doc\\u00001\\n\"}")));
        string longName = new('a', 500);
        Assert.Equal(UntitledName.MaxLength, UntitledName.From(Answer($$"""{"title":"{{longName}}"}"""))!.Length);
    }
}
