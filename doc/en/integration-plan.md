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
  Pickers/PickPurpose.cs            document, picture, fonts, recipients (from accept)
  Strings.cs, Resources/*.resx      French and English texts, localization/fr.json
  wwwroot/host/index.html           iframe page: canvas data-host="page", loading
  wwwroot/host/word-proxy.js        the proxy
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
external/wordcraft                  submodule of the fork (work item 4 only)
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
| `init` | window → proxy | `author`, `theme`, `language`, `reservedKeys` | First message, with the port. |
| `ready` | proxy → window | `appVersion` | WordCraft is listening. |
| `failed` | proxy → window | `error` | WordCraft did not start (no WebGPU nor WebGL2, wasm not found). |
| `open` | window → proxy | `name`, `bytes` | Opens a document. |
| `save` | proxy → window | `name`, `saveAs`, `bytes` | Every file WordCraft would download. |
| `dirty` | proxy → window | `dirty` | The shown document has changes. |
| `pick` | proxy → window | `id`, `accept`, `multiple` | WordCraft opens the file picker. |
| `picked` | window → proxy | `id`, `files` or `cancelled` | Answer to the picker. |
| `command` | window → proxy | `id`, `method`, `params` | Calls `window.wordcraft.request`. |
| `result` | proxy → window | `id`, `ok`, `value` or `error` | Answer to `command`. |
| `key` | proxy → window | key and modifiers | An OS shortcut typed in the iframe. |
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
| Fix of the Kazo OS service worker (decision 10) | Thread "Servir la page iframe des apps installées" | PR [Os#451](https://github.com/Kazo-ca/Os/pull/451) (ADR 0243) ready on 2026-11-13, to merge. Nothing to change on the app side. Only blocks work item 5. |
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
  document is lost (question 14, section 9). `word-host.js` reconnects the reloaded page, but
  the document is gone.
- Published OS: to try once Os#451 is released (decision 10).

### Work item 2: open and save (`word/2-open-save`)

- File associations and "Open with" (formats of the plan), `OpenRequest`, intents.
- `DocumentSession`, `SaveDecision`, `DocumentFormats`; messages `open`, `save`, `dirty`.
- OS dialogs, title with asterisk, close guard with `ui.inspect`.
- xUnit tests: each row of the "Save" table, write failure, cancelled dialog, export, two
  documents in one page, title, closing. Playwright: open a `.docx` from the explorer, edit
  it, save it, reopen it; Save As; PDF export; close with changes; two windows.
- **Done when**: no case loses changes unless the user chose it, including when the write
  fails.

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

### Work item 5: Store (`word/5-store`)

- Icon without the ArtCraft logo, French and English listing with "based on WordCraft by the
  ArtCraft team" and the known limits (alpha, spelling in English only, Asian fonts),
  screenshots.
- `THIRD-PARTY-NOTICES.md` (WordCraft's NOTICE and ATTRIBUTION), licences in the package.
- Minimum OS version: the one of decision 10. Playwright journey in a published OS.
- Tag through the "Tag a release" workflow, catalog PR in Os-AppMarketPlace (signing only
  there), a row in Kazo OS's `features.md`.
- **Done when**: Kazo Word installs from the Store and opens a `.docx` on double-click.

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
| The Store analyses the app's JavaScript (CodeQL) | WordCraft's generated `.js` will be analysed: to check in work item 5, before the tag. |
| Memory: each window loads its own WordCraft | Measured in work item 1: about 130 MB for the first window, 25 MB per extra window, without a document. To measure again with large documents in work item 2. |
| A window coming to the front reloads its iframe (Kazo OS moves its element in the page) | Question 14 asked to Benoit on 2026-11-13; recommendation: Kazo OS keeps a stable order of the windows in the page, stacking staying in `z-index`. |
| The `.wasm` is 61 MB, not 13 MB | Question 15 asked to Benoit on 2026-11-13; recommendation: keep decision 13. |
| No GPU | Clear error screen (`failed`). |
