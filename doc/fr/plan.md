> [English version](../en/plan.md)

# Plan : l'app Kazo Word pour Kazo OS

Plan **en discussion** (fil « Intégrer WordCraft dans Kazo OS », 2026-11-11 et
2026-11-12). Les décisions marquées « en attente » prennent la recommandation
tant que Benoit n'a pas répondu. Ce document fixe le but, les décisions et
l'ordre des choses à faire ; l'avancement est en fin de page.

## But

Kazo Word est le **traitement de texte de Kazo OS**. Il intègre
[WordCraft](https://github.com/storytold/wordcraft) (équipe ArtCraft, MIT ou
Apache-2.0), un traitement de texte écrit en Rust qui publie déjà une version
WebAssembly : ruban, styles, suivi des modifications, références, `.docx` natif,
export PDF. On ouvre un document depuis l'OS comme sur un vrai système :
double-clic dans l'explorateur, « Ouvrir avec », dialogues de l'OS ; on
l'enregistre dans les fichiers de l'utilisateur.

Rien n'est réécrit : l'app Kazo OS **emballe le build web de WordCraft** et
branche son mode « hébergé » (`craft-web-host`) sur le système de fichiers, les
dialogues et les associations de fichiers de Kazo OS. C'est la première app
d'une suite bureautique ; GridCraft (tableur) suivra avec la même méthode.

WordCraft est en **alpha** (environ 60 % prêt pour le vrai travail selon ses
auteurs, version 0.18 le 2026-11-11). Les premiers chantiers servent aussi de
tests d'intégration.

## Décisions

| # | Sujet | Décision |
| --- | --- | --- |
| 1 | Intégration | Une **iframe** par fenêtre, sur une page de l'app (`wwwroot/`), avec un **proxy** JavaScript dans cette page. WordCraft y tourne en mode `data-host="page"` et ne parle qu'au proxy ; le proxy relaie vers la fenêtre Kazo par `postMessage`. Décidé le 2026-11-11. |
| 2 | Provenance du build web | *En attente.* Recommandation : le zip officiel `wordcraft-web-<version>.zip` de la release amont, **sans modification**, épinglé par version et SHA-256. Pas de Rust dans la CI, et un build intact évite la contrainte de la marque ArtCraft (une version modifiée doit retirer le nom et le logo ArtCraft). |
| 3 | Dépôts | Un dépôt par app : `Kazo-ca/Os-App-Word` (créé le 2026-11-12 depuis Os-AppTemplate), plus tard un dépôt pour GridCraft. Le proxy est copié ; il devient un paquet partagé si une troisième app Craft arrive. |
| 4 | Fichier › Ouvrir de WordCraft | Le proxy intercepte le sélecteur de fichiers du navigateur et ouvre le dialogue de Kazo OS à la place, comme `FilePromptInterceptor` de Webamp. En parallèle, un message `open-request` est proposé en amont (voir « Piste en amont »). Décidé le 2026-11-11. |
| 5 | Portée de la v1 | *En attente.* Recommandation : ordinateur seulement, **plusieurs fenêtres** (un document par fenêtre). |
| 6 | Nom | Un nom Kazo, « Kazo Word » (Benoit, 2026-11-12). La fiche du Store dit « basé sur WordCraft de l'équipe ArtCraft », sans logo ArtCraft. |
| 7 | Le mot « Word » | *En attente.* « Word » est une marque de Microsoft pour un traitement de texte. Recommandation : garder le dépôt `Os-App-Word`, mais choisir un autre nom affiché avant la publication au Store. |

## L'intégration

```
Fenêtre Kazo OS (Blazor, C#)  ⇄  proxy (page de l'iframe)  ⇄  WordCraft (.wasm, canvas)
        │
  fichiers de l'utilisateur (KazoFile, dialogues de l'OS)
```

- **La fenêtre (C#)** garde la logique : quel fichier est ouvert, où il est
  enregistré, le titre, la confirmation à la fermeture. Elle lit et écrit avec
  `KazoFile` et ouvre les dialogues de l'OS.
- **La page de l'iframe** (`wwwroot/host/index.html` et son script `.js` à
  part, jamais de script en ligne) contient le canvas de WordCraft avec
  `data-host="page"` et charge le build amont.
- **Le proxy** relaie les messages de `craft-web-host` :

| Message | Sens | Ce que fait Kazo Word |
| --- | --- | --- |
| `wordcraft:ready` | WordCraft → hôte | Envoie le fichier à ouvrir, s'il y en a un. |
| `wordcraft:open` `{ name, bytes }` | hôte → WordCraft | Octets lus par `KazoFile`. |
| `wordcraft:save` `{ name, saveAs, bytes }` | WordCraft → hôte | `saveAs=false` : réécrit le fichier ouvert. `saveAs=true` : dialogue d'enregistrement de l'OS. Les exports (PDF, PNG) passent aussi par ce dialogue. |
| `wordcraft:dirty` | WordCraft → hôte | Astérisque dans le titre, confirmation à la fermeture. |
| `wordcraft:failed` | WordCraft → hôte | Message clair dans la fenêtre (il faut WebGPU ou WebGL2). |

- **Le proxy fait aussi** : renvoyer à Kazo OS les raccourcis qui lui
  appartiennent (une iframe garde sinon toutes les touches) ; intercepter le
  sélecteur de fichiers du navigateur (décision 4) ; appeler les commandes de
  `window.wordcraft` pour le C# (menu de la fenêtre, tests).
