# Kazo Word

Un traitement de texte pour Kazo OS, pour les documents `.docx` et `.odt`. Kazo Word est basé sur
[WordCraft](https://github.com/storytold/wordcraft) de l'équipe ArtCraft, un traitement de texte
libre écrit en Rust, qui tourne ici dans une fenêtre de Kazo OS.

## Écrire

- Des pages mises en forme comme à l'impression : styles, polices, paragraphes, listes, tableaux,
  images, en-têtes et pieds de page, numéros de page, notes de bas de page, table des matières.
- Le ruban de WordCraft, sa recherche de commandes et ses raccourcis ; orthographe anglaise vérifiée
  pendant la frappe.
- Plusieurs fenêtres à la fois, un document par fenêtre.

## Vos fichiers Kazo OS

- Double-cliquez sur un fichier `.docx`, `.odt`, `.rtf`, `.doc`, `.md`, `.html` ou `.txt` dans
  l'Explorateur de fichiers, ou utilisez « Ouvrir avec » : le document s'ouvre dans Kazo Word.
- Enregistrer réécrit le fichier ouvert ; Enregistrer sous et le premier enregistrement d'un
  nouveau document passent par la boîte d'enregistrement de Kazo OS. Les exports PDF, PNG, texte
  et Markdown sont des copies : le document reste lié à son fichier.
- La barre de titre affiche `*` tant que le document a des changements non enregistrés, et fermer
  la fenêtre demande avant de les perdre.
- Fichier › Ouvrir, et les images, polices et listes de publipostage choisies dans WordCraft,
  viennent des fichiers de Kazo OS, par la boîte de dialogue de Kazo OS.
- Kazo Word suit le thème (clair ou sombre) et la langue de l'OS ; ses réglages sont gardés par
  utilisateur de Kazo OS dans `~/.config/ca.kazo.word/`.

## Limites connues

- **WordCraft est en alpha.** La plupart des documents s'ouvrent et s'enregistrent bien, mais
  gardez une copie des documents importants.
- **L'orthographe est vérifiée en anglais seulement** dans la version web de WordCraft.
- **Polices asiatiques** : seules de petites polices sont incluses ; le chinois, le japonais et le
  coréen peuvent mal s'afficher.
- WordCraft dessine avec **WebGPU ou WebGL 2** : un navigateur ou un ordinateur qui n'a ni l'un ni
  l'autre affiche un écran d'erreur au lieu du document.
- Le téléchargement est gros (environ 23 Mo, 61 Mo une fois installé) et chaque fenêtre charge sa
  propre copie de WordCraft (environ 130 Mo de mémoire pour la première fenêtre).
- L'impression passe par la boîte d'impression du navigateur.
- Demande Kazo OS 3.3.2 ou plus récent.

## Licence

Kazo Word est un logiciel libre sous licence MIT. WordCraft est sous licence MIT ou Apache-2.0 ; ses
avis et les licences des polices et dictionnaires qu'il inclut sont livrés avec l'app (voir
`THIRD-PARTY-NOTICES-fr.md` dans le dépôt). Le nom et les logos ArtCraft sont des marques de
l'équipe ArtCraft et ne sont pas utilisés par Kazo Word.
