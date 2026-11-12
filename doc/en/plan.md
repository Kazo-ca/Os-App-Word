> [Version française](../fr/plan.md)

# Plan: the Kazo Word app for Kazo OS

Plan set with Benoit in the thread "Intégrer WordCraft dans Kazo OS"
(2026-11-11 and 2026-11-12). This document sets the goal, the decisions and the order
of the work; progress is at the end of the page. The plan was checked on 2026-11-12
against WordCraft's source code and its issues: see
[`plan-verification.md`](plan-verification.md).

## Goal

Kazo Word is the **word processor of Kazo OS**. It integrates
[WordCraft](https://github.com/storytold/wordcraft) (ArtCraft team, MIT or
Apache-2.0), a word processor written in Rust that already ships a WebAssembly
build: ribbon, styles, track changes, references, native `.docx`, PDF export. A
document is opened from the OS as on a real system: double-click in the file
explorer, "Open with", OS dialogs; it is saved in the user's files.

Nothing is rewritten: the Kazo OS app **wraps WordCraft's web build** and plugs
its "hosted" mode (`craft-web-host`) into the Kazo OS file system, dialogs and
file associations. It is the first app of an office suite; Kazo Sheet
(spreadsheet, GridCraft) will follow with the same method.

WordCraft is in **alpha** (about 60% ready for real work according to its
authors, version 0.18.1 on 2026-11-12). The first work items are also
integration tests.

## Decisions

| # | Topic | Decision |
| --- | --- | --- |
| 1 | Integration | One **iframe** per window, on a page of the app (`wwwroot/`), with a JavaScript **proxy** in that page. WordCraft runs there in `data-host="page"` mode and only talks to the proxy; the proxy relays to the Kazo window with `postMessage`. Decided 2026-11-11. |
| 2 | Source of the web build | **Built from source** (Benoit, 2026-11-12): we will need changes and want the code at hand. **For the first tests**, the official `wordcraft-web-<version>.zip` of the upstream release, pinned by version and SHA-256. A modified build must remove the ArtCraft name and logo (trademark licence). "Same result as the zip" means same behaviour, not the same bytes. See also the shared library (decision 8). |
| 3 | Repositories | One repository per app: `Kazo-ca/Os-App-Word` (created 2026-11-12 from Os-AppTemplate, the first tests happen there), later one for Kazo Sheet. Benoit, 2026-11-12. |
| 4 | WordCraft's File › Open | The proxy intercepts the browser's file picker and opens the Kazo OS dialog instead, like Webamp's `FilePromptInterceptor`. In parallel, an `open-request` message is proposed upstream (see "Upstream track"). Decided 2026-11-11. The picker serves four purposes (document, picture, fonts, mail-merge recipients): the proxy handles all of them. A document picked with File › Open opens in a new Kazo Word window, to keep one document per window (check of 2026-11-12). |
| 5 | Scope of v1 | Desktop only, **several windows** (one document per window). Benoit, 2026-11-12. |
| 6 | Names | "**Kazo Word**" (word processor, WordCraft) and "**Kazo Sheet**" (spreadsheet, GridCraft). Benoit, 2026-11-12. The Store listing says "based on WordCraft by the ArtCraft team", with no ArtCraft logo. |
| 7 | The word "Word" | "Word" is a Microsoft trademark for a word processor. Benoit keeps "Kazo Word" (answer to question 6, 2026-11-12). |
| 8 | Shared library | For the **runtime dependencies common** to both apps (they share a WebAssembly base: egui, wgpu, `craft-libs`, fonts, dictionaries). **Decided after the tests**, before Kazo Sheet is created (Benoit, 2026-11-12). To measure during the tests: two wasm modules share no Rust code at run time; what can be shared is data loaded separately (fonts, dictionaries, translations) and our C# and JavaScript side. |
| 9 | Where the upstream sources live | **Forks** `Kazo-ca/wordcraft`, `Kazo-ca/gridcraft` and `Kazo-ca/craft-libs` (Benoit, 2026-11-12), as git submodules, built by CI; the same forks serve the upstream PRs. Benoit creates the forks before work item 4. Check of 2026-11-12: only the WordCraft fork is a submodule; Cargo downloads `craft-libs` and the egui fork at the pinned revisions, and the build also reads `craft-i18n` and `craft-fonts` at their pinned revisions. The `Kazo-ca/craft-libs` fork serves the upstream PRs. |

