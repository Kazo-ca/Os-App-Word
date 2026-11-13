# Avis de tiers

> [English version](THIRD-PARTY-NOTICES.md)

Kazo Word est sous licence MIT ([LICENSE](LICENSE)). Son code C#, son proxy d'iframe
(`src/Kazo.Word/wwwroot/host/`), son module côté OS (`src/Kazo.Word/wwwroot/js/`) et son icône
(`src/Kazo.Word/wwwroot/icons/app.svg`, `store/icon.png`) sont l'œuvre de ce dépôt.

Il embarque une seule œuvre tierce, **WordCraft**, qui apporte ses propres éléments tiers.

## WordCraft

| | |
| --- | --- |
| Œuvre | [WordCraft](https://github.com/storytold/wordcraft), un traitement de texte écrit en Rust, de l'équipe ArtCraft et des contributeurs de WordCraft |
| Version | 0.18.1, compilée depuis notre fork [`Kazo-ca/wordcraft`](https://github.com/Kazo-ca/wordcraft) (branche `kazo/v0.18.1`, l'étiquette amont, sans modification) |
| Licence | MIT ou Apache-2.0, au choix de l'utilisateur |
| Fichiers | `src/Kazo.Word/wwwroot/wordcraft/` (la version web : `.js`, `.wasm`, `snippets/`) |
| Provenance | `src/Kazo.Word/wwwroot/wordcraft/wordcraft.json` : le commit du fork, les révisions de craft-i18n et craft-fonts, les compilateurs et le SHA-256 de chaque fichier |

Kazo Word ne modifie pas WordCraft : il montre sa version web dans une iframe et lui parle par les
événements et l'API de page que WordCraft publie.

Les avis de WordCraft sont livrés **sans modification** à côté de sa version web, et dans le paquet
installé (`_content/Kazo.Word/wordcraft/`) :

| Fichier | Contenu |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | Les licences de WordCraft |
| `NOTICE` | Le droit d'auteur de WordCraft, son avis sur les marques, et les éléments tiers qu'il embarque : polices (Source Sans 3, Source Serif 4, Inter, JetBrains Mono, et les familles de craft-fonts), données de langue (liste de mots anglaise, dictionnaires d'orthographe SCOWL, motifs de césure) et les polices d'icônes d'egui |
| `ATTRIBUTION.md` | La liste, fichier par fichier, des ressources de WordCraft (auteur, source, licence) |
| `OFL-*.txt` | Les textes de la SIL Open Font License 1.1 des polices embarquées (BIZ UDMincho, BIZ UDPGothic, LXGW WenKai Lite, Nanum Gothic, Noto Sans Arabic, CJK SC, Devanagari, Georgian, Hebrew, Symbols, Symbols 2, Thai, Shippori Mincho) |

### La marque ArtCraft

Le nom, le logotype et les logos ArtCraft sont des marques de l'équipe ArtCraft ; la licence libre
de WordCraft ne les couvre pas. Kazo Word n'en utilise aucun : son nom, son icône et sa fiche du
Store sont l'œuvre de ce dépôt. « Basé sur WordCraft de l'équipe ArtCraft » dit seulement d'où vient
le logiciel.

## Dans le paquet

Le paquet Kazo OS (`.kazoapp`) contient, sous `wwwroot/` :

- `legal/LICENSE.txt` : la licence MIT de Kazo Word (le `LICENSE` de ce dépôt) ;
- `legal/THIRD-PARTY-NOTICES.md` et `legal/THIRD-PARTY-NOTICES-fr.md` : ce fichier et sa version
  anglaise ;
- `wordcraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `ATTRIBUTION.md`, `OFL-*.txt` : les avis de
  WordCraft, comme ci-dessus.

Un test C# (`LegalFilesTests`) vérifie que le projet met ces fichiers dans le paquet.

## Données de test

Les tests de bout en bout fabriquent leurs propres documents avec Kazo Word ; ils ne livrent aucun
fichier tiers.
