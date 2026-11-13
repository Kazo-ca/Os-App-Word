> [English version](README.md)

# Kazo Word

Un traitement de texte pour Kazo OS : [WordCraft](https://github.com/storytold/wordcraft) (MIT ou
Apache-2.0) tourne dans un iframe d'une fenêtre Kazo OS, piloté par le C# de la fenêtre à
travers un petit proxy. En cours de développement : voir le [plan](doc/fr/plan.md) et le
[plan d'intégration](doc/fr/integration-plan.md).

| Dossier | Contenu |
| --- | --- |
| `src/Kazo.Word` | L'app : `WordApp` (manifeste), `WordWindow` (la fenêtre), `Hosting/` (l'iframe), `wwwroot/host/` (la page de l'iframe et son proxy), `wwwroot/js/word-host.js` (côté OS), `wwwroot/wordcraft/` (le build de WordCraft) |
| `src/Kazo.Word.Os` | Un hôte qui lance tout Kazo OS avec les apps de base et Kazo Word : le projet à démarrer |
| `tests/Kazo.Word.Tests` | Tests C# : manifeste, démarrage de WordCraft, le build de WordCraft correspond à sa provenance |
| `tests/proxy` | Tests du proxy de l'iframe (avec un faux WordCraft) et du module côté OS, sous node |
| `tests/e2e` | Parcours dans l'hôte Kazo OS avec Playwright : démarrage, saisie, WebGPU et WebGL 2, deux fenêtres |
| `tools/vendor-wordcraft.sh` | Place un build de WordCraft dans `src/Kazo.Word/wwwroot/wordcraft/` avec sa provenance |
| `tools/build-wordcraft.sh` | Compile WordCraft depuis `external/wordcraft` et le place dans l'app |
| `external/KazoOs` | Le dépôt Kazo OS en sous-module git, sur un tag de release |
| `external/wordcraft` | Notre fork de WordCraft (`Kazo-ca/wordcraft`) en sous-module git : utile seulement pour compiler WordCraft |
| `store/` | Ce que montre le Kazo OS Store : icône (PNG 256×256), descriptions courtes, descriptions, `store.json` |

## Le build de WordCraft

`src/Kazo.Word/wwwroot/wordcraft/` n'est jamais modifié à la main (décision 13 du plan). Il est
compilé depuis le sous-module `external/wordcraft`, notre fork de WordCraft, et son
`wordcraft.json` nomme le commit du fork (un test C# vérifie que c'est celui du sous-module).

Pour changer de version de WordCraft : déplacez le sous-module sur un commit du fork (pour une
release, la branche `kazo/v<version>` du fork, faite depuis le tag amont) dans une PR. Le workflow
**Build WordCraft** le compile sur cette PR. Une fois fusionnée, lancez **Build WordCraft** depuis
l'onglet Actions sur cette branche : il compile, fait tourner la CI sur le résultat et ouvre une
PR qui remplace le dossier.

Le même build sur un ordinateur (Rust stable avec `wasm32-unknown-unknown`, Trunk 0.21.14, et
`storytold/craft-i18n` et `storytold/craft-fonts` aux révisions qu'épingle le sous-module ; le
script les vérifie) :

```bash
git submodule update --init external/wordcraft
CRAFT_I18N_DIR=<craft-i18n> CRAFT_FONTS_DIR=<craft-fonts> tools/build-wordcraft.sh
```

`tools/vendor-wordcraft.sh` prend aussi le zip officiel de WordCraft (la source jusqu'au chantier 4) :

```bash
tools/vendor-wordcraft.sh                 # télécharge le zip officiel épinglé et le vérifie
tools/vendor-wordcraft.sh --zip <fichier> # pareil, depuis un zip déjà téléchargé
tools/vendor-wordcraft.sh --dir <dossier> # un dossier compilé depuis external/wordcraft par packaging/web/package.sh
```

Le `.wasm` fait 61 Mo : GitHub avertit au push, et l'accepte.

## Tests

```bash
dotnet build Kazo.Word.slnx -c Release
dotnet test Kazo.Word.slnx -c Release --no-build
npm ci
npm test               # le proxy et le module côté OS
npm run test:e2e       # Playwright, dans l'hôte Release (Chromium) : démarrage, ouvrir et enregistrer
```

Lancez `launch ca.kazo.word --webgl` dans le terminal de Kazo OS pour forcer le rendu WebGL 2
de WordCraft.

## Premiers pas

```bash
# Un dépôt créé depuis le template GitHub a déjà le sous-module :
git clone --recurse-submodules <votre dépôt>

# Un dossier créé par « dotnet new kazoos-solution » l'ajoute une fois :
git init
git submodule add --depth 1 https://github.com/Kazo-ca/Os.git external/KazoOs
git -C external/KazoOs checkout v<version de Kazo OS>
```

Ouvrez ensuite `Kazo.Word.slnx` dans Visual Studio ou Rider et démarrez `Kazo.Word.Os`,
ou lancez `dotnet run --project src/Kazo.Word.Os`.

## La version de Kazo OS

`external/KazoOs/wasm2/KazoOs/build/Versions.props` est la version de Kazo OS de ce dépôt.
Pour changer de version, placez le sous-module sur son tag et validez-le :

```bash
git -C external/KazoOs fetch --depth 1 origin tag v2.2.0
git -C external/KazoOs checkout v2.2.0
git add external/KazoOs
```

Ne modifiez jamais le sous-module : on ne modifie pas l'OS depuis un dépôt d'app, et la CI refuse
un sous-module modifié ou hors de son tag.

## Deux profils de build

| Profil | Kazo OS vient de | Usage |
| --- | --- | --- |
| `Debug` | les projets du sous-module (`KazoOsUseSource=true`) | entrer dans le SDK et l'OS, points d'arrêt partout |
| `Release` | les paquets `Kazo.Os.*` de nuget.org, à la version du sous-module | CI, `dotnet publish`, le Kazo OS Store |

Forcez l'un ou l'autre avec `-p:KazoOsUseSource=true` ou `false`.

## Publier dans le Kazo OS Store

1. Remplissez `store/` (icône, descriptions courtes, descriptions, catégorie et étiquettes dans `store.json`).
2. Changez `Version` dans la classe `[AppManifest]` de l'app et validez-la dans `main`, puis posez le
   tag : lancez le workflow **Tag a release** (onglet Actions, ou l'API GitHub pour un agent), qui pose
   sur la tête de `main` le tag de la version du manifeste et lance les deux workflows de
   publication ; ou poussez le tag vous-même : `git tag v1.0.0 && git push --tags`.
3. Le workflow **Publish to the Kazo OS Store** compile l'app et produit le dossier du catalogue
   `apps/<appId>_<version>/`. Avec un secret `HACK_STORE_TOKEN` (un jeton qui peut créer un fork et
   ouvrir des pull requests), il ouvre la pull request vers `Kazo-ca/Os-AppMarketPlace` depuis
   votre fork ; sans lui, téléchargez l'artefact du workflow et ouvrez la pull request vous-même.

`HACK_STORE_TOKEN` est un jeton d'accès personnel GitHub à vous, que Kazo OS ne fournit pas :

1. GitHub ▸ **Settings** ▸ **Developer settings** ▸ **Personal access tokens** ▸ **Tokens (classic)** ▸
   **Generate new token (classic)**, avec une expiration courte et les scopes **`repo`** et
   **`workflow`** (un jeton à grain fin ne peut pas couvrir à la fois votre fork et le dépôt du catalogue).
2. Copiez-le (affiché une seule fois) dans ce dépôt : **Settings** ▸ **Secrets and variables** ▸
   **Actions** ▸ **New repository secret**, nom `HACK_STORE_TOKEN` (ou `gh secret set HACK_STORE_TOKEN`).

`repo` donne accès à tous vos dépôts : gardez le jeton dans ce seul secret et révoquez-le au moindre doute.

Le catalogue ne prend jamais de binaire de votre part : une fois votre pull request relue et
fusionnée, sa CI compile l'app au commit du tag.

## Publier sur la branche `kazoos-packages`

Le même tag `v*` lance aussi **Publish the Kazo OS packages** : il fait le paquet de l'app et le
pousse sur la branche `kazoos-packages` de ce dépôt, en gardant les versions déjà publiées. Les
utilisateurs de Kazo OS l'installent alors avec `kazopkg install <owner>/<repo>`, sans le Kazo OS Store.

Ces paquets ne sont pas signés : les apps sont signées seulement dans le dépôt du Kazo OS Store
(`Kazo-ca/Os-AppMarketPlace`), quand il compile votre app depuis le catalogue (ADR 0219). Kazo OS
les vérifie par leur SHA-256 et demande un administrateur avant d'installer depuis une source non
fiable. Pour un paquet signé, installez l'app depuis le Kazo OS Store.

## Plan

Le plan de l'app Kazo Word (WordCraft dans Kazo OS) est dans [`doc/fr/plan.md`](doc/fr/plan.md) ; l'index de la documentation est [`doc/fr/README.md`](doc/fr/README.md).
