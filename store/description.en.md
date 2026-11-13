# Kazo Word

A word processor for Kazo OS, for `.docx` and `.odt` documents. Kazo Word is based on
[WordCraft](https://github.com/storytold/wordcraft) by the ArtCraft team, an open-source word
processor written in Rust, which runs here in a Kazo OS window.

## Write

- Pages laid out as they print: styles, fonts, paragraphs, lists, tables, pictures, headers and
  footers, page numbers, footnotes, a table of contents.
- WordCraft's ribbon, its command search and its shortcuts; English spelling checked as you type.
- Several windows at once, one document per window.

## Your Kazo OS files

- Double-click a `.docx`, `.odt`, `.rtf`, `.doc`, `.md`, `.html` or `.txt` file in the File
  Explorer, or use "Open with": the document opens in Kazo Word.
- Save rewrites the file you opened; Save As and the first save of a new document use the Kazo OS
  save dialog. Exports to PDF, PNG, text and Markdown are copies: the document stays bound to its
  file.
- The title bar shows `*` while the document has unsaved changes, and closing the window asks
  before losing them.
- File › Open and the pictures, fonts and mail-merge lists you pick in WordCraft come from Kazo OS
  files, through the Kazo OS dialog.
- Kazo Word follows the OS theme (light or dark) and language; its settings are kept per Kazo OS
  user in `~/.config/ca.kazo.word/`.

## Known limits

- **WordCraft is in alpha.** Most documents open and save well, but keep a copy of the important
  ones.
- **Spelling is checked in English only** in the web version of WordCraft.
- **Asian fonts**: only small fonts are included; Chinese, Japanese and Korean text may look wrong.
- WordCraft draws with **WebGPU or WebGL 2**: a browser or computer with neither shows an error
  screen instead of the document.
- The download is large (about 23 MB, 61 MB once installed) and each window loads its own copy of
  WordCraft (about 130 MB of memory for the first window).
- Printing goes through the browser's print dialog.
- Needs Kazo OS 3.3.2 or later.

## License

Kazo Word is free software under the MIT license. WordCraft is under the MIT or Apache-2.0 license;
its notices and the licenses of the fonts and dictionaries it includes ship with the app (see
`THIRD-PARTY-NOTICES.md` in the repository). The ArtCraft name and logos are trademarks of the
ArtCraft team and are not used by Kazo Word.
