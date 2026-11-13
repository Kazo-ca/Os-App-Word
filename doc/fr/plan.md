> [English version](../en/plan.md)

# Plan : l'app Kazo Word pour Kazo OS

Plan établi avec Benoit dans le fil « Intégrer WordCraft dans Kazo OS »
(2026-11-11 et 2026-11-12). Ce document fixe le but, les décisions et
l'ordre des choses à faire ; l'avancement est en fin de page. Le plan a été
contre-vérifié le 2026-11-12 sur le code source de WordCraft et ses issues : voir
[`plan-verification.md`](plan-verification.md). Le détail de la réalisation (fichiers,
contrats, tests, critères de fin de chaque chantier) est dans
[`integration-plan.md`](integration-plan.md).

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
d'une suite bureautique ; Kazo Sheet (tableur, GridCraft) suivra avec la même méthode.

WordCraft est en **alpha** (environ 60 % prêt pour le vrai travail selon ses
auteurs, version 0.18.1 le 2026-11-12). Les premiers chantiers servent aussi de
tests d'intégration.

## Décisions

| # | Sujet | Décision |
| --- | --- | --- |
| 1 | Intégration | Une **iframe** par fenêtre, sur une page de l'app (`wwwroot/`), avec un **proxy** JavaScript dans cette page. WordCraft y tourne en mode `data-host="page"` et ne parle qu'au proxy ; le proxy relaie vers la fenêtre Kazo par `postMessage`. Décidé le 2026-11-11. |
| 2 | Provenance du build web | **Compilé depuis les sources** (Benoit, 2026-11-12) : on aura besoin de modifications et d'avoir le code à portée de main. **Pour les premiers tests**, le zip officiel `wordcraft-web-<version>.zip` de la release amont, épinglé par version et SHA-256. Un build modifié doit retirer le nom et le logo ArtCraft (licence de marque). « Même résultat que le zip » veut dire même comportement, pas les mêmes octets. Voir aussi la bibliothèque partagée (décision 8). |
| 3 | Dépôts | Un dépôt par app : `Kazo-ca/Os-App-Word` (créé le 2026-11-12 depuis Os-AppTemplate, les premiers tests s'y font), plus tard un dépôt pour Kazo Sheet. Benoit, 2026-11-12. |
| 4 | Fichier › Ouvrir de WordCraft | Le proxy intercepte le sélecteur de fichiers du navigateur et ouvre le dialogue de Kazo OS à la place, comme `FilePromptInterceptor` de Webamp. En parallèle, un message `open-request` est proposé en amont (voir « Piste en amont »). Décidé le 2026-11-11. Le sélecteur sert à quatre choses (document, image, polices, destinataires du publipostage) : le proxy les traite toutes. Un document choisi par Fichier › Ouvrir s'ouvre dans une nouvelle fenêtre Kazo Word, pour garder un document par fenêtre (vérification du 2026-11-12). |
| 5 | Portée de la v1 | Ordinateur seulement, **plusieurs fenêtres** (un document par fenêtre). Benoit, 2026-11-12. |
| 6 | Noms | « **Kazo Word** » (traitement de texte, WordCraft) et « **Kazo Sheet** » (tableur, GridCraft). Benoit, 2026-11-12. La fiche du Store dit « basé sur WordCraft de l'équipe ArtCraft », sans logo ArtCraft. |
| 7 | Le mot « Word » | « Word » est une marque de Microsoft pour un traitement de texte. Benoit garde « Kazo Word » (réponse à la question 6, 2026-11-12). |
| 8 | Bibliothèque partagée | Pour les **dépendances d'exécution communes** aux deux apps (elles partagent une base WebAssembly : egui, wgpu, `craft-libs`, polices, dictionnaires). **Décidé après les tests**, avant la création de Kazo Sheet (Benoit, 2026-11-12). À mesurer pendant les tests : deux modules wasm ne partagent pas de code Rust à l'exécution ; ce qui peut se partager, ce sont les données chargées à part (polices, dictionnaires, traductions) et notre côté C# et JavaScript. |
| 9 | Où vivent les sources amont | Des **forks** `Kazo-ca/wordcraft`, `Kazo-ca/gridcraft` et `Kazo-ca/craft-libs` (Benoit, 2026-11-12), en sous-modules git, compilés par la CI ; les mêmes forks servent aux PR en amont. Benoit crée les forks avant le chantier 4. Vérification du 2026-11-12 : seul le fork de WordCraft est un sous-module ; Cargo télécharge `craft-libs` et le fork d'egui aux révisions épinglées, et la compilation lit aussi `craft-i18n` et `craft-fonts` à leurs révisions épinglées. Le fork `Kazo-ca/craft-libs` sert aux PR en amont. |
| 10 | Page de l'iframe dans l'OS publié | **Corriger Kazo OS** : le service worker sert aussi la page d'une app installée (fil à part « Servir la page iframe des apps installées », dans `Kazo-ca/Os`). Kazo Word demandera la version de l'OS qui contient la correction. Benoit, 2026-11-13. |
| 11 | Piste en amont | **Se joindre aux issues existantes** ; une issue neuve seulement pour ce qui manque ; Claude prépare les textes, Benoit les publie. Benoit, 2026-11-13. |
| 12 | Orthographe | En anglais seulement dans la version web : **accepté pour la v1**, noté comme limite connue dans la fiche du Store. Benoit, 2026-11-13. |
| 13 | Où vit le build de WordCraft | **Dans ce dépôt**, sous `wwwroot/wordcraft/`, avec un fichier de provenance (version, commit, SHA-256), mis à jour par une PR. Le Store compile avec `dotnet` seulement : il lui faut un build déjà fait. Voir le [plan d'intégration](integration-plan.md), section 3. Benoit, 2026-11-13. |
| 14 | Une fenêtre qui passe devant recharge son iframe | **Corriger Kazo OS** : les fenêtres gardent un ordre stable dans la page (leur ordre d'ouverture), l'empilement restant en `z-index`, avec une ADR, dans un fil à part de `Kazo-ca/Os`. Kazo Word demandera la version de l'OS qui contient la correction. Benoit, 2026-11-13. |
| 15 | Le `.wasm` fait 61 Mo, pas 13 Mo | **Garder la décision 13** : le build reste dans ce dépôt, environ 22 Mo d'historique par montée de version de WordCraft. Benoit, 2026-11-13. |

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
| `wordcraft:saved` `{ id }` | hôte → WordCraft | Pas utilisé aujourd'hui : WordCraft envoie `save` sans `id` et se croit enregistré dès l'envoi. La fenêtre Kazo garde son propre état « modifié » et le dit quand une écriture échoue. |
| `wordcraft:dirty` | WordCraft → hôte | Astérisque dans le titre, confirmation à la fermeture. Ne parle que du document affiché : à la fermeture, la fenêtre demande aussi la liste des documents à `ui.inspect`. |
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
- **Un point à régler dans Kazo OS** (décision 10, voir la
  [vérification](plan-verification.md)). Les fichiers `wwwroot/` d'une app
  installée sont servis à `_content/<assembly>/` et `.wasm` part en
  `application/wasm`, mais le service worker de l'OS publié répond à une
  navigation (le chargement de la page de l'iframe) par l'`index.html` de l'OS.
  En développement la page est servie. La taille n'est pas un souci : mesuré au
  chantier 1, le paquet fait 22,9 Mo (61,3 Mo décompressé, dont 61 Mo de wasm)
  pour des limites de 64 Mio compressés et 128 Mio décompressés.
- **L'iframe** porte `allow="fullscreen; clipboard-read; clipboard-write"`.

## Chantiers

Série `word/base` (créée depuis `main` quand le plan est validé), une branche
`word/<n>-<sujet>` et une PR vers `word/base` par chantier, puis une PR
`word/base` → `main` que Benoit fusionne.

1. **Solution et iframe** (`word/1-solution`) : renommer la solution du modèle
   (`Kazo.Word`), manifeste, fenêtre ; récupérer le build web amont épinglé
   (version + SHA-256) dans `wwwroot/` ; page de l'iframe, écran de chargement,
   erreur `failed` ; **mesurer la taille** du paquet ; vérifier que la page se
   charge dans l'iframe en développement puis dans un OS publié, en WebGPU et en
   WebGL2, et que deux fenêtres tournent en même temps.
2. **Ouvrir et enregistrer** (`word/2-open-save`) : associations de fichiers,
   ouvrir par l'explorateur et « Ouvrir avec », `open`/`save`/`dirty`,
   dialogues de l'OS, titre, confirmation à la fermeture, plusieurs fenêtres. La
   fenêtre garde son propre état « modifié » (WordCraft ne sait pas si
   l'enregistrement a réussi) et retrouve chaque document par son nom.
3. **Proxy complet** (`word/3-proxy`) : raccourcis de l'OS, interception du
   sélecteur de fichiers (document, image, polices, destinataires), auteur, mode
   sombre et langue de l'OS par les commandes `ui.theme` et `ui.language`, menu
   de la fenêtre.
4. **Build depuis les sources** (`word/4-build-from-source`) : sous-module du fork de WordCraft, compilation `wasm32` par Trunk 0.21.14 dans la CI avec `craft-i18n` et `craft-fonts` aux révisions épinglées, même comportement que le zip ; retrait des marques ArtCraft dès qu'une modification est faite ; bibliothèque partagée (décisions 8 et 9).
5. **Store** (`word/5-store`) : icône, fiche, `THIRD-PARTY-NOTICES.md` (NOTICE
   et ATTRIBUTION de WordCraft), tests de bout en bout, tag, PR au catalogue
   d'Os-AppMarketPlace (la signature se fait seulement là).

Les chantiers 2, 3 et 4 peuvent avancer en parallèle après le 1.

## Piste en amont

En parallèle de la série, sans la bloquer : proposer à `craft-libs`
(`craft-web-host`) ce qui manque, puis retirer du proxy chaque contournement
devenu inutile.

1. Confirmation d'un enregistrement (`id` et `saved`) et enregistrement demandé
   par l'hôte : se joindre à
   [craft-libs #611](https://github.com/storytold/craft-libs/issues/611).
2. Message `open-request` : Fichier › Ouvrir demande le fichier à l'hôte. À
   proposer sur [craft-libs #610](https://github.com/storytold/craft-libs/issues/610)
   et [wordcraft #4945](https://github.com/storytold/wordcraft/issues/4945), qui
   traitent le même problème en cachant l'onglet Fichier.
3. Réglages confiés à l'hôte : dans `~/.config` de chaque utilisateur Kazo au
   lieu du `localStorage` partagé. Rien en amont pour le web.
4. Touches réservées par l'hôte. Rien en amont.

Retirés après la vérification du 2026-11-12 : le thème et la langue de l'hôte
(les commandes `ui.theme` et `ui.language` existent déjà) et le canvas passé en
paramètre (inutile avec l'iframe). On se joint aux issues existantes (décision 11) ; le calendrier est dans le
[plan d'intégration](integration-plan.md), section 8.

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

- **Page de l'iframe dans l'OS publié** : pas servie aujourd'hui ; corrigée dans
  Kazo OS par un fil à part (décision 10).
- **Enregistrement non confirmé** par WordCraft et **plusieurs documents dans une
  page** : traités dans la fenêtre Kazo au chantier 2.
- **Orthographe en anglais seulement** dans la version web : accepté pour la v1
  (décision 12).
- **Taille du build web** : l'amont annonçait environ 13 Mo de wasm ; mesuré
  au chantier 1 : 61 Mo de wasm, paquet de 22,9 Mo, sous les limites du Store
  (décision 15).
- **Alpha et rythme rapide** (une version par semaine environ) : version
  épinglée, montée de version volontaire et testée.
- **Réglages hors des fichiers Kazo** (`localStorage`, IndexedDB), partagés par
  les utilisateurs Kazo du même navigateur, jusqu'au point 3 de la piste en
  amont.
- **GPU** : il faut WebGPU ou WebGL2.

## Plus tard

- Kazo Sheet (GridCraft), même méthode, et le `handoff` entre les deux apps.
- Mobile.

## Avancement

| Chantier | Branche | Dépend de | État |
| --- | --- | --- | --- |
| Plan | `docs/word-plan` | rien | Fusionné (PR [#1](https://github.com/Kazo-ca/Os-App-Word/pull/1)) |
| Vérification du plan | `docs/word-plan-verification` | plan | Fusionné (PR [#2](https://github.com/Kazo-ca/Os-App-Word/pull/2)) |
| Plan d'intégration détaillé | `docs/word-plan-verification` | vérification | Fusionné (PR [#2](https://github.com/Kazo-ca/Os-App-Word/pull/2)) |
| Kazo OS : page d'iframe d'une app installée | `fix/installed-app-iframe-page` dans `Kazo-ca/Os` | décision 10 | PR [Os#451](https://github.com/Kazo-ca/Os/pull/451) (ADR 0243), à fusionner. Kazo Word demandera la première version de l'OS publiée après la 3.3.0. |
| 1. Solution et iframe | `word/1-solution` | plan | En cours (feu vert de Benoit le 2026-11-13). WordCraft tourne dans une fenêtre Kazo ; décisions 14 (ordre des fenêtres, correction de Kazo OS) et 15 (taille) prises ; reste le test du document qui survit et l'essai dans un OS publié. |
| 2. Ouvrir et enregistrer | `word/2-open-save` | 1 | Fait le 2026-11-13 (résultat dans le [plan d'intégration](integration-plan.md#chantier-2--ouvrir-et-enregistrer-word2-open-save)), PR vers `word/base`. |
| 3. Proxy complet | `word/3-proxy` | 1 | À faire |
| 4. Build depuis les sources | `word/4-build-from-source` | 1, forks créés | Fait le 2026-11-13 (résultat dans le [plan d'intégration](integration-plan.md#chantier-4--build-depuis-les-sources-word4-build-from-source)), PR vers `word/base`. |
| 5. Store | `word/5-store` | 2, 3, 4 | À faire |
| Piste en amont | fork de `storytold/craft-libs` | rien | À faire |
