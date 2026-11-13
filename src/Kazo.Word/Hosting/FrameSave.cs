namespace Kazo.Word.Hosting;

/// <summary>A file WordCraft saves (message <c>save</c>).</summary>
/// <param name="Name">The file name WordCraft gave; its extension tells the format.</param>
/// <param name="SaveAs">WordCraft's <c>saveAs</c>: the file becomes the document's file.</param>
/// <param name="Bytes">The file's content.</param>
public sealed record FrameSave(string Name, bool SaveAs, byte[] Bytes);