## The integration

```
Kazo OS window (Blazor, C#)  ⇄  proxy (iframe page)  ⇄  WordCraft (.wasm, canvas)
        │
  user's files (KazoFile, OS dialogs)
```

- **The window (C#)** keeps the logic: which file is open, where it is saved,
  the title, the confirmation on close. It reads and writes with `KazoFile` and
  opens the OS dialogs.
- **The iframe page** (`wwwroot/host/index.html` and its separate `.js`
  script, never an inline script) holds WordCraft's canvas with
  `data-host="page"` and loads the upstream build.
- **The proxy** relays the `craft-web-host` messages:

| Message | Direction | What Kazo Word does |
| --- | --- | --- |
| `wordcraft:ready` | WordCraft → host | Sends the file to open, if any. |
| `wordcraft:open` `{ name, bytes }` | host → WordCraft | Bytes read by `KazoFile`. |
| `wordcraft:save` `{ name, saveAs, bytes }` | WordCraft → host | `saveAs=false`: rewrites the open file. `saveAs=true`: OS save dialog. Exports (PDF, PNG) also go through that dialog. |
| `wordcraft:saved` `{ id }` | host → WordCraft | Not used today: WordCraft sends `save` without an `id` and believes it is saved as soon as it is sent. The Kazo window keeps its own "modified" state and says so when a write fails. |
| `wordcraft:dirty` | WordCraft → host | Asterisk in the title, confirmation on close. Only speaks of the shown document: on close, the window also asks `ui.inspect` for the list of documents. |
| `wordcraft:failed` | WordCraft → host | Clear message in the window (WebGPU or WebGL2 is needed). |

- **The proxy also**: forwards to Kazo OS the shortcuts that belong to it (an
  iframe otherwise keeps every key); intercepts the browser's file picker
  (decision 4); calls `window.wordcraft` commands for the C# side (window menu,
  tests).
- **Author**: `?author=` gets the Kazo OS user's name, for comments and track
  changes.
- **Associated formats** (Kazo Word becomes the default app): `.docx .docm
  .dotx .dotm .doc .dot .odt .ott .fodt .rtf`. For `.md .txt .html .tex`, only
  "Open with": the OS editor stays the default app.
- **Capabilities**: `dialogs.file-open`, `dialogs.file-save`,
  `filesystem.user-home.read`, `filesystem.user-home.write`,
  `filesystem.private.read`, `filesystem.private.write`.
- **One point to settle in Kazo OS** (question 10 of the
  [check](plan-verification.md)). An installed app's `wwwroot/` files are served
  at `_content/<assembly>/` and `.wasm` goes out as `application/wasm`, but the
  service worker of the published OS answers a navigation (loading the iframe
  page) with the OS `index.html`. In development the page is served. Size is not
  a concern: about 13 MB of wasm (5 MB compressed) for limits of 64 MiB
  compressed and 128 MiB extracted.
- **The iframe** carries `allow="fullscreen; clipboard-read; clipboard-write"`.

## Work items

Series `word/base` (created from `main` once the plan is validated), one
`word/<n>-<topic>` branch and one PR to `word/base` per work item, then one
`word/base` → `main` PR that Benoit merges.

1. **Solution and iframe** (`word/1-solution`): rename the template's solution
   (`Kazo.Word`), manifest, window; fetch the pinned upstream web build
   (version + SHA-256) into `wwwroot/`; iframe page, loading screen, `failed`
   error; **measure the package size**; check that the page loads in the iframe
   in development then in a published OS, with WebGPU and with WebGL2, and that
   two windows run at once.
2. **Open and save** (`word/2-open-save`): file associations, open from the
   explorer and "Open with", `open`/`save`/`dirty`, OS dialogs, title,
   confirmation on close, several windows. The window keeps its own "modified"
   state (WordCraft does not know whether the save succeeded) and finds each
   document by its name.
3. **Full proxy** (`word/3-proxy`): OS shortcuts, file picker interception
   (document, picture, fonts, recipients), author, OS dark mode and language
   through the `ui.theme` and `ui.language` commands, window menu.
