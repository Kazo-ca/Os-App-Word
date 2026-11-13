> [English version](../en/plan-verification.md)

# Vérification du plan de Kazo Word (2026-11-12)

Benoit a demandé le 2026-11-12 de contre-vérifier le [plan](plan.md) sur le code source de
WordCraft et sur les issues de son dépôt, avant de commencer le chantier 1. Cette page dit ce
qui a été lu, ce qui est confirmé, ce qui change dans le plan, les questions posées et leurs réponses.

## Ce qui a été lu

| Source | Version lue |
| --- | --- |
| [`storytold/wordcraft`](https://github.com/storytold/wordcraft) | `6bf96e65` (2026-11-12), version 0.18.1 |
| [`storytold/craft-libs`](https://github.com/storytold/craft-libs) (`craft-web-host`, `craft-web-api`) | `f92a91a` (2026-11-12) ; WordCraft épingle `fd5e2fe` |
| [`storytold/craft-common-actions`](https://github.com/storytold/craft-common-actions) (workflow de release) | `804f702` |
| [`storytold/egui`](https://github.com/storytold/egui) (fork d'egui, `eframe` web) | `6d518fc7`, la révision épinglée par WordCraft |
| [`rfd`](https://github.com/PolyMeilex/rfd) (dialogues de fichiers) | 0.17.2, la version du `Cargo.lock` |
| Kazo OS | `24b57360` (version 3.3.0) |
| Issues et PR en amont | Recherche du 2026-11-12 sur `web`, `host`, `iframe`, `embed`, `craft-web-host` (liste plus bas) |

Non fait : compiler WordCraft et télécharger le zip de la release (le conteneur de travail ne
le permet pas). La taille vient de la note d'hébergement de WordCraft et sera mesurée au
chantier 1.

## Résultat

Le plan tient : l'iframe avec un proxy, le mode `data-host="page"`, les messages et le
découpage en chantiers sont confirmés par le code. Sept points changent, dont un dans Kazo OS.

## Ce qui est confirmé

| Hypothèse du plan | Où c'est vérifié | Résultat |
| --- | --- | --- |
| Messages `ready`, `open`, `save`, `dirty`, `failed`, mode `data-host="page"` | `apps/wordcraft-web/src/host.rs`, `crates/web-host` de craft-libs | Confirmé. Le contrat a aussi `host`, `saved`, `handoff`, `handoff-done`. |
| `?author=` nomme l'auteur | `host.rs` (`set_author`, 200 caractères au plus) | Confirmé. |
| Commandes pour le C# (`window.wordcraft`) | `apps/wordcraft-web/src/api.rs`, `docs/control-protocol.md` | Confirmé. `ui.theme` et `ui.language` existent déjà : le mode sombre et la langue de l'OS se règlent sans rien demander en amont. `ui.inspect` donne la liste des documents ouverts avec leur état « modifié ». |
| Le proxy peut reprendre les raccourcis de l'OS | `crates/eframe/src/web/events.rs` du fork d'egui | Confirmé. WordCraft écoute les touches sur le canvas : un écouteur en phase de capture sur la page de l'iframe passe avant lui. |
| Le proxy peut intercepter le sélecteur de fichiers | `src/backend/wasm.rs` de `rfd` 0.17.2 | Confirmé. `rfd` crée un champ de fichier dans un bloc `rfd-overlay`, écoute `change` et `cancel`, puis appelle `click()`. Un seul écouteur de clic en capture suffit. |
| Taille du paquet | `packaging/web/README.md` | Environ 13 Mo de wasm, 5 Mo compressés : loin des limites de 64 Mio et 128 Mio. Pas d'ADR à prévoir. **Corrigé au chantier 1** : le `.wasm` de 0.18.1 fait 61 Mo et le paquet 22,9 Mo (61,3 Mo décompressé), toujours sous les limites. |
| En-têtes d'isolation | `service-worker.published.js` de Kazo OS | Le service worker ajoute déjà COOP, COEP et CORP, ce que demande la note d'hébergement de WordCraft. |
| Côté Kazo OS | SDK 3.3.0 | Dialogues ouvrir et enregistrer, associations de fichiers, `IWindowCloseGuard`, nom affiché de l'utilisateur, plusieurs fenêtres par app : tout existe. |

## Ce qui change dans le plan

1. **La page de l'iframe n'est pas servie par le Kazo OS publié.** Le chargement d'une page
   dans une iframe est une « navigation ». Le service worker publié répond à toute navigation
   par l'`index.html` de l'OS (`shouldServeIndexHtml`), sauf pour les fichiers de la release ;
   les fichiers d'une app installée n'en font pas partie. L'iframe démarrerait donc un second
   Kazo OS au lieu de WordCraft. En développement, le service worker sert bien la page : le
   problème ne se voit qu'une fois l'OS publié. Le plan disait « rien à changer dans Kazo OS » :
   c'est faux sur ce point. Voir la question 10.
2. **Un enregistrement n'est pas confirmé.** WordCraft envoie `save` sans `id` et se
   considère enregistré dès l'envoi. Si l'écriture échoue dans Kazo OS, ou si l'utilisateur
   annule le dialogue d'enregistrement, WordCraft croit quand même que tout est enregistré.
   La fenêtre Kazo garde donc son propre état « modifié » et le dit clairement en cas d'échec.
   L'amont le sait : [craft-libs #611](https://github.com/storytold/craft-libs/issues/611).
3. **WordCraft web ouvre plusieurs documents dans la même page.** Fichier › Nouveau et
   Fichier › Ouvrir ajoutent un document et WordCraft bascule entre eux ; `dirty` ne parle que
   du document affiché et `save` ne désigne un document que par son nom. Pour garder « un
   document par fenêtre » (décision 5) : un document choisi par Fichier › Ouvrir s'ouvre dans
   une **nouvelle fenêtre Kazo Word**, et à la fermeture la fenêtre demande à `ui.inspect` si un
   document a des modifications. L'option qui cacherait l'onglet Fichier (`?chrome=none`)
   n'existe pas encore dans WordCraft ([#4945](https://github.com/storytold/wordcraft/issues/4945)).
4. **Le sélecteur de fichiers sert à quatre choses**, pas seulement à Fichier › Ouvrir :
   document, image, polices (plusieurs fichiers) et destinataires du publipostage. Le proxy
   lit les extensions demandées par le champ pour choisir le bon dialogue Kazo, puis remet les
   fichiers au champ.
5. **Compiler depuis les sources demande plus que le fork de WordCraft** : Rust stable avec la
   cible `wasm32-unknown-unknown`, Trunk 0.21.14, le dépôt `craft-i18n` à la révision de
   `craft-i18n.rev` (sans lui l'interface reste en anglais) et le dépôt `craft-fonts` à la
   révision du workflow de release. `craft-libs` (quatre révisions différentes) et le fork
   d'egui sont téléchargés par Cargo : pas besoin de sous-module pour eux. Le fork
   `Kazo-ca/craft-libs` sert seulement à préparer les PR en amont. « Même résultat que le
   zip » veut dire même comportement, pas les mêmes octets.
6. **La piste en amont recoupe des issues qui existent déjà** (tableau plus bas). On s'y
   joint au lieu d'ouvrir cinq issues neuves. Le robot du dépôt demande une issue par problème.
   Le dépôt est très actif (environ 1 500 issues, plus de 300 PR ouvertes) : rien du plan ne
   doit attendre une fusion en amont.
7. **Limites de la version web à connaître** : le correcteur orthographique ne marche qu'en
   anglais ([#6140](https://github.com/storytold/wordcraft/issues/6140), voir la question 12) ;
   seules de petites polices sont incluses, le chinois et le japonais s'affichent mal
   ([#5621](https://github.com/storytold/wordcraft/issues/5621)) ; un export DOCX, ODT ou RTF
   efface à tort l'état « modifié » ([#6131](https://github.com/storytold/wordcraft/issues/6131),
   correction proposée dans la PR #6446).

Détails pour le chantier 1 : le zip officiel a son propre `index.html` sans `data-host` ; on
livre notre page et on retrouve à la compilation les noms hachés (`wordcraft-web-<hash>.js`,
`wordcraft-web-<hash>_bg.wasm`) ; les écouteurs du proxy sont posés avant l'appel à `init` ;
l'iframe porte `allow="fullscreen; clipboard-read; clipboard-write"`.

## Issues et PR en amont qui nous concernent

| Notre besoin | En amont | État le 2026-11-12 |
| --- | --- | --- |
| Confirmer un enregistrement (`id`, `saved`), demander un enregistrement depuis l'hôte | [craft-libs #611](https://github.com/storytold/craft-libs/issues/611), partie 1 | Ouverte, personne dessus. |
| Enregistrement automatique vers l'hôte | craft-libs #611 partie 2, [PR craft-libs #713](https://github.com/storytold/craft-libs/pull/713), PR wordcraft #6015 (brouillon) | En cours par un contributeur. |
| Fichier › Ouvrir en mode hébergé | [wordcraft #4945](https://github.com/storytold/wordcraft/issues/4945), [craft-libs #610](https://github.com/storytold/craft-libs/issues/610) (`?chrome=none` cache l'onglet Fichier) | Ouvertes, pas de PR. Notre message `open-request` est une autre réponse au même problème. |
| Thème de l'hôte | [wordcraft #6393](https://github.com/storytold/wordcraft/issues/6393), point 3 | Question ouverte. `ui.theme` nous suffit. |
| Lecture seule décidée par l'hôte | [craft-libs #609](https://github.com/storytold/craft-libs/issues/609), wordcraft #2229 et PR #2272 | Ouvertes. Utile plus tard pour les fichiers protégés. |
| Orthographe hors anglais | [wordcraft #6140](https://github.com/storytold/wordcraft/issues/6140), craft-libs #745 | Ouvertes, pas de PR. |
| Contrat commun des hôtes | [craft-libs #358](https://github.com/storytold/craft-libs/issues/358) | Livré (PR #420 et #413), suivi ouvert. |
| Réglages confiés à l'hôte | Rien pour le web (craft-libs #12 traite le bureau) | À proposer. |
| Touches réservées par l'hôte | Rien trouvé | À proposer. |
| Canvas passé en paramètre | Rien trouvé | Inutile avec l'iframe : retiré de la piste. |

Sans effet pour nous : [wordcraft #4630](https://github.com/storytold/wordcraft/issues/4630)
et #4591 (iframe d'une autre origine, impression bloquée) ; notre iframe est de la même
origine.

## Questions et réponses

Posées à Benoit dans le fil le 2026-11-12 ; ✨ marque la recommandation. **Benoit a retenu
les trois recommandations le 2026-11-13 : 10 A, 11 A et 12 A** (décisions 10 à 12 du
[plan](plan.md)). La correction de Kazo OS (10 A) est lancée dans un fil à part ; la suite
est dans le [plan d'intégration](integration-plan.md).

**10. Comment servir la page de l'iframe dans le Kazo OS publié ?**

- **A. Corriger Kazo OS ✨** : le service worker sert les fichiers d'une app installée aussi
  pour une navigation sous `_content/`. Petite PR dans `Kazo-ca/Os` avec une ADR ; Kazo Word
  demande ensuite cette version de l'OS. La page a une vraie adresse (`?author=` marche) et
  toute app future peut avoir une page en iframe. Les tests du chantier 1 n'attendent pas : en
  développement la page est déjà servie.
- **B. Contourner dans l'app** : lire la page avec `fetch` et l'injecter dans l'iframe
  (`srcdoc`). Rien à changer dans l'OS, marche sur la 3.3.0. Mais la page n'a plus d'adresse :
  pas de `?author=` ni de `?webgl`, et le comportement varie selon le navigateur.
- **C. Les deux** : B tout de suite, A en parallèle, puis B retiré. Plus de travail, pour un
  problème qui ne touche pas les tests en développement.

**11. Comment mener la piste en amont, maintenant qu'on sait que des issues existent ?**

- **A. Se joindre aux issues existantes ✨** : un commentaire sur craft-libs #611
  (confirmation d'enregistrement) et sur #610 (Fichier › Ouvrir), une issue neuve seulement
  pour ce qui manque (touches réservées, réglages confiés à l'hôte), une issue par problème.
  Claude prépare les textes, Benoit les publie.
- **B. Ouvrir nos cinq issues comme prévu** : plus simple à suivre pour nous, mais le robot
  du dépôt les marquera comme doublons et les mainteneurs devront les relier.
- **C. Ne rien proposer en amont avant la v1** : zéro temps passé maintenant ; le proxy garde
  tous ses contournements plus longtemps.

**12. Le correcteur orthographique ne marche qu'en anglais dans la version web. Qu'en fait-on
pour la v1 ?**

- **A. L'accepter pour la v1 ✨** : noté comme limite connue dans la fiche du Store, on suit
  wordcraft #6140.
- **B. L'ajouter nous-mêmes au chantier 4** : modifier WordCraft et `craft-proof` pour
  charger le dictionnaire français. C'est un build modifié (marques ArtCraft à retirer) et un
  travail de plusieurs jours à refaire à chaque montée de version tant que l'amont ne l'a pas.
- **C. Attendre l'amont avant de publier dans le Store** : la v1 dépend d'un calendrier qu'on
  ne contrôle pas.

## Ordre conseillé

1. Question 10 décidée (A) : une PR dans `Kazo-ca/Os`, à part de la série `word/base`.
2. Chantier 1 comme essai avec critères : la page se charge dans l'iframe (en développement,
   puis dans un OS publié), WordCraft démarre sous COEP en WebGPU et en WebGL2, `ready`
   arrive, deux fenêtres tournent en même temps, le paquet est mesuré.
3. Chantiers 2, 3 et 4 en parallèle, avec les corrections ci-dessus.
4. Piste en amont selon la décision 11, sans rien bloquer.
