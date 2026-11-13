namespace Kazo.Word.Documents;

/// <summary>
/// The documents of one Kazo Word window and their files (section 6 of
/// doc/en/integration-plan.md): which WordCraft document name is bound to which file, what is not
/// saved, the title. Every decision of opening and saving is taken here, in C#, and tested.
/// </summary>
/// <remarks>
/// <para>
/// WordCraft names a document by its file name (the name the window sent with <c>open</c>, or the
/// name it chose at the first save of a new document) and hands every save over as
/// <c>{ name, saveAs, bytes }</c>. The session binds those names to OS paths.
/// </para>
/// <para>
/// WordCraft believes a save is done as soon as it hands it over (it sends no <c>id</c>, and clears
/// its own "modified" state). The session therefore remembers each document whose last save did
/// not reach its file: the window stays "modified", and the next save of that document asks for a
/// path again instead of trusting a binding that just failed.
/// </para>
/// </remarks>
public sealed class DocumentSession
{
    private readonly Dictionary<string, string> _bindings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unsaved = new(StringComparer.Ordinal);

    /// <summary>Gets the name of the window's document: the one opened, or last bound to a file; <see langword="null"/> before any.</summary>
    public string? CurrentName { get; private set; }

    /// <summary>Gets whether WordCraft reports changes on the document it shows.</summary>
    public bool FrameDirty { get; private set; }

    /// <summary>Gets whether the window's document has work that is not in its file.</summary>
    public bool IsDirty => FrameDirty || (CurrentName is not null && _unsaved.Contains(CurrentName));

    /// <summary>Gets the names whose last save did not reach a file.</summary>
    public IReadOnlyCollection<string> UnsavedNames => _unsaved;

    /// <summary>
    /// Gets whether the window still holds only the blank document it starts with, never changed:
    /// a file opened from it can replace it in place instead of opening another window.
    /// </summary>
    public bool IsUntouchedBlank => !Touched && _bindings.Count == 0 && _unsaved.Count == 0;

    /// <summary>Gets whether anything happened to the window's documents (a change, an open, a save).</summary>
    public bool Touched { get; private set; }

    /// <summary>Gets the file a document name is bound to.</summary>
    /// <param name="name">WordCraft's document name.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    public string? PathOf(string name) => _bindings.GetValueOrDefault(name);

    /// <summary>A file was sent to WordCraft with <c>open</c>: its name is bound to its path.</summary>
    /// <param name="name">The name sent (the file's name).</param>
    /// <param name="path">The file's absolute path.</param>
    public void Opened(string name, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(path);
        _bindings[name] = path;
        _unsaved.Remove(name);
        CurrentName = name;
        FrameDirty = false;
        Touched = true;
    }

    /// <summary>WordCraft reported that the shown document has changes, or no longer has any.</summary>
    /// <param name="dirty">The new state.</param>
    public void SetFrameDirty(bool dirty)
    {
        FrameDirty = dirty;
        Touched |= dirty;
    }

    /// <summary>Decides what to do with a file WordCraft saves (the table "Save" of the integration plan).</summary>
    /// <param name="name">The file name WordCraft gave.</param>
    /// <param name="saveAs">WordCraft's <c>saveAs</c>: the file becomes the document's file.</param>
    /// <returns>The decision.</returns>
    public SaveDecision Decide(string name, bool saveAs)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        string? folder = FolderFor(name);
        if (DocumentFormats.IsCopy(name))
        {
            return new SaveDecision(SaveAction.AskForCopy, name, null, folder);
        }

        if (!saveAs && PathOf(name) is { } path && !_unsaved.Contains(name))
        {
            return new SaveDecision(SaveAction.Rewrite, name, path, folder);
        }

        return new SaveDecision(SaveAction.AskAndBind, name, null, folder);
    }

    /// <summary>The file was written.</summary>
    /// <param name="decision">The decision that was carried out.</param>
    /// <param name="path">The file written.</param>
    public void Saved(SaveDecision decision, string path)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentException.ThrowIfNullOrEmpty(path);
        Touched = true;
        if (!decision.SavesTheDocument)
        {
            return;
        }

        _bindings[decision.Name] = path;
        _unsaved.Remove(decision.Name);
        CurrentName = decision.Name;
    }

    /// <summary>The file was not written: the dialog was cancelled, or the write failed.</summary>
    /// <param name="decision">The decision that did not complete.</param>
    public void SaveFailed(SaveDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Touched = true;
        if (!decision.SavesTheDocument)
        {
            return;
        }

        _unsaved.Add(decision.Name);
        CurrentName ??= decision.Name;
    }

    /// <summary>
    /// WordCraft's document was renamed (after a save to a file of another name, the window gives
    /// the document that file's name): the binding and the unsaved mark follow it.
    /// </summary>
    /// <param name="oldName">The previous name.</param>
    /// <param name="newName">The new name.</param>
    public void Renamed(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(oldName);
        ArgumentException.ThrowIfNullOrEmpty(newName);
        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            return;
        }

        if (_bindings.Remove(oldName, out string? path))
        {
            _bindings[newName] = path;
        }

        if (_unsaved.Remove(oldName))
        {
            _unsaved.Add(newName);
        }

        if (string.Equals(CurrentName, oldName, StringComparison.Ordinal))
        {
            CurrentName = newName;
        }
    }

    /// <summary>
    /// Picks, among the documents WordCraft lists (<c>ui.inspect</c>), those with work that is not
    /// in a file: WordCraft says they are modified, or their last save failed.
    /// </summary>
    /// <param name="documents">WordCraft's open documents.</param>
    /// <returns>The documents to save before the window closes.</returns>
    public IReadOnlyList<OpenDocument> WithUnsavedWork(IEnumerable<OpenDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return [.. documents.Where(document => document.Dirty || (document.Name is { } name && _unsaved.Contains(name)))];
    }

    /// <summary>
    /// Builds the window title: <c>*name — Kazo Word</c>, the asterisk while the document has work
    /// that is not in its file; <c>Kazo Word</c> alone before any document.
    /// </summary>
    /// <param name="untitled">The name of a document that has no file yet, in the user's language.</param>
    /// <param name="hasDocument">Whether WordCraft shows a document (it is running).</param>
    /// <returns>The title.</returns>
    public string Title(string untitled, bool hasDocument = true)
    {
        if (!hasDocument)
        {
            return AppName;
        }

        string name = CurrentName is { } current && PathOf(current) is { } path
            ? DocumentFormats.FileNameOf(path)
            : CurrentName ?? untitled;
        return $"{(IsDirty ? "*" : string.Empty)}{name} — {AppName}";
    }

    /// <summary>The product name, never translated.</summary>
    public const string AppName = "Kazo Word";

    // The save dialog opens in the folder of the document's file, else of the window's document.
    private string? FolderFor(string name) =>
        (PathOf(name) ?? (CurrentName is { } current ? PathOf(current) : null)) is { } path
            ? DocumentFormats.FolderOf(path)
            : null;
}
