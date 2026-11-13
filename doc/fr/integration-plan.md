> [English version](../en/integration-plan.md)

# Plan d'intégration détaillé de Kazo Word

Demandé par Benoit le 2026-11-13, après la [vérification](plan-verification.md) du
[plan](plan.md). Le plan dit quoi et pourquoi ; cette page dit **comment** : les fichiers, les
contrats entre les trois parties, le déroulement de chaque cas, puis pour chaque chantier ce
qui est livré, les tests et le critère de fin. Chaque fil de chantier part de cette page.

## 1. Ce qui est décidé

| # | Décision | Source |
| --- | --- | --- |
| 1 à 9 | Voir le [plan](plan.md). | 2026-11-11 et 2026-11-12 |
| 10 | Le service worker de Kazo OS sert la page d'iframe d'une app installée (correction dans `Kazo-ca/Os`, fil à part). Kazo Word demande la version de l'OS qui la contient. | Benoit, 2026-11-13 |
| 11 | En amont, on se joint aux issues existantes ; une issue neuve seulement pour ce qui manque. Claude prépare les textes, Benoit les publie. | Benoit, 2026-11-13 |
| 12 | Le correcteur orthographique en anglais seulement est accepté pour la v1, noté comme limite connue. | Benoit, 2026-11-13 |
| 13 | Le build compilé de WordCraft vit dans ce dépôt, sous `wwwroot/wordcraft/` (section 3). | Benoit, 2026-11-13 |

## 2. Les trois parties

```
┌─ Fenêtre Kazo Word (Blazor, C#) ────────────────────────────────┐
│ WordWindow.razor  ⇄  js/word-host.js   (document de l'OS)        │
│        │                    ⇅  MessagePort (un par fenêtre)      │
│        │        ┌─ iframe  _content/Kazo.Word/host/index.html ─┐ │
│        │        │ host/word-proxy.js                           │ │
│        │        │      ⇅  CustomEvent sur le canvas            │ │
│        │        │ WordCraft (.wasm), data-host="page"          │ │
│        │        └──────────────────────────────────────────────┘ │
│  KazoFile, dialogues de l'OS, titre, fermeture                   │
└──────────────────────────────────────────────────────────────────┘
```

