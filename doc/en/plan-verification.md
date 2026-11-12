> [Version française](../fr/plan-verification.md)

# Check of the Kazo Word plan (2026-11-12)

On 2026-11-12 Benoit asked for the [plan](plan.md) to be checked against WordCraft's source
code and the issues of its repository, before work item 1 starts. This page says what was
read, what is confirmed, what changes in the plan and which questions remain.

## What was read

| Source | Version read |
| --- | --- |
| [`storytold/wordcraft`](https://github.com/storytold/wordcraft) | `6bf96e65` (2026-11-12), version 0.18.1 |
| [`storytold/craft-libs`](https://github.com/storytold/craft-libs) (`craft-web-host`, `craft-web-api`) | `f92a91a` (2026-11-12); WordCraft pins `fd5e2fe` |
| [`storytold/craft-common-actions`](https://github.com/storytold/craft-common-actions) (release workflow) | `804f702` |
| [`storytold/egui`](https://github.com/storytold/egui) (egui fork, `eframe` web) | `6d518fc7`, the revision WordCraft pins |
| [`rfd`](https://github.com/PolyMeilex/rfd) (file dialogs) | 0.17.2, the version in `Cargo.lock` |
| Kazo OS | `24b57360` (version 3.3.0) |
| Upstream issues and PRs | Search of 2026-11-12 on `web`, `host`, `iframe`, `embed`, `craft-web-host` (list below) |

Not done: building WordCraft and downloading the release zip (the work container does not
allow it). The size comes from WordCraft's hosting note and will be measured in work item 1.

## Result

The plan holds: the iframe with a proxy, the `data-host="page"` mode, the messages and the
split into work items are confirmed by the code. Seven points change, one of them in Kazo OS.

## What is confirmed

| Assumption of the plan | Where it is checked | Result |
| --- | --- | --- |
| Messages `ready`, `open`, `save`, `dirty`, `failed`, `data-host="page"` mode | `apps/wordcraft-web/src/host.rs`, `crates/web-host` in craft-libs | Confirmed. The contract also has `host`, `saved`, `handoff`, `handoff-done`. |
| `?author=` names the author | `host.rs` (`set_author`, 200 characters at most) | Confirmed. |
| Commands for the C# side (`window.wordcraft`) | `apps/wordcraft-web/src/api.rs`, `docs/control-protocol.md` | Confirmed. `ui.theme` and `ui.language` already exist: the OS dark mode and language are set with nothing to ask upstream. `ui.inspect` lists the open documents with their "modified" state. |
| The proxy can take back the OS shortcuts | `crates/eframe/src/web/events.rs` in the egui fork | Confirmed. WordCraft listens for keys on the canvas: a capture-phase listener on the iframe page runs before it. |
| The proxy can intercept the file picker | `src/backend/wasm.rs` in `rfd` 0.17.2 | Confirmed. `rfd` creates a file field in an `rfd-overlay` block, listens for `change` and `cancel`, then calls `click()`. One capture-phase click listener is enough. |
| Package size | `packaging/web/README.md` | About 13 MB of wasm, 5 MB compressed: far from the 64 MiB and 128 MiB limits. No ADR needed. |
| Isolation headers | `service-worker.published.js` in Kazo OS | The service worker already adds COOP, COEP and CORP, which WordCraft's hosting note asks for. |
| Kazo OS side | SDK 3.3.0 | Open and save dialogs, file associations, `IWindowCloseGuard`, the user's display name, several windows per app: all exist. |

## What changes in the plan

1. **The iframe page is not served by the published Kazo OS.** Loading a page in an iframe is
   a "navigation". The published service worker answers every navigation with the OS
   `index.html` (`shouldServeIndexHtml`), except for the files of the release; an installed
   app's files are not among them. The iframe would start a second Kazo OS instead of
   WordCraft. In development the service worker does serve the page: the problem only shows
   once the OS is published. The plan said "nothing to change in Kazo OS": that is wrong on
   this point. See question 10.
2. **A save is not confirmed.** WordCraft sends `save` without an `id` and considers itself
   saved as soon as it is sent. If the write fails in Kazo OS, or the user cancels the save
   dialog, WordCraft still believes everything is saved. The Kazo window therefore keeps its
   own "modified" state and says so clearly on failure. Upstream knows:
   [craft-libs #611](https://github.com/storytold/craft-libs/issues/611).
3. **WordCraft web opens several documents in the same page.** File › New and File › Open add
   a document and WordCraft switches between them; `dirty` only speaks of the shown document
   and `save` names a document only by its name. To keep "one document per window"
   (decision 5): a document picked with File › Open opens in a **new Kazo Word window**, and
   on close the window asks `ui.inspect` whether any document has changes. The option that
   would hide the File tab (`?chrome=none`) does not exist in WordCraft yet
   ([#4945](https://github.com/storytold/wordcraft/issues/4945)).
4. **The file picker serves four purposes**, not only File › Open: document, picture, fonts
   (several files) and mail-merge recipients. The proxy reads the extensions the field asks
   for to choose the right Kazo dialog, then hands the files to the field.
5. **Building from source needs more than the WordCraft fork**: stable Rust with the
   `wasm32-unknown-unknown` target, Trunk 0.21.14, the `craft-i18n` repository at the revision
   in `craft-i18n.rev` (without it the interface stays in English) and the `craft-fonts`
   repository at the revision of the release workflow. `craft-libs` (four different revisions)
   and the egui fork are downloaded by Cargo: no submodule needed for them. The
   `Kazo-ca/craft-libs` fork only serves to prepare upstream PRs. "Same result as the zip"
   means same behaviour, not the same bytes.
6. **The upstream track overlaps issues that already exist** (table below). We join them
   instead of opening five new issues. The repository's bot asks for one issue per problem.
   The repository is very active (about 1,500 issues, more than 300 open PRs): nothing in the
   plan may wait for an upstream merge.
7. **Limits of the web version to know**: the spell checker only works in English
   ([#6140](https://github.com/storytold/wordcraft/issues/6140), see question 12); only small
   fonts are included, Chinese and Japanese display badly
   ([#5621](https://github.com/storytold/wordcraft/issues/5621)); a DOCX, ODT or RTF export
   wrongly clears the "modified" state
   ([#6131](https://github.com/storytold/wordcraft/issues/6131), fix proposed in PR #6446).

Details for work item 1: the official zip has its own `index.html` without `data-host`; we
ship our page and find the hashed names at build time (`wordcraft-web-<hash>.js`,
`wordcraft-web-<hash>_bg.wasm`); the proxy's listeners are added before the call to `init`;
the iframe carries `allow="fullscreen; clipboard-read; clipboard-write"`.

## Upstream issues and PRs that concern us

| Our need | Upstream | State on 2026-11-12 |
| --- | --- | --- |
| Confirm a save (`id`, `saved`), ask for a save from the host | [craft-libs #611](https://github.com/storytold/craft-libs/issues/611), part 1 | Open, nobody on it. |
| AutoSave to the host | craft-libs #611 part 2, [craft-libs PR #713](https://github.com/storytold/craft-libs/pull/713), wordcraft PR #6015 (draft) | In progress by a contributor. |
| File › Open in host mode | [wordcraft #4945](https://github.com/storytold/wordcraft/issues/4945), [craft-libs #610](https://github.com/storytold/craft-libs/issues/610) (`?chrome=none` hides the File tab) | Open, no PR. Our `open-request` message is another answer to the same problem. |
| Host theme | [wordcraft #6393](https://github.com/storytold/wordcraft/issues/6393), item 3 | Open question. `ui.theme` is enough for us. |
| Read-only decided by the host | [craft-libs #609](https://github.com/storytold/craft-libs/issues/609), wordcraft #2229 and PR #2272 | Open. Useful later for protected files. |
| Spelling beyond English | [wordcraft #6140](https://github.com/storytold/wordcraft/issues/6140), craft-libs #745 | Open, no PR. |
| Shared host contract | [craft-libs #358](https://github.com/storytold/craft-libs/issues/358) | Shipped (PRs #420 and #413), tracking issue open. |
| Settings kept by the host | Nothing for the web (craft-libs #12 covers the desktop) | To propose. |
| Keys reserved by the host | Nothing found | To propose. |
| Canvas passed as a parameter | Nothing found | Not needed with the iframe: removed from the track. |

No effect on us: [wordcraft #4630](https://github.com/storytold/wordcraft/issues/4630) and
#4591 (iframe from another origin, printing blocked); our iframe is same-origin.

## Open questions

Asked to Benoit in the thread on 2026-11-12; ✨ marks the recommendation.

**10. How to serve the iframe page in the published Kazo OS?**

- **A. Fix Kazo OS ✨**: the service worker serves an installed app's files for a navigation
  under `_content/` too. A small PR in `Kazo-ca/Os` with an ADR; Kazo Word then requires that
  OS version. The page has a real address (`?author=` works) and any future app can have an
  iframe page. The tests of work item 1 do not wait: in development the page is already
  served.
- **B. Work around it in the app**: read the page with `fetch` and inject it into the iframe
  (`srcdoc`). Nothing to change in the OS, works on 3.3.0. But the page has no address any
  more: no `?author=` nor `?webgl`, and the behaviour varies by browser.
- **C. Both**: B right away, A in parallel, then B removed. More work, for a problem that
  does not touch the tests in development.

**11. How to run the upstream track, now that we know issues exist?**

- **A. Join the existing issues ✨**: a comment on craft-libs #611 (save confirmation) and on
  #610 (File › Open), a new issue only for what is missing (reserved keys, settings kept by
  the host), one issue per problem. Claude prepares the texts, Benoit posts them.
- **B. Open our five issues as planned**: simpler for us to follow, but the repository's bot
  will mark them as duplicates and the maintainers will have to link them.
- **C. Propose nothing upstream before v1**: no time spent now; the proxy keeps all its
  workarounds longer.

**12. The spell checker only works in English in the web version. What do we do for v1?**

- **A. Accept it for v1 ✨**: noted as a known limit in the Store listing, we follow
  wordcraft #6140.
- **B. Add it ourselves in work item 4**: change WordCraft and `craft-proof` to load the
  French dictionary. That is a modified build (ArtCraft marks to remove) and several days of
  work to redo at each version upgrade as long as upstream does not have it.
- **C. Wait for upstream before publishing in the Store**: v1 depends on a schedule we do not
  control.

## Recommended order

1. Decide question 10. If A: a PR in `Kazo-ca/Os`, apart from the `word/base` series.
2. Work item 1 as a trial with criteria: the page loads in the iframe (in development, then
   in a published OS), WordCraft starts under COEP with WebGPU and with WebGL2, `ready`
   arrives, two windows run at once, the package is measured.
3. Work items 2, 3 and 4 in parallel, with the corrections above.
4. Upstream track according to question 11, blocking nothing.