- **Auteur** : `?author=` reçoit le nom de l'utilisateur Kazo OS, pour les
  commentaires et le suivi des modifications.
- **Formats associés** (Kazo Word devient l'app par défaut) : `.docx .docm
  .dotx .dotm .doc .dot .odt .ott .fodt .rtf`. Pour `.md .txt .html .tex`,
  seulement « Ouvrir avec » : l'éditeur de l'OS reste l'app par défaut.
- **Capacités** : `dialogs.file-open`, `dialogs.file-save`,
  `filesystem.user-home.read`, `filesystem.user-home.write`,
  `filesystem.private.read`, `filesystem.private.write`.
- **Rien à changer dans Kazo OS au départ.** Les fichiers `wwwroot/` d'une app
  installée sont servis à `_content/<assembly>/` et `.wasm` part en
  `application/wasm`. Un paquet accepte 64 Mio compressés et 128 Mio
  décompressés : si le build web les dépasse, il faudra une ADR dans Kazo OS
  avant le chantier 4.

## Chantiers

Série `word/base` (créée depuis `main` quand le plan est validé), une branche
`word/<n>-<sujet>` et une PR vers `word/base` par chantier, puis une PR
`word/base` → `main` que Benoit fusionne.

1. **Solution et iframe** (`word/1-solution`) : renommer la solution du modèle
   (`Kazo.Word`), manifeste, fenêtre ; récupérer le build web amont épinglé
   (version + SHA-256) dans `wwwroot/` ; page de l'iframe, écran de chargement,
   erreur `failed` ; **mesurer la taille** du paquet.
2. **Ouvrir et enregistrer** (`word/2-open-save`) : associations de fichiers,
   ouvrir par l'explorateur et « Ouvrir avec », `open`/`save`/`dirty`,
   dialogues de l'OS, titre, confirmation à la fermeture, plusieurs fenêtres.
3. **Proxy complet** (`word/3-proxy`) : raccourcis de l'OS, interception du
   sélecteur de fichiers, auteur, mode sombre et langue de l'OS si WordCraft
   l'accepte par ses commandes, menu de la fenêtre.
4. **Store** (`word/4-store`) : icône, fiche, `THIRD-PARTY-NOTICES.md` (NOTICE
   et ATTRIBUTION de WordCraft), tests de bout en bout, tag, PR au catalogue
   d'Os-AppMarketPlace (la signature se fait seulement là).

Les chantiers 2 et 3 peuvent avancer en parallèle après le 1.

## Piste en amont

En parallèle de la série, sans la bloquer : proposer à `craft-libs`
(`craft-web-host`) ce qui manque, puis retirer du proxy chaque contournement
devenu inutile.

1. Message `open-request` : Fichier › Ouvrir demande le fichier à l'hôte.
2. Réglages confiés à l'hôte : dans `~/.config` de chaque utilisateur Kazo au
   lieu du `localStorage` partagé.
3. Thème et langue de l'hôte dans le message `host`.
4. Touches réservées par l'hôte.
5. Canvas passé en paramètre plutôt qu'un id fixe.

Le `AGENTS.md` de WordCraft demande une issue avant chaque PR et refuse une PR
présentée comme écrite par un agent : Claude prépare le code et les textes dans
un fork, **Benoit ouvre les issues et les PR en son nom** (accord du
2026-11-11).

## Tests

Tests C# (xUnit) pour l'ouverture et l'enregistrement (`saveAs`, exports, noms
de fichiers), le titre et la fermeture ; tests `node --test` du proxy avec un
faux WordCraft ; parcours Playwright dans l'hôte : ouvrir un `.docx` depuis
l'explorateur, le modifier, l'enregistrer, le rouvrir ; Enregistrer sous ;
deux fenêtres en même temps.

## Licences

- Ce dépôt : MIT.
- WordCraft : MIT ou Apache-2.0. Son `NOTICE` et son `ATTRIBUTION.md` (polices
  SIL OFL, dictionnaires) sont livrés avec l'app.
- Le nom et le logo ArtCraft sont des marques
  ([`LICENSE-brand.txt`](https://github.com/storytold/wordcraft/blob/main/docs/brand/LICENSE-brand.txt)) :
  jamais dans l'icône, le nom ou la fiche du Store de Kazo Word.

## Risques

- **Taille du build web** non mesurée : mesurée au chantier 1.
- **Alpha et rythme rapide** (une version par semaine environ) : version
  épinglée, montée de version volontaire et testée.
- **Réglages hors des fichiers Kazo** (`localStorage`, IndexedDB), partagés par
  les utilisateurs Kazo du même navigateur, jusqu'au point 2 de la piste en
  amont.
- **GPU** : il faut WebGPU ou WebGL2.

## Plus tard

- GridCraft (tableur), même méthode, et le `handoff` entre les deux apps.
- Mobile.

## Avancement

| Chantier | Branche | Dépend de | État |
| --- | --- | --- | --- |
| Plan | `docs/word-plan` | réponses 2, 5, 7 | En cours |
| 1. Solution et iframe | `word/1-solution` | plan | À faire |
| 2. Ouvrir et enregistrer | `word/2-open-save` | 1 | À faire |
| 3. Proxy complet | `word/3-proxy` | 1 | À faire |
| 4. Store | `word/4-store` | 2, 3 | À faire |
| Piste en amont | fork de `storytold/craft-libs` | rien | À faire |