- **La fenêtre (C#)** décide : quel fichier est lié à quel document, où on écrit, le titre,
  la fermeture. Toute la logique testable est en C#.
- **`js/word-host.js`** (dans le document de l'OS, chargé par le composant) crée l'iframe,
  tient le `MessagePort` et appelle le C#. Il ne décide rien.
- **`host/word-proxy.js`** (dans l'iframe) parle à WordCraft : événements du canvas,
  `window.wordcraft`, sélecteur de fichiers, touches. Il ne décide rien non plus.

Un `MessagePort` par fenêtre, remis à l'iframe à son chargement : deux fenêtres ne peuvent
pas se mélanger, et aucun message ne passe par `window.postMessage` après le premier.

## 3. Où vit le build de WordCraft (décision 13)

Constat du 2026-11-13 : le Store compile une app depuis le commit nommé dans `store.json`,
avec `dotnet build -t:KazoOsPack` seulement, et ne récupère que le sous-module
`external/KazoOs` (action `build-app` d'Os-AppMarketPlace). Il n'a ni Rust, ni Trunk, ni le
fork de WordCraft. Le Store ne peut donc pas compiler WordCraft : il lui faut un build
déjà fait.

- **A. Dans ce dépôt ✨** : `wwwroot/wordcraft/` contient le `.js`, le `.wasm` (61 Mo,
  mesuré au chantier 1 ; le plan disait environ 13 Mo) et les licences, avec un fichier de provenance (version, commit, SHA-256). Un script
  les met à jour : depuis le zip officiel (chantier 1), puis depuis notre compilation
  (chantier 4), par une PR. C'est ce que fait Webamp. Le dépôt grossit d'environ 22 Mo (le `.wasm` compressé)
  à chaque montée de version de WordCraft, et GitHub avertit au push d'un fichier de plus de
  50 Mo (il refuse au-delà de 100 Mo).
- **B. Téléchargé pendant la compilation** : une cible MSBuild télécharge le zip épinglé et
  vérifie son SHA-256. Le dépôt reste léger et le Store n'a rien à changer (son poste de
  compilation a le réseau). Mais le contenu du paquet n'est plus tout entier dans le commit
  relu, chaque compilation dépend d'un téléchargement, et à partir du chantier 4 il faut
  publier nos propres builds quelque part.
- **C. Git LFS** : dépôt léger et fichiers suivis par git, mais le Store ne récupère pas les
  fichiers LFS aujourd'hui : il faudrait modifier son action de compilation.

**Benoit a retenu A le 2026-11-13.**

## 4. Fichiers

```
Kazo.Word.slnx
src/Kazo.Word/                      l'app (assembly Kazo.Word)
  WordApp.cs                        [AppManifest] : ca.kazo.word, formats, capacités, fenêtre
  WordWindow.razor(.cs)(.css)       la fenêtre : iframe, chargement, erreur, garde de fermeture
  Hosting/WordFrame.cs              enveloppe C# de js/word-host.js (appels et événements)
  Hosting/FrameMessages.cs          les types des messages (section 5)
  Hosting/FrameStartup.cs           chargement, WordCraft ou erreur, délai de 60 s, « Réessayer »
  Documents/DocumentSession.cs      état d'une fenêtre : documents liés, « modifié », décisions
  Documents/SaveDecision.cs         réécrire, demander un chemin, copie exportée
  Documents/DocumentFormats.cs      extensions, types, filtres des dialogues
  Opening/OpenRequest.cs            fichier demandé au lancement ou par intention
  Pickers/PickPurpose.cs            document, image, polices, destinataires (d'après accept)
  Strings.cs, Resources/*.resx      textes fr et en, localization/fr.json
  wwwroot/host/index.html           page de l'iframe : canvas data-host="page", chargement
  wwwroot/host/word-proxy.js        le proxy
  wwwroot/host/word-proxy.css
  wwwroot/js/word-host.js           côté OS : iframe et MessagePort
  wwwroot/wordcraft/                build de WordCraft, non modifié à la main
    wordcraft.json                  provenance : version, source, noms et SHA-256 des fichiers
    wordcraft-web-<hash>.js, wordcraft-web-<hash>_bg.wasm, LICENSE-*, NOTICE, ATTRIBUTION.md
  wwwroot/icons/app.svg
src/Kazo.Word.Os/                   hôte de débogage (du modèle)
tests/Kazo.Word.Tests/              xUnit
tests/proxy/                        node --test, avec un faux WordCraft
tests/e2e/                          Playwright (node --test), dans l'hôte de débogage
tools/vendor-wordcraft.sh           zip épinglé ou dossier compilé → wwwroot/wordcraft/
external/KazoOs                     sous-module (du modèle)
external/wordcraft                  sous-module du fork (chantier 4 seulement)
THIRD-PARTY-NOTICES.md              chantier 5
```

Identité : id `ca.kazo.word`, éditeur `pub.kazo`, nom « Kazo Word », catégorie
`productivity`, plusieurs instances (`SingleInstancePerUser` à `false`), fenêtre par défaut
1100 × 760, minimum 760 × 480 (le minimum de WordCraft).

## 5. Contrat entre la fenêtre et le proxy

Messages sur le `MessagePort`, tous de la forme `{ type, id?, ... }`. Les octets voyagent en
`ArrayBuffer` transféré, sans copie.

| Message | Sens | Contenu | Rôle |
| --- | --- | --- | --- |
| `init` | fenêtre → proxy | `author`, `theme`, `language`, `reservedKeys` | Premier message, avec le port. |
| `ready` | proxy → fenêtre | `appVersion` | WordCraft écoute. |
| `failed` | proxy → fenêtre | `error` | WordCraft n'a pas démarré (pas de WebGPU ni de WebGL2, wasm introuvable). |
| `open` | fenêtre → proxy | `name`, `bytes` | Ouvre un document. |
| `save` | proxy → fenêtre | `name`, `saveAs`, `bytes` | Tout fichier que WordCraft voudrait télécharger. |
| `dirty` | proxy → fenêtre | `dirty` | Le document affiché a des modifications. |
| `pick` | proxy → fenêtre | `id`, `accept`, `multiple` | WordCraft ouvre le sélecteur de fichiers. |
| `picked` | fenêtre → proxy | `id`, `files` ou `cancelled` | Réponse au sélecteur. |
| `command` | fenêtre → proxy | `id`, `method`, `params` | Appelle `window.wordcraft.request`. |
| `result` | proxy → fenêtre | `id`, `ok`, `value` ou `error` | Réponse à `command`. |
| `key` | proxy → fenêtre | touche et modificateurs | Un raccourci de l'OS tapé dans l'iframe. |
| `focus` | proxy → fenêtre | rien | Un clic dans l'iframe : la fenêtre Kazo passe devant. |

Le proxy traduit `open`, `save`, `dirty`, `ready`, `failed` de et vers les événements
`wordcraft:*` du canvas, sans les interpréter.

## 6. Déroulement des cas

**Démarrage.** La fenêtre affiche « Chargement », `word-host.js` crée l'iframe
(`allow="fullscreen; clipboard-read; clipboard-write"`) sur
`_content/Kazo.Word/host/index.html?author=<nom affiché>`. Le proxy pose ses écouteurs sur le
canvas, lit `wordcraft/wordcraft.json`, importe le `.js` et appelle `init` avec le `.wasm`.
À `ready`, la fenêtre règle le thème et la langue (`ui.theme`, `ui.language`), envoie le
fichier demandé ou crée un document vide, puis retire « Chargement ». Sans `ready` ni
`failed` après 60 secondes : écran d'erreur avec « Réessayer ».

**Ouvrir depuis l'OS.** Double-clic, « Ouvrir avec » ou intention : `OpenRequest` donne le
chemin, la fenêtre lit avec `KazoFile`, envoie `open` et lie le nom au chemin. Un fichier
ouvert alors que la fenêtre a déjà un document part dans une **nouvelle fenêtre**.

**Enregistrer.** `DocumentSession` décide à partir de `name`, `saveAs` et de l'extension :

| Cas | Décision |
| --- | --- |
| `name` est lié à un chemin et `saveAs` est faux | Réécrit ce chemin. |
| `saveAs` est vrai, ou `name` n'est lié à rien (document sans titre, import enregistré dans un autre format) | Dialogue d'enregistrement de l'OS, puis le document est lié au chemin choisi. |
| Format de copie (PDF, PNG, texte, Markdown) | Dialogue d'enregistrement, le document reste lié à son fichier. |

Si l'écriture échoue ou si le dialogue est annulé, la fenêtre affiche l'erreur et **reste
« modifiée »** même si WordCraft se croit enregistré ; le prochain enregistrement redemande
un chemin.

**« Modifié ».** L'état de la fenêtre est : WordCraft dit `dirty`, **ou** un enregistrement
n'a pas abouti depuis la dernière écriture réussie. Le titre porte l'astérisque.

**Fermer.** `IWindowCloseGuard` : la fenêtre demande `ui.inspect`, qui liste tous les
documents de la page avec leur état. S'il y a des modifications : « Enregistrer », « Ne pas
enregistrer », « Annuler ». « Enregistrer » envoie `file.save` et attend que l'écriture
réussisse avant de fermer.

**Fichier › Ouvrir dans WordCraft.** Le proxy arrête le clic sur le champ de fichier et envoie
`pick`. Pour un document, la fenêtre ouvre le dialogue de l'OS puis lance une nouvelle
fenêtre Kazo Word sur ce fichier et répond `cancelled` ; si la fenêtre n'a qu'un document
vide jamais touché, elle l'ouvre sur place. Pour une image, des polices ou des destinataires,
elle lit les fichiers et répond `picked` : le proxy les remet au champ et déclenche `change`.

**Fichier › Nouveau dans WordCraft.** `Ctrl+N` est repris par le proxy et ouvre une nouvelle
fenêtre. Le bouton de l'onglet Fichier crée un second document dans la page : il est suivi par
son nom et par `ui.inspect`, et son premier enregistrement demande un chemin.

**Raccourcis de l'OS.** Le proxy écoute `keydown` en capture ; une touche de la liste
`reservedKeys` est arrêtée et envoyée en `key`, et `word-host.js` la rejoue sur le document
de l'OS. Tout le reste va à WordCraft.

**Thème et langue.** À `ready`, puis quand l'OS change : `ui.theme` (clair ou sombre, lu sur
la fenêtre) et `ui.language` (langue de l'utilisateur Kazo, anglais si WordCraft ne l'a pas).

## 7. Chantiers

Série `word/base`, une branche et une PR vers `word/base` par chantier, comme dans le plan.
Aucun chantier ne commence sans le feu vert de Benoit.

### Préalables

| Quoi | Qui | État |
| --- | --- | --- |
| Ordre stable des fenêtres dans la page (décision 14) | Fil Kazo OS demandé au coordinateur le 2026-11-13 | À faire. Avant de fermer le chantier 1 (test du document qui survit) et avant le chantier 5. |
| Correction du service worker de Kazo OS (décision 10) | Fil « Servir la page iframe des apps installées » | PR [Os#451](https://github.com/Kazo-ca/Os/pull/451) (ADR 0243) prête le 2026-11-13, à fusionner. Rien à changer côté app. Ne bloque que le chantier 5. |
| Forks `Kazo-ca/wordcraft`, `Kazo-ca/craft-libs`, `Kazo-ca/gridcraft` (décision 9) | Benoit, par le coordinateur | Avant le chantier 4. |

### Chantier 1 : solution et iframe (`word/1-solution`)

Un essai qui doit prouver que WordCraft démarre dans une fenêtre Kazo.

- Renommer la solution du modèle (`Kazo.Word`, `Kazo.Word.Os`, `Kazo.Word.Tests`), `WordApp`
  avec l'identité de la section 4, sans associations de fichiers encore.
- `tools/vendor-wordcraft.sh` : télécharge `wordcraft-web-0.18.1.zip`, vérifie le SHA-256
  épinglé, copie le `.js`, le `.wasm` et les licences, écrit `wordcraft.json`.
- `host/index.html`, `host/word-proxy.js` (démarrage, `ready`, `failed`), `js/word-host.js`,
  `WordWindow` avec les trois états : chargement, WordCraft, erreur.
- Tests : manifeste ; `wordcraft.json` correspond aux fichiers (SHA-256) ; proxy avec un faux
  WordCraft (`ready`, `failed`, délai dépassé) ; un parcours Playwright qui attend `ready`.
- **Terminé quand** : dans l'hôte de débogage, WordCraft s'affiche et on peut taper du texte,
  en WebGPU et avec `?webgl` ; deux fenêtres tournent en même temps ; un clic dans l'iframe
  fait passer la fenêtre devant ; la taille du paquet `.kazoapp` est notée dans le plan ; le
  résultat dans un OS publié est noté (attendu : en échec jusqu'à la décision 10).

**Résultat (2026-11-13).**

- WordCraft s'affiche en 4 secondes environ dans l'hôte de débogage (Release, Kazo OS 3.3.0) et
  le texte tapé arrive dans le document, avec le rendu WebGPU (`BrowserWebGpu`) et avec le
  rendu WebGL 2 (argument de lancement `--webgl`, qui ajoute `?webgl` à l'iframe). WordCraft
  passe de lui-même à WebGL 2 quand le navigateur n'a pas d'adaptateur WebGPU.
- Deux fenêtres tournent en même temps, chacune avec son document ; un clic dans WordCraft fait
  passer sa fenêtre devant.
- En mode hôte, WordCraft s'ouvre sur son écran d'accueil (nouveau document, modèles) : le
  chantier 2 envoie le fichier demandé ou crée un document vide.
- Paquet `.kazoapp` : 22,9 Mo, 61,3 Mo décompressé, 31 fichiers (limites : 64 Mio, 128 Mio,
  2000 fichiers). Le `.wasm` seul fait 61 Mo.
- Mémoire (Chromium sans écran, rendu logiciel, écran d'accueil) : environ 130 Mo pour la
  première fenêtre, puis environ 25 Mo par fenêtre de plus (quatre fenêtres : 233 Mo de plus
  que sans Kazo Word). Le module compilé est partagé ; un gros document coûtera plus.
- **Trouvé** : Kazo OS range les fenêtres dans la page selon leur ordre d'empilement. Quand une
  fenêtre passe devant, son élément est déplacé, et un iframe déplacé se recharge : WordCraft
  repart de zéro et le document est perdu (décision 14, section 9). `word-host.js` reconnecte
  la page rechargée, mais le document est perdu.
- OS publié : à essayer une fois Os#451 publiée (décision 10).

### Chantier 2 : ouvrir et enregistrer (`word/2-open-save`)

- Associations de fichiers et « Ouvrir avec » (formats du plan), `OpenRequest`, intentions.
- `DocumentSession`, `SaveDecision`, `DocumentFormats` ; messages `open`, `save`, `dirty`.
- Dialogues de l'OS, titre avec astérisque, garde de fermeture avec `ui.inspect`.
- Tests xUnit : chaque ligne du tableau « Enregistrer », échec d'écriture, dialogue annulé,
  export, deux documents dans une page, titre, fermeture. Playwright : ouvrir un `.docx`
  depuis l'explorateur, le modifier, l'enregistrer, le rouvrir ; Enregistrer sous ; export
  PDF ; fermer avec des modifications ; deux fenêtres.
- **Terminé quand** : aucun cas ne perd de modifications sans que l'utilisateur l'ait choisi,
  y compris quand l'écriture échoue.

**Résultat (2026-11-13).**

- Ce que WordCraft 0.18.1 envoie, vérifié dans sa page : un document neuf est nommé d'après ses
  premiers mots (« Hello.docx ») et son premier enregistrement arrive avec `saveAs` vrai, les
  suivants avec le même nom et `saveAs` faux ; Enregistrer sous en `.odt` arrive avec `saveAs`
  vrai ; un PDF, un PNG, du HTML, du Markdown ou du texte arrivent avec `saveAs` faux (des
  copies). WordCraft efface son état « modifié » dès qu'il a envoyé l'enregistrement.
- `DocumentSession` lie chaque nom de document de WordCraft à un chemin. Une copie ne change
  jamais la liaison. Un enregistrement qui n'aboutit pas (dialogue annulé, écriture refusée,
  toute autre erreur) laisse la fenêtre « modifiée » et le prochain enregistrement de ce
  document redemande un chemin. Quand l'utilisateur choisit un autre nom de fichier, la fenêtre
  renomme le document dans WordCraft (`file.rename`) pour que son titre et ses enregistrements
  suivants portent le nom du fichier.
- Démarrage : un fichier demandé (argument de lancement) est lu avec `KazoFile` et envoyé par
  `open` ; sinon un document vide (`file.new`). Dans les deux cas l'écran d'accueil de WordCraft
  est fermé (`ui.backstage {"value": false}`).
- Titre : `*nom — Kazo Word`, « Sans titre » avant le premier enregistrement.
- Fermeture : `ui.inspect` liste les documents ; « Oui » enregistre chacun
  (`engine.execute` `file.save` avec `doc`) et ne ferme qu'une fois tous écrits ; « Non » ferme ;
  « Annuler » garde la fenêtre. Si WordCraft ne répond pas, la fenêtre demande seulement s'il faut
  fermer sans enregistrer.
- Associations : les dix formats de traitement de texte en `open` et `edit` ; `.md`, `.txt`,
  `.html`, `.htm`, `.tex` en `open` seulement. Capacités : dialogues Ouvrir et Enregistrer,
  dossier personnel en lecture et écriture, `apps.launch` (ouvrir un fichier dans une autre
  fenêtre Kazo Word). `.tex` n'a pas d'autre app dans Kazo OS : Kazo Word y devient l'app par
  défaut de fait.
- Pour le chantier 3 : `WordWindow.OpenInNewWindowAsync(path)` (sur place si la fenêtre n'a
  qu'un document vide jamais touché, sinon une nouvelle fenêtre), `ShowOpenDialogAsync()` et
  `IsUntouchedBlank`.
- Trouvé : un rappel `[JSInvokable]` ne tourne pas dans le contexte de l'app ; sans
  `KazoExecutionContext.Enter(AppContext)`, `KazoFile` refuse. Le parcours Playwright l'a montré.
- Tests : 57 cas xUnit de plus (`DocumentSessionTests`, `DocumentFormatsTests`) ; trois
  parcours Playwright (`tests/e2e/open-save.e2e.mjs`) : enregistrer un nouveau document, le
  réenregistrer sans dialogue, le rouvrir avec `xdg-open` dans une deuxième fenêtre ; Enregistrer
  sous en `.odt` puis une copie PDF ; fermer avec des modifications (Annuler, dialogue annulé,
  puis Oui). L'échec d'écriture est couvert en xUnit.

### Chantier 3 : proxy complet (`word/3-proxy`)

- Sélecteur de fichiers (les quatre usages), `Ctrl+N` et Fichier › Ouvrir en nouvelle fenêtre.
- Raccourcis de l'OS (`reservedKeys`), auteur, thème, langue, focus.
- Menu de la fenêtre : Nouveau, Ouvrir, Enregistrer, Enregistrer sous, par `command`.
- Essai : réglages par utilisateur Kazo. Le proxy charge `~/.config/ca.kazo.word/` dans le
  `localStorage` de l'iframe avant `init` et renvoie les écritures à la fenêtre. S'il tient,
  le risque « réglages partagés » disparaît sans rien demander en amont.
- Vérifier l'impression et le presse-papiers ; noter ce qui ne marche pas.
- Tests : proxy avec faux WordCraft (interception du champ pour chaque usage, touches
  réservées et non réservées, `command` et délai) ; Playwright : insérer une image depuis les
  fichiers Kazo, `Alt+F4` ferme la fenêtre, le thème suit l'OS.
- **Terminé quand** : aucun dialogue du navigateur ne s'ouvre depuis Kazo Word, et les
  raccourcis de l'OS marchent quand le curseur est dans le document.

### Chantier 4 : build depuis les sources (`word/4-build-from-source`)

- Sous-module `external/wordcraft` (fork). Le Store ne le récupère pas et n'en a pas besoin.
- Workflow `build-wordcraft.yml`, lancé à la main : Rust stable et cible
  `wasm32-unknown-unknown`, Trunk 0.21.14 vérifié par SHA-256, `craft-i18n` à la révision de
  `craft-i18n.rev`, `craft-fonts` à la révision du workflow amont, `trunk build --release`,
  puis `tools/vendor-wordcraft.sh` sur le résultat et une PR qui remplace `wwwroot/wordcraft/`.
- Les tests des chantiers 1 à 3 tournent sur ce build : c'est la preuve du « même
  comportement que le zip ».
- Dès qu'une modification de WordCraft est livrée : retirer le nom et le logo ArtCraft du
  build, et le dire dans `wordcraft.json`.
- Mesures pour la décision 8 (bibliothèque partagée) : taille du `.wasm`, des polices et des
  traductions, ce qui pourrait être chargé à part.
- **Terminé quand** : une montée de version de WordCraft se fait en changeant le commit du
  sous-module et en lançant le workflow.

### Chantier 5 : Store (`word/5-store`)

- Icône sans logo ArtCraft, fiche fr et en avec « basé sur WordCraft de l'équipe ArtCraft »
  et les limites connues (alpha, orthographe en anglais seulement, polices asiatiques),
  captures d'écran.
- `THIRD-PARTY-NOTICES.md` (NOTICE et ATTRIBUTION de WordCraft), licences dans le paquet.
- Version minimale de l'OS : celle de la décision 10. Parcours Playwright dans un OS publié.
- Tag par le workflow « Tag a release », PR au catalogue d'Os-AppMarketPlace (signature
  là-bas seulement), ligne dans `features.md` de Kazo OS.
- **Terminé quand** : Kazo Word s'installe depuis le Store et ouvre un `.docx` par double-clic.

### Ordre

Le chantier 1 d'abord. Ensuite 2, 3 et 4 en parallèle, dans trois fils : 2 touche le C#, 3
touche le proxy, 4 touche la compilation. Le chantier 5 en dernier, après la correction de
Kazo OS.

## 8. Piste en amont (décision 11)

Brouillons en anglais dans `/mnt/project-files/word/upstream/`, publiés par Benoit sous son
nom, une issue par problème, sans mention d'agent.

| Sujet | Où | Quand |
| --- | --- | --- |
| Confirmer un enregistrement (`id`, `saved`) dans WordCraft | Commentaire sur craft-libs #611 | Après le chantier 2, avec notre cas d'échec d'écriture. |
| Fichier › Ouvrir demandé à l'hôte (`open-request`) | Commentaire sur craft-libs #610 et wordcraft #4945 | Après le chantier 3. |
| Touches réservées par l'hôte | Issue neuve sur craft-libs | Après le chantier 3. |
| Réglages confiés à l'hôte | Issue neuve sur craft-libs | Après l'essai du chantier 3. |

Chaque proposition acceptée retire un contournement du proxy.

## 9. Risques suivis

| Risque | Réponse |
| --- | --- |
| WordCraft change vite (une version par semaine) | Version épinglée ; les tests du proxy et les parcours Playwright disent ce qu'une montée de version casse. |
| Le Store analyse le JavaScript de l'app (CodeQL) | Le `.js` généré de WordCraft sera analysé : à vérifier au chantier 5, avant le tag. |
| Mémoire : chaque fenêtre charge son propre WordCraft | Mesuré au chantier 1 : environ 130 Mo pour la première fenêtre, 25 Mo par fenêtre de plus, sans document. À refaire avec de gros documents au chantier 2. |
| Une fenêtre qui passe devant recharge son iframe (Kazo OS déplace son élément dans la page) | Décision 14 : Kazo OS garde un ordre stable des fenêtres dans la page, l'empilement restant en `z-index` (fil à part de `Kazo-ca/Os`). Le test « le document survit quand sa fenêtre passe devant » arrive avec la montée du sous-module vers cette version. |
| Le `.wasm` fait 61 Mo, pas 13 Mo | Décision 15 : la décision 13 reste. |
| GPU absent | Écran d'erreur clair (`failed`). |
