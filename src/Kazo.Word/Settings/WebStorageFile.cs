using System.Text.Json;
using Kazo.Os.AppSdk.IO;

namespace Kazo.Word.Settings;

/// <summary>
/// WordCraft's settings for one Kazo user: <c>~/.config/ca.kazo.word/web-storage.json</c>, the
/// content of the iframe's <c>localStorage</c> as WordCraft wrote it (<c>wordcraft.ui</c>: theme,
/// recent files, AutoCorrect, dictionary...). Trial of work item 3 (doc/en/integration-plan.md,
/// section 7): each Kazo user gets their own WordCraft settings, instead of one browser storage
/// shared by everyone on the machine. Reached with <c>filesystem.private.read</c> and <c>.write</c>
/// (ADR 0044 of Kazo OS), as a Linux program keeps its settings under <c>~/.config/&lt;program&gt;/</c>.
/// </summary>
public sealed class WebStorageFile
{
    /// <summary>The app's folder under <c>~/.config</c>: its id, as the OS expects (ADR 0044).</summary>
    public const string FolderName = "ca.kazo.word";

    /// <summary>The file's name.</summary>
    public const string FileName = "web-storage.json";

    private readonly Func<string, CancellationToken, Task<string>> _readAllText;
    private readonly Func<string, string, CancellationToken, Task> _writeAllText;
    private readonly Func<string, CancellationToken, Task> _createDirectory;
    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);

    // One write at a time: the Kazo OS file system can take longer than the window's half second
    // between two saves, and an older write that finished last would put back older values.
    private readonly SemaphoreSlim _saving = new(1, 1);

    /// <summary>Creates the file of a user, read and written through the Kazo OS file system.</summary>
    /// <param name="homeDirectory">The user's home (<c>AppContext.HomeDirectory</c>).</param>
    public WebStorageFile(string homeDirectory)
        : this(
            homeDirectory,
            (path, token) => KazoFile.ReadAllTextAsync(path, token),
            (path, text, token) => KazoFile.WriteAllTextAsync(path, text, token),
            (path, token) => KazoDirectory.CreateDirectoryAsync(path, token))
    {
    }

    /// <summary>Creates the file with its own file access (the tests).</summary>
    /// <param name="homeDirectory">The user's home.</param>
    /// <param name="readAllText">Reads a file; throws an <see cref="IOException"/> when it cannot.</param>
    /// <param name="writeAllText">Writes a file, replacing it.</param>
    /// <param name="createDirectory">Creates a folder and its parents; does nothing when it exists.</param>
    public WebStorageFile(
        string homeDirectory,
        Func<string, CancellationToken, Task<string>> readAllText,
        Func<string, string, CancellationToken, Task> writeAllText,
        Func<string, CancellationToken, Task> createDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        ArgumentNullException.ThrowIfNull(readAllText);
        ArgumentNullException.ThrowIfNull(writeAllText);
        ArgumentNullException.ThrowIfNull(createDirectory);
        Directory = $"{homeDirectory.TrimEnd('/')}/.config/{FolderName}";
        Path = $"{Directory}/{FileName}";
        _readAllText = readAllText;
        _writeAllText = writeAllText;
        _createDirectory = createDirectory;
    }

    /// <summary>Gets the folder of the file.</summary>
    public string Directory { get; }

    /// <summary>Gets the file's path.</summary>
    public string Path { get; }

    /// <summary>Gets the entries as last loaded or changed.</summary>
    public IReadOnlyDictionary<string, string> Entries => _entries;

    /// <summary>
    /// Reads the file. A missing, unreadable or corrupt file gives no entries: WordCraft starts with
    /// its defaults, as on a first run, rather than not at all.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entries.</returns>
    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        _entries.Clear();
        string text;
        try
        {
            text = await _readAllText(Path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return _entries;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        _entries[property.Name] = property.Value.GetString()!;
                    }
                }
            }
        }
        catch (JsonException)
        {
            _entries.Clear();
        }

        return _entries;
    }

    /// <summary>Records one write of WordCraft; <see cref="SaveAsync"/> writes the file.</summary>
    /// <param name="key">The storage key.</param>
    /// <param name="value">Its new value; <see langword="null"/> when WordCraft removed it.</param>
    /// <returns><see langword="true"/> when the entries changed.</returns>
    public bool Set(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (value is null)
        {
            return _entries.Remove(key);
        }

        if (_entries.TryGetValue(key, out string? current) && current == value)
        {
            return false;
        }

        _entries[key] = value;
        return true;
    }

    /// <summary>
    /// Writes the entries, creating the folder the first time. A save that starts while another is
    /// writing waits for it, then writes the entries as they are at that moment: the last save
    /// always leaves the latest values in the file.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task completed once the file is written.</returns>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saving.WaitAsync(cancellationToken);
        try
        {
            string text = Serialize();
            await _createDirectory(Directory, cancellationToken);
            await _writeAllText(Path, text, cancellationToken);
        }
        finally
        {
            _saving.Release();
        }
    }

    private string Serialize()
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach ((string key, string value) in _entries.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
