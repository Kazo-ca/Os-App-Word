> [Version française](../fr/integration-plan.md)

# Detailed integration plan of Kazo Word

Asked for by Benoit on 2026-11-13, after the [check](plan-verification.md) of the
[plan](plan.md). The plan says what and why; this page says **how**: the files, the contracts
between the three parts, how each case runs, then for each work item what is delivered, the
tests and the completion criterion. Each work-item thread starts from this page.

## 1. What is decided

| # | Decision | Source |
| --- | --- | --- |
| 1 to 9 | See the [plan](plan.md). | 2026-11-11 and 2026-11-12 |
| 10 | The Kazo OS service worker serves the iframe page of an installed app (fix in `Kazo-ca/Os`, separate thread). Kazo Word requires the OS version that has it. | Benoit, 2026-11-13 |
| 11 | Upstream, we join the existing issues; a new issue only for what is missing. Claude prepares the texts, Benoit posts them. | Benoit, 2026-11-13 |
| 12 | The spell checker in English only is accepted for v1, noted as a known limit. | Benoit, 2026-11-13 |
| 13 | The built WordCraft lives in this repository, under `wwwroot/wordcraft/` (section 3). | Benoit, 2026-11-13 |
| 14 | Kazo OS keeps a stable order of the windows in the page; a window coming to the front no longer reloads its iframe (fix in `Kazo-ca/Os`). | Benoit, 2026-11-13 |
| 15 | The real `.wasm` is 61 MB: decision 13 stays. | Benoit, 2026-11-13 |
| 16 | No Kazo menu for v1: WordCraft's File tab is enough (the SDK gives no window menu yet). | Benoit, 2026-11-13 |
| 17 | Kazo OS raises its maximum file size (16 MiB today, ADR 0094) so the 60 MB `.wasm` installs: Kazo OS ADR 0245, 128 MiB per file, released in Kazo OS 3.3.2. | Benoit, 2026-11-13 |
| 18 | The first Store version stays **0.1.0** (the manifest's version): the number says it is a first trial, WordCraft being in alpha. | Benoit, 2026-11-13 |

## 2. The three parts

```
┌─ Kazo Word window (Blazor, C#) ─────────────────────────────────┐
│ WordWindow.razor  ⇄  js/word-host.js   (the OS document)         │
│        │                    ⇅  MessagePort (one per window)      │
│        │        ┌─ iframe  _content/Kazo.Word/host/index.html ─┐ │
│        │        │ host/word-proxy.js                           │ │
│        │        │      ⇅  CustomEvent on the canvas            │ │
│        │        │ WordCraft (.wasm), data-host="page"          │ │
│        │        └──────────────────────────────────────────────┘ │
│  KazoFile, OS dialogs, title, closing                            │
└──────────────────────────────────────────────────────────────────┘
```

- **The window (C#)** decides: which file is bound to which document, where we write, the
  title, closing. All testable logic is in C#.
- **`js/word-host.js`** (in the OS document, loaded by the component) creates the iframe,
  holds the `MessagePort` and calls the C#. It decides nothing.
- **`host/word-proxy.js`** (in the iframe) talks to WordCraft: canvas events,
  `window.wordcraft`, file picker, keys. It decides nothing either.

One `MessagePort` per window, handed to the iframe when it loads: two windows cannot mix, and
no message goes through `window.postMessage` after the first one.

## 3. Where the WordCraft build lives (decision 13)

Finding of 2026-11-13: the Store builds an app from the commit named in `store.json`, with
`dotnet build -t:KazoOsPack` only, and fetches only the `external/KazoOs` submodule (the
`build-app` action of Os-AppMarketPlace). It has no Rust, no Trunk and no WordCraft fork. The
Store therefore cannot build WordCraft: it needs a build already made.

- **A. In this repository ✨**: `wwwroot/wordcraft/` holds the `.js`, the `.wasm` (61 MB,
  measured in work item 1; the plan said about 13 MB) and the licences, with a provenance file (version, commit, SHA-256). A script
  updates them: from the official zip (work item 1), then from our own build (work item 4),
  through a PR. This is what Webamp does. The repository grows by about 22 MB (the compressed
  `.wasm`) at each WordCraft version upgrade, and GitHub warns on push about a file over
  50 MB (it refuses one over 100 MB).
- **B. Downloaded during the build**: an MSBuild target downloads the pinned zip and checks
  its SHA-256. The repository stays light and the Store has nothing to change (its build
  machine has network access). But the package content is no longer entirely in the reviewed
  commit, each build depends on a download, and from work item 4 on we must publish our own
  builds somewhere.
- **C. Git LFS**: a light repository and files tracked by git, but the Store does not fetch
  LFS files today: its build action would have to change.

**Benoit chose A on 2026-11-13.**

## 4. Files

```
Kazo.Word.slnx
src/Kazo.Word/                      the app (assembly Kazo.Word)
  WordApp.cs                        [AppManifest]: ca.kazo.word, formats, capabilities, window
  WordWindow.razor(.cs)(.css)       the window: iframe, loading, error, close guard
  Hosting/WordFrame.cs              C# wrapper of js/word-host.js (calls and events)
  Hosting/FrameMessages.cs          the message types (section 5)
  Hosting/FrameStartup.cs           loading, WordCraft or error, 60 s timeout, "Retry"
  Documents/DocumentSession.cs      a window's state: bound documents, "modified", decisions
  Documents/SaveDecision.cs         rewrite, ask for a path, exported copy
  Documents/DocumentFormats.cs      extensions, types, dialog filters
  Opening/OpenRequest.cs            file asked for at launch or by intent
  WordWindow.Proxy.cs               picker, reserved keys, settings, theme and language
  Hosting/ReservedKeys.cs           OS shortcuts and Ctrl+N forwarded by the proxy
  Hosting/WordCraftLanguage.cs      WordCraft language from the Kazo culture
  Pickers/PickRequest.cs            document, picture, fonts, recipients (from accept)
  Settings/WebStorageFile.cs        ~/.config/ca.kazo.word/web-storage.json
  Strings.cs, Resources/*.resx      French and English texts, localization/fr.json
  wwwroot/host/index.html           iframe page: canvas data-host="page", loading
  wwwroot/host/word-proxy.js        the proxy
  wwwroot/host/file-picker.js       catches rfd's file field
  wwwroot/host/reserved-keys.js     forwards the reserved shortcuts
  wwwroot/host/settings-store.js    in-memory localStorage sent to the window
  wwwroot/host/word-proxy.css
  wwwroot/js/word-host.js           OS side: iframe and MessagePort
  wwwroot/wordcraft/                WordCraft build, never edited by hand
    wordcraft.json                  provenance: version, source, names and SHA-256 of the files
    wordcraft-web-<hash>.js, wordcraft-web-<hash>_bg.wasm, LICENSE-*, NOTICE, ATTRIBUTION.md
  wwwroot/icons/app.svg
src/Kazo.Word.Os/                   debug host (from the template)
tests/Kazo.Word.Tests/              xUnit
tests/proxy/                        node --test, with a fake WordCraft
tests/e2e/                          Playwright (node --test), in the debug host
tools/vendor-wordcraft.sh           pinned zip or built folder → wwwroot/wordcraft/
external/KazoOs                     submodule (from the template)
external/wordcraft                  submodule of the fork, only needed to compile WordCraft
tools/build-wordcraft.sh            builds external/wordcraft, then vendor-wordcraft.sh --dir
THIRD-PARTY-NOTICES.md              work item 5
```

Identity: id `ca.kazo.word`, publisher `pub.kazo`, name "Kazo Word", category
`productivity`, several instances (`SingleInstancePerUser` is `false`), default window
1100 × 760, minimum 760 × 480 (WordCraft's minimum).

## 5. Contract between the window and the proxy

Messages on the `MessagePort`, all shaped `{ type, id?, ... }`. Bytes travel as a transferred
`ArrayBuffer`, without a copy.

| Message | Direction | Content | Role |
| --- | --- | --- | --- |
| `init` | window → proxy | `author`, `language`, `reservedKeys`, `settings` | First message, with the port. WordCraft starts only after it. |
| `ready` | proxy → window | `appVersion` | WordCraft is listening. |
| `failed` | proxy → window | `error` | WordCraft did not start (no WebGPU nor WebGL2, wasm not found). |
| `open` | window → proxy | `name`, `bytes` | Opens a document. |
| `save` | proxy → window | `name`, `saveAs`, `bytes` | Every file WordCraft would download. |
| `dirty` | proxy → window | `dirty` | The shown document has changes. |
| `pick` | proxy → window | `id`, `accept`, `multiple` | WordCraft opens the file picker. |
| `picked` | window → proxy | `id`, `files` or `cancelled` | Answer to the picker. |
| `command` | window → proxy | `id`, `method`, `params`, `timeoutMs` | Calls `window.wordcraft.request` (10 s by default). Theme and language go through it (`ui.theme`, `ui.language`). |
| `result` | proxy → window | `id`, `ok`, `value` or `error` | Answer to `command`. |
| `key` | proxy → window | `phase`, `target`, key and modifiers | A reserved shortcut typed in the iframe: `os` is replayed on the iframe element for the OS, `app` is handled by the window (`Ctrl+N`). |
| `setting` | proxy → window | `key`, `value` | WordCraft wrote to its `localStorage`; the window writes it to `~/.config/ca.kazo.word/web-storage.json`. |
| `focus` | proxy → window | nothing | A click in the iframe: the Kazo window comes to the front. |

The proxy translates `open`, `save`, `dirty`, `ready`, `failed` from and to the `wordcraft:*`
events of the canvas, without interpreting them.

## 6. How each case runs

**Start.** The window shows "Loading", `word-host.js` creates the iframe
(`allow="fullscreen; clipboard-read; clipboard-write"`) on
`_content/Kazo.Word/host/index.html?author=<display name>`. The proxy adds its listeners on
the canvas, reads `wordcraft/wordcraft.json`, imports the `.js` and calls `init` with the
`.wasm`. On `ready`, the window sets the theme and language (`ui.theme`, `ui.language`),
sends the requested file or creates an empty document, then removes "Loading". With no
`ready` nor `failed` after 60 seconds: an error screen with "Retry".

**Open from the OS.** Double-click, "Open with" or intent: `OpenRequest` gives the path, the
window reads with `KazoFile`, sends `open` and binds the name to the path. A file opened
while the window already has a document goes to a **new window**.

**Save.** `DocumentSession` decides from `name`, `saveAs` and the extension:

| Case | Decision |
| --- | --- |
| `name` is bound to a path and `saveAs` is false | Rewrites that path. |
| `saveAs` is true, or `name` is bound to nothing (untitled document, import saved in another format) | OS save dialog, then the document is bound to the chosen path. |
| Copy format (PDF, PNG, text, Markdown) | Save dialog, the document stays bound to its file. |

If the write fails or the dialog is cancelled, the window shows the error and **stays
"modified"** even though WordCraft believes it is saved; the next save asks for a path again.

**"Modified".** The window's state is: WordCraft says `dirty`, **or** a save has not gone
through since the last successful write. The title carries the asterisk.

**Close.** `IWindowCloseGuard`: the window asks `ui.inspect`, which lists every document of
the page with its state. If there are changes: "Save", "Don't save", "Cancel". "Save" sends
`file.save` and waits for the write to succeed before closing.

**File › Open inside WordCraft.** The proxy stops the click on the file field and sends
`pick`. For a document, the window opens the OS dialog then launches a new Kazo Word window
on that file and answers `cancelled`; if the window only has an empty, untouched document, it
opens the file in place. For a picture, fonts or recipients, it reads the files and answers
`picked`: the proxy hands them to the field and fires `change`.

**File › New inside WordCraft.** `Ctrl+N` is taken by the proxy and opens a new window. The
button of the File tab creates a second document in the page: it is followed by its name and
by `ui.inspect`, and its first save asks for a path.

**OS shortcuts.** The proxy listens for `keydown` in the capture phase; a key of the
`reservedKeys` list is stopped and sent as `key`, and `word-host.js` replays it on the OS
document. Everything else goes to WordCraft.

**Theme and language.** On `ready`, then when the OS changes: `ui.theme` (light or dark, read
on the window) and `ui.language` (the Kazo user's language, English if WordCraft lacks it).

## 7. Work items

Series `word/base`, one branch and one PR to `word/base` per work item, as in the plan. No
work item starts without Benoit's go.

### Prerequisites

| What | Who | State |
| --- | --- | --- |
| Stable order of the windows in the page (decision 14) | Thread "Garder l'ordre des fenêtres stable" | Done: PR [Os#452](https://github.com/Kazo-ca/Os/pull/452) (ADR 0244), released in Kazo OS 3.3.1 on 2026-11-13. |
| Fix of the Kazo OS service worker (decision 10) | Thread "Servir la page iframe des apps installées" | Done: PR [Os#451](https://github.com/Kazo-ca/Os/pull/451) (ADR 0243), released in Kazo OS 3.3.1 on 2026-11-13. Kazo Word requires Kazo OS 3.3.1 or later. |
| A file over 16 MiB in Kazo OS (decision 17) | Thread "Relever la taille maximale de fichier" | Done: 128 MiB per file, PR [Os#455](https://github.com/Kazo-ca/Os/pull/455) (ADR 0245), released in Kazo OS 3.3.2 on 2026-11-13. Kazo Word requires Kazo OS 3.3.2 or later. |
| Forks `Kazo-ca/wordcraft`, `Kazo-ca/craft-libs`, `Kazo-ca/gridcraft` (decision 9) | Benoit, through the coordinator | Before work item 4. |

### Work item 1: solution and iframe (`word/1-solution`)

A trial that must prove WordCraft starts in a Kazo window.

- Rename the template's solution (`Kazo.Word`, `Kazo.Word.Os`, `Kazo.Word.Tests`), `WordApp`
  with the identity of section 4, without file associations yet.
- `tools/vendor-wordcraft.sh`: downloads `wordcraft-web-0.18.1.zip`, checks the pinned
  SHA-256, copies the `.js`, the `.wasm` and the licences, writes `wordcraft.json`.
- `host/index.html`, `host/word-proxy.js` (start, `ready`, `failed`), `js/word-host.js`,
  `WordWindow` with its three states: loading, WordCraft, error.
- Tests: manifest; `wordcraft.json` matches the files (SHA-256); proxy with a fake WordCraft
  (`ready`, `failed`, timeout); one Playwright journey that waits for `ready`.
- **Done when**: in the debug host, WordCraft shows and text can be typed, with WebGPU and
  with `?webgl`; two windows run at once; a click in the iframe brings the window to the
  front; the size of the `.kazoapp` package is written in the plan; the result in a published
  OS is written down (expected: failing until decision 10).

**Result (2026-11-13).**

- WordCraft shows in about 4 seconds in the debug host (Release, Kazo OS 3.3.0) and typed text
  reaches the document, with the WebGPU renderer (`BrowserWebGpu`) and with the WebGL 2
  renderer (launch argument `--webgl`, which adds `?webgl` to the iframe). WordCraft falls back
  to WebGL 2 by itself when the browser has no WebGPU adapter.
- Two windows run at once, each with its own document; a click in WordCraft brings its window
  to the front.
- In host mode, WordCraft opens on its start screen (new document, templates): work item 2
  sends the requested file or creates an empty document.
- `.kazoapp` package: 22.9 MB, 61.3 MB extracted, 31 files (limits: 64 MiB, 128 MiB, 2000
  files). The `.wasm` alone is 61 MB.
- Memory (headless Chromium, software rendering, start screen): about 130 MB for the first
  window, then about 25 MB per extra window (four windows: 233 MB more than without Kazo Word).
  The compiled module is shared; a large document will cost more.
- **Found**: Kazo OS orders the windows in the page by their stacking order. When a window comes
  to the front its element is moved, and a moved iframe reloads: WordCraft starts again and the
  document is lost (decision 14, section 9). `word-host.js` reconnects the reloaded page, but
  the document is gone.
- Kazo OS 3.3.1 (2026-11-13) fixes both: a document survives when its window comes to the front
  and goes back behind (Playwright journey, failing on 3.3.0, passing on 3.3.1), and the published
  service worker serves the iframe page. The submodule moved to `v3.3.1`.
  The OS also brings to the front a window that gets the focus through a click in its iframe; the
  `pointerdown` that `word-host.js` replays on the iframe stays, it only has the same effect.
- **Published OS (3.3.1)**: `sudo kazopkg install Kazo-ca/Os-App-Word` reads the package, then
  fails on the `.wasm`: `FileTooLarge`. The Kazo OS file system refuses any file over 16 MiB
  (ADR 0094), and the `.wasm` is 60 MB (22 MB gzipped). Decision 17: Kazo OS raises its limit.
- **Published OS (3.3.2, 2026-11-13)**: with 128 MiB per file (ADR 0245, decision 17),
  `sudo kazopkg install Kazo-ca/Os-App-Word` installs the package (22.8 MB) in 16 s; Kazo Word
  opens from the menu, WordCraft runs after 8 s in the page served by the published service
  worker (no second Kazo OS), and typed text reaches the document, with WebGPU and with WebGL 2
  (`launch ca.kazo.word --webgl`). The submodule is on `v3.3.2`, the minimum OS version.

### Work item 2: open and save (`word/2-open-save`)

- File associations and "Open with" (formats of the plan), `OpenRequest`, intents.
- `DocumentSession`, `SaveDecision`, `DocumentFormats`; messages `open`, `save`, `dirty`.
- OS dialogs, title with asterisk, close guard with `ui.inspect`.
- xUnit tests: each row of the "Save" table, write failure, cancelled dialog, export, two
  documents in one page, title, closing. Playwright: open a `.docx` from the explorer, edit
  it, save it, reopen it; Save As; PDF export; close with changes; two windows.
- **Done when**: no case loses changes unless the user chose it, including when the write
  fails.

**Result (2026-11-13).**

- What WordCraft 0.18.1 sends, checked in its page: a new document is named after its first
  words ("Hello.docx") and its first save arrives with `saveAs` true, the next ones with the same
  name and `saveAs` false; Save As to `.odt` arrives with `saveAs` true; a PDF, PNG, HTML,
  Markdown or text file arrives with `saveAs` false (copies). WordCraft clears its "modified"
  state as soon as it has sent the save.
- `DocumentSession` binds each WordCraft document name to a path. A copy never changes the
  binding. A save that does not complete (dialog cancelled, write refused, any other error)
  leaves the window "modified", and the next save of that document asks for a path again. When
  the user picks another file name, the window renames the document in WordCraft
  (`file.rename`) so its title and later saves carry the file's name.
- Start: a requested file (launch argument) is read with `KazoFile` and sent with `open`;
  otherwise a blank document (`file.new`). Either way WordCraft's start screen is closed
  (`ui.backstage {"value": false}`).
- Title: `*name — Kazo Word`, "Untitled" before the first save.
- Close: `ui.inspect` lists the documents; "Yes" saves each one (`engine.execute` `file.save`
  with `doc`) and closes only once all are written; "No" closes; "Cancel" keeps the window. If
  WordCraft does not answer, the window only asks whether to close without saving.
- Associations: the ten word-processing formats with `open` and `edit`; `.md`, `.txt`, `.html`,
  `.htm`, `.tex` with `open` only. Capabilities: Open and Save dialogs, home folder read and
  write, `apps.launch` (open a file in another Kazo Word window). `.tex` has no other app in
  Kazo OS: Kazo Word becomes its default app in practice.
- For work item 3: `WordWindow.OpenInNewWindowAsync(path)` (in place when the window holds only
  an untouched blank document, otherwise a new window), `ShowOpenDialogAsync()` and
  `IsUntouchedBlank`.
- Found: a `[JSInvokable]` callback does not run in the app's context; without
  `KazoExecutionContext.Enter(AppContext)`, `KazoFile` refuses. The Playwright run showed it.
- Tests: 57 more xUnit cases (`DocumentSessionTests`, `DocumentFormatsTests`); three Playwright
  runs (`tests/e2e/open-save.e2e.mjs`): save a new document, save it again without a dialog,
  reopen it with `xdg-open` in a second window; Save As to `.odt` then a PDF copy; close with
  changes (Cancel, cancelled dialog, then Yes). The write failure is covered in xUnit.

### Work item 3: full proxy (`word/3-proxy`)

- File picker (the four uses), `Ctrl+N` and File › Open in a new window.
- OS shortcuts (`reservedKeys`), author, theme, language, focus.
- Window menu: New, Open, Save, Save As, through `command`.
- Trial: settings per Kazo user. The proxy loads `~/.config/ca.kazo.word/` into the iframe's
  `localStorage` before `init` and sends the writes back to the window. If it holds, the
  "shared settings" risk goes away with nothing to ask upstream.
- Check printing and the clipboard; write down what does not work.
- Tests: proxy with a fake WordCraft (interception of the field for each use, reserved and
  unreserved keys, `command` and timeout); Playwright: insert a picture from the Kazo files,
  `Alt+F4` closes the window, the theme follows the OS.
- **Done when**: no browser dialog opens from Kazo Word, and the OS shortcuts work while the
  caret is in the document.

**Result (2026-11-13).**

- Picker: `host/file-picker.js` catches rfd's file field (click, or the `#rfd-overlay` card
  without activation) and sends `pick`. The use comes from the accepted extensions (document,
  picture, fonts, recipients). The window shows the OS dialog, reads the files and answers
  `picked`. For a document, WordCraft hears "cancelled" and the file opens through work item 2's
  `OpenInNewWindowAsync` (a new window, or in place for an untouched blank document).
  Cancelling goes through a `change` with no file: the `cancel` event makes WordCraft 0.18.1
  panic.
- Shortcuts: `host/reserved-keys.js` keeps the OS default shortcuts (an app cannot read the
  user's keybindings file) and `Ctrl+N`. The OS binds `Alt+F4` only in full screen (keyboard
  lock); outside it, `Alt+Q` closes.
- Theme and language: the window sends `ui.theme` on each OS theme change and picks the
  WordCraft language closest to the Kazo culture (`WordCraftLanguage`).
- Settings trial: **it holds**. `localStorage` is replaced in the iframe by an in-memory copy
  loaded from `~/.config/ca.kazo.word/web-storage.json`, written at most 500 ms after the first unsaved write.
  With two windows, the last write wins. Added fonts (IndexedDB) stay shared by the origin.
- Window menu: dropped (decision 16).
- The proxy's messages arrive outside the Kazo execution context: each handler that reaches
  the SDK calls `KazoExecutionContext.Enter`.
- Noted limits: printing goes through the browser's print dialog (accepted exception);
  "System fonts" asks the browser's local-fonts permission; copy and paste work with the
  system clipboard, but one trial pasted the text copied earlier twice (to look at); the focus
  relay becomes useless once Kazo OS brings the window forward on a click in its iframe (to
  review at the submodule upgrade).
- Tests: 31 proxy tests (`npm test`), 125 xUnit tests with work item 2, 6 Playwright journeys
  (`tests/e2e/proxy.e2e.mjs`), which fail if the browser's file chooser opens.

### Work item 4: build from source (`word/4-build-from-source`)

- Submodule `external/wordcraft` (fork). The Store does not fetch it and does not need it.
- Workflow `build-wordcraft.yml`, run by hand: stable Rust and the `wasm32-unknown-unknown`
  target, Trunk 0.21.14 checked by SHA-256, `craft-i18n` at the revision in `craft-i18n.rev`,
  `craft-fonts` at the revision of the upstream workflow, `trunk build --release`, then
  `tools/vendor-wordcraft.sh` on the result and a PR that replaces `wwwroot/wordcraft/`.
- The tests of work items 1 to 3 run on this build: that is the proof of "same behaviour as
  the zip".
- As soon as a change to WordCraft ships: remove the ArtCraft name and logo from the build,
  and say so in `wordcraft.json`.
- Measurements for decision 8 (shared library): size of the `.wasm`, of the fonts and of the
  translations, what could be loaded separately.
- **Done when**: a WordCraft version upgrade is done by changing the submodule commit and
  running the workflow.

**Result (2026-11-13).**

- Submodule `external/wordcraft` on the `Kazo-ca/wordcraft` fork, at the commit of release
  0.18.1 (`079f478b`, "Merge tested main for v0.18.1"). That commit is on the upstream `release`
  branch, which the fork had not copied: the fork keeps it with the `kazo/v0.18.1` branch, named
  in `.gitmodules`. A version upgrade does the same: a `kazo/v<version>` branch of the fork from
  the upstream tag, then the submodule on it.
- `tools/build-wordcraft.sh` builds like the upstream release: it reads the revisions of
  `craft-i18n` (`craft-i18n.rev`) and `craft-fonts` (`craft-fonts-ref` of the upstream workflow)
  from the submodule and refuses other checkouts, requires Trunk 0.21.14 and the wasm32 target,
  runs WordCraft's `packaging/web/package.sh` (licences and font licences included), with the
  commit date as build date, then `tools/vendor-wordcraft.sh --dir`.
- `tools/vendor-wordcraft.sh --dir <folder>`: the "built folder" source. `wordcraft.json` then
  says `"kind": "source-build"`, the repository, the submodule's commit (which must be clean),
  the `craft-i18n` and `craft-fonts` revisions, `rustc` and `trunk`. An xUnit test checks that
  this commit is the submodule's commit in git's index: moving the submodule without rebuilding
  (or the reverse) fails.
- Workflow `build-wordcraft.yml`: run by hand, it builds, pushes the build on a branch
  `wordcraft/build-<version>-<commit>`, runs the whole CI on that branch (`ci.yml` called, since a
  push made with the workflow token starts no workflow), then opens the PR. On a PR that touches
  the submodule, the scripts or this file, it only builds. GitHub shows the "Run workflow" button
  only once the file is on `main`: until then the build is made with the script, and the PR
  checks it. Opening the PR needs the "Allow GitHub Actions to create and approve pull requests"
  setting; without it, the workflow gives the link to open it.
- CI (and the two publishing workflows) now fetch only the Kazo OS submodule; the Store still
  fetches only that one.
- The build of this PR, made with Rust 1.100.0: same files as the official zip, same licences,
  same JavaScript module apart from generated suffixes. The `.wasm` is not byte-identical (Rust
  version, and credits refreshed by the upstream release). With an older Rust (1.97) the module
  imported a few more WebGPU and video functions: the workflow takes the current stable Rust, as the
  upstream release does.
- Tests on this build: 125 xUnit, 31 proxy tests, 12 Playwright journeys. The journey "insert a
  picture from the Kazo files" sometimes failed (on `word/base` too): it imported the picture
  before File Explorer had its view, and File Explorer then ignores the import. It now waits for
  the folder's first listing.
- Measurements for decision 8: `.wasm` 60,242,518 bytes (22.2 MB with gzip -9), JavaScript
  module 182 KB. Data inside the `.wasm`, from the source files: interface translations about
  4.6 MB (51 languages), spelling and hyphenation data about 3.0 MB, fonts about 1.1 MB (Noto Sans
  Arabic and Georgian; the other fonts are not in the web build). About 9 MB of data could
  therefore be loaded separately; the rest, about 51 MB, is code, which two wasm modules do not
  share.
- The ArtCraft name and logo stay: no change to WordCraft ships yet.
- Build here: about 6 minutes on 4 cores, no cache.

### Work item 5: Store (`word/5-store`)

- Icon without the ArtCraft logo, French and English listing with "based on WordCraft by the
  ArtCraft team" and the known limits (alpha, spelling in English only, Asian fonts),
  screenshots.
- `THIRD-PARTY-NOTICES.md` (WordCraft's NOTICE and ATTRIBUTION), licences in the package.
- Minimum OS version: the one of decision 10. Playwright journey in a published OS.
- Tag through the "Tag a release" workflow, catalog PR in Os-AppMarketPlace (signing only
  there), a row in Kazo OS's `features.md`.
- **Done when**: Kazo Word installs from the Store and opens a `.docx` on double-click.

Result (2026-11-13):

- Icon: `store/icon.png` is the app's own icon (`wwwroot/icons/app.svg`, a page and a pen)
  rendered at 256 × 256; it replaces the template's placeholder. No ArtCraft mark anywhere.
- Listing: `store/description.{en,fr}.md` and the short descriptions credit "WordCraft by the
  ArtCraft team" and list the known limits (alpha, English spelling, Asian fonts, WebGPU or
  WebGL 2, size and memory, printing, Kazo OS 3.3.2). Three screenshots in `store/screenshots/`,
  taken in the published OS by the journey below (WordCraft's own report and letter templates).
- `THIRD-PARTY-NOTICES.md` and `THIRD-PARTY-NOTICES-fr.md` at the root. The project links them
  and `LICENSE` into `wwwroot/legal/`, so the package carries them next to WordCraft's own
  `LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `ATTRIBUTION.md` and `OFL-*.txt`
  (`LegalFilesTests` checks the links, the files, the WordCraft version named in the notices,
  and the credit in the listing).
- Version 0.1.0 (decision 18), minimum Kazo OS 3.3.2 (decision 17).
- Journey `tests/installed/install-and-open.mjs` (`npm run test:installed -- <OS wwwroot>`):
  the published Kazo OS 3.3.2 (`kazoos-wasm-3.3.2.zip` of the release) served as a plain static
  host, `sudo kazopkg install Kazo-ca/Os-App-Word` answered from the local packages, a `.docx`
  written with Kazo Word, closed, then **double-clicked in the File Explorer: Kazo Word opens it**,
  and the license files answer from the installed package. Green on 2026-11-13, with no sign-in
  again after the install. With a third argument it also writes the Store screenshots.
- Found: on SwiftShader, a page screenshot does not show what WebGPU draws (the canvas comes out
  empty); the screenshot run leaves WebGPU off, so WordCraft draws with WebGL 2.
- Then: PR `word/base` → `main` (Benoit's decision), tag `v0.1.0` with "Tag a release", catalog
  PR in Os-AppMarketPlace, row in Kazo OS's `features.md`; the Store install is checked once the
  catalog is published.

### Order

Work item 1 first. Then 2, 3 and 4 in parallel, in three threads: 2 touches the C#, 3 touches
the proxy, 4 touches the build. Work item 5 last, after the Kazo OS fix.

## 8. Upstream track (decision 11)

Drafts in English in `/mnt/project-files/word/upstream/`, posted by Benoit under his name,
one issue per problem, with no mention of an agent.

| Topic | Where | When |
| --- | --- | --- |
| Confirm a save (`id`, `saved`) in WordCraft | Comment on craft-libs #611 | After work item 2, with our write-failure case. |
| File › Open asked from the host (`open-request`) | Comment on craft-libs #610 and wordcraft #4945 | After work item 3. |
| Keys reserved by the host | New issue on craft-libs | After work item 3. |
| Settings kept by the host | New issue on craft-libs | After the trial of work item 3. |

Each accepted proposal removes a workaround from the proxy.

## 9. Risks followed

| Risk | Answer |
| --- | --- |
| WordCraft changes fast (one version a week) | Pinned version; the proxy tests and the Playwright journeys say what a version upgrade breaks. |
| The Store analyses the app's JavaScript (CodeQL) | WordCraft's generated `.js` (180 KB of wasm-bindgen glue) is analysed by the catalog PR's CodeQL; its results are a summary in the review comment, not a gate. Read in work item 5 on the catalog PR. |
| Memory: each window loads its own WordCraft | Measured in work item 1: about 130 MB for the first window, 25 MB per extra window, without a document. To measure again with large documents in work item 2. |
| A window coming to the front reloads its iframe (Kazo OS moves its element in the page) | Decision 14: Kazo OS keeps a stable order of the windows in the page, stacking staying in `z-index` (separate thread of `Kazo-ca/Os`). Released in Kazo OS 3.3.1; the journey "the document survives when its window comes to the front" checks it. |
| Kazo OS refuses a file over 16 MiB (ADR 0094), the `.wasm` is 60 MB | Decision 17: Kazo OS 3.3.2 accepts 128 MiB per file (ADR 0245); Kazo Word installs in the published OS. |
| The `.wasm` is 61 MB, not 13 MB | Decision 15: decision 13 stays. |
| WordCraft settings shared by every Kazo user (same origin) | Work item 3 trial held: settings in `~/.config/ca.kazo.word/`. Added fonts (IndexedDB) stay shared. |
| No GPU | Clear error screen (`failed`). |
