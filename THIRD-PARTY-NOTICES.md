# Third-party notices

> [Version française](THIRD-PARTY-NOTICES-fr.md)

Kazo Word is under the MIT license ([LICENSE](LICENSE)). Its C# code, its iframe proxy
(`src/Kazo.Word/wwwroot/host/`), its OS-side module (`src/Kazo.Word/wwwroot/js/`) and its icon
(`src/Kazo.Word/wwwroot/icons/app.svg`, `store/icon.png`) are this repository's own work.

It bundles one third-party work, **WordCraft**, which brings its own third-party material.

## WordCraft

| | |
| --- | --- |
| Work | [WordCraft](https://github.com/storytold/wordcraft), a word processor written in Rust, by the ArtCraft team and the WordCraft contributors |
| Version | 0.18.1, compiled from our fork [`Kazo-ca/wordcraft`](https://github.com/Kazo-ca/wordcraft) (branch `kazo/v0.18.1`, the upstream tag, unmodified) |
| License | MIT or Apache-2.0, at the user's option |
| Files | `src/Kazo.Word/wwwroot/wordcraft/` (the web build: `.js`, `.wasm`, `snippets/`) |
| Provenance | `src/Kazo.Word/wwwroot/wordcraft/wordcraft.json`: the fork's commit, the craft-i18n and craft-fonts revisions, the compilers, and the SHA-256 of every file |

Kazo Word does not change WordCraft: it shows its web build in an iframe and talks to it through
the events and the page API WordCraft publishes.

WordCraft's own notices ship **unmodified** next to its build, and in the installed package
(`_content/Kazo.Word/wordcraft/`):

| File | Content |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | WordCraft's licenses |
| `NOTICE` | WordCraft's copyright, its trademark notice, and the third-party material it bundles: fonts (Source Sans 3, Source Serif 4, Inter, JetBrains Mono, and the craft-fonts families), language data (English word list, SCOWL spelling dictionaries, hyphenation patterns), and the egui icon fonts |
| `ATTRIBUTION.md` | WordCraft's per-file list of assets (author, source, license) |
| `OFL-*.txt` | The SIL Open Font License 1.1 texts of the fonts the build embeds (BIZ UDMincho, BIZ UDPGothic, LXGW WenKai Lite, Nanum Gothic, Noto Sans Arabic, CJK SC, Devanagari, Georgian, Hebrew, Symbols, Symbols 2, Thai, Shippori Mincho) |

### The ArtCraft brand

The ArtCraft name, wordmark and logos are trademarks of the ArtCraft team; WordCraft's
open-source license does not cover them. Kazo Word uses none of them: its name, its icon and its
Store listing are this repository's own. "Based on WordCraft by the ArtCraft team" only says where
the software comes from.

## In the package

The Kazo OS package (`.kazoapp`) carries, under `wwwroot/`:

- `legal/LICENSE.txt`: Kazo Word's MIT license (this repository's `LICENSE`);
- `legal/THIRD-PARTY-NOTICES.md` and `legal/THIRD-PARTY-NOTICES-fr.md`: this file and its French
  version;
- `wordcraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `ATTRIBUTION.md`, `OFL-*.txt`: WordCraft's
  notices, as above.

A C# test (`LegalFilesTests`) checks that the project puts these files in the package.

## Test data

The end-to-end tests make their own documents with Kazo Word; they ship no third-party file.
