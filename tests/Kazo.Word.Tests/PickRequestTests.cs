using Kazo.Word.Pickers;

namespace Kazo.Word.Tests;

/// <summary>What WordCraft opened its file picker for, read from the extensions it accepts (work item 3).</summary>
public sealed class PickRequestTests
{
    // WordCraft 0.18.1's filters, seen in the debug host.
    private const string Documents = ".docx,.docm,.dotx,.dotm,.doc,.dot,.odt,.ott,.fodt,.rtf,.txt,.md,.html,.htm,.mht,.mhtml,.tex,.json,.xml";
    private const string Pictures = ".png,.jpg,.jpeg,.gif,.webp,.bmp,.wmf,.emf";
    private const string Fonts = ".ttf,.otf,.ttc,.otc,.woff,.woff2";

    [Theory]
    [InlineData(Documents, PickPurpose.Document)]
    [InlineData(Pictures, PickPurpose.Picture)]
    [InlineData(Fonts, PickPurpose.Fonts)]
    [InlineData(".csv,.xlsx,.docx,.txt", PickPurpose.Recipients)]
    [InlineData(".zip", PickPurpose.Other)]
    [InlineData("", PickPurpose.Other)]
    [InlineData("image/*", PickPurpose.Other)]
    public void The_purpose_comes_from_the_accepted_extensions(string accept, PickPurpose expected)
    {
        Assert.Equal(expected, new PickRequest(1, accept, false).Purpose);
    }

    [Fact]
    public void Extensions_are_lowercase_with_their_dot_and_without_duplicates()
    {
        Assert.Equal([".png", ".jpg"], new PickRequest(1, " .PNG, .jpg,.png,image/*", false).Extensions);
    }

    [Fact]
    public void The_message_of_word_host_is_read()
    {
        PickRequest? request = PickRequest.FromJson("""{"id":4,"accept":".ttf,.otf","multiple":true}""");

        Assert.Equal(new PickRequest(4, ".ttf,.otf", true), request);
        Assert.Equal(PickPurpose.Fonts, request!.Purpose);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"accept":".png"}""")]
    public void A_message_that_is_not_a_pick_gives_nothing(string? json)
    {
        Assert.Null(PickRequest.FromJson(json));
    }
}