4. **Build from source** (`word/4-build-from-source`): submodule of the WordCraft fork, `wasm32` build with Trunk 0.21.14 in CI with `craft-i18n` and `craft-fonts` at the pinned revisions, same behaviour as the zip; ArtCraft marks removed as soon as a change is made; shared library (decisions 8 and 9).
5. **Store** (`word/5-store`): icon, listing, `THIRD-PARTY-NOTICES.md`
   (WordCraft's NOTICE and ATTRIBUTION), end-to-end tests, tag, catalog PR in
   Os-AppMarketPlace (signing happens only there).

Work items 2, 3 and 4 can run in parallel after 1.

## Upstream track

In parallel with the series, without blocking it: propose to `craft-libs`
(`craft-web-host`) what is missing, then drop from the proxy each workaround
that is no longer needed.

1. Confirmation of a save (`id` and `saved`) and a save asked for by the host:
   join [craft-libs #611](https://github.com/storytold/craft-libs/issues/611).
2. `open-request` message: File › Open asks the host for the file. To propose on
   [craft-libs #610](https://github.com/storytold/craft-libs/issues/610) and
   [wordcraft #4945](https://github.com/storytold/wordcraft/issues/4945), which
   handle the same problem by hiding the File tab.
3. Settings kept by the host: in each Kazo user's `~/.config` instead of the
   shared `localStorage`. Nothing upstream for the web.
4. Keys reserved by the host. Nothing upstream.

Removed after the check of 2026-11-12: host theme and language (the `ui.theme`
and `ui.language` commands already exist) and the canvas passed as a parameter
(not needed with the iframe). How to run this track is question 11 of the
[check](plan-verification.md).

WordCraft's `AGENTS.md` asks for an issue before each PR and refuses a PR
presented as written by an agent: Claude prepares the code and texts in a
fork, **Benoit opens the issues and PRs under his name** (agreed 2026-11-11).

## Tests

C# tests (xUnit) for opening and saving (`saveAs`, exports, file names), the
title and closing; `node --test` tests of the proxy with a fake WordCraft;
Playwright journeys in the host: open a `.docx` from the explorer, edit it,
save it, reopen it; Save As; two windows at once.

## Licences

- This repository: MIT.
- WordCraft: MIT or Apache-2.0. Its `NOTICE` and `ATTRIBUTION.md` (SIL OFL
  fonts, dictionaries) ship with the app.
- The ArtCraft name and logo are trademarks
  ([`LICENSE-brand.txt`](https://github.com/storytold/wordcraft/blob/main/docs/brand/LICENSE-brand.txt)):
  never in Kazo Word's icon, name or Store listing.

## Risks

- **Iframe page in the published OS**: not served today (question 10 of the
  [check](plan-verification.md)).
- **Save not confirmed** by WordCraft and **several documents in one page**:
  handled in the Kazo window in work item 2.
- **Spelling in English only** in the web version (question 12).
- **Web build size**: about 13 MB of wasm (5 MB compressed) according to
  upstream; measured in work item 1.
- **Alpha and fast pace** (about one release a week): pinned version, upgrades
  deliberate and tested.
- **Settings outside Kazo files** (`localStorage`, IndexedDB), shared by the
  Kazo users of the same browser, until point 3 of the upstream track.
- **GPU**: WebGPU or WebGL2 is needed.

## Later

- Kazo Sheet (GridCraft), same method, and the `handoff` between both apps.
- Mobile.

## Progress

| Work item | Branch | Depends on | State |
| --- | --- | --- | --- |
| Plan | `docs/word-plan` | nothing | Merged (PR [#1](https://github.com/Kazo-ca/Os-App-Word/pull/1)) |
| Check of the plan | `docs/word-plan-verification` | plan | PR_VERIF_EN |
| Kazo OS: iframe page of an installed app | to create in `Kazo-ca/Os` | question 10 | Waiting for the answer |
| 1. Solution and iframe | `word/1-solution` | plan | Waiting for Benoit's go |
| 2. Open and save | `word/2-open-save` | 1 | To do |
| 3. Full proxy | `word/3-proxy` | 1 | To do |
| 4. Build from source | `word/4-build-from-source` | 1, forks created | To do |
| 5. Store | `word/5-store` | 2, 3, 4 | To do |
| Upstream track | fork of `storytold/craft-libs` | nothing | To do |
