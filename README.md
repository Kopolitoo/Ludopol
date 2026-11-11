# Ludopol

Bibliothèque de jeux persos, à parcourir au regard (Tobii PCEye 5 avec
[Prunel](https://github.com/Kopolitoo/Prunel)).

Ludopol est un logiciel Windows local : un catalogue de jeux avec de gros boutons, sans petits
menus, pour retrouver les jeux jouables avec une poursuite oculaire, noter leur compatibilité et
les lancer.

## État

Étape 5 : synchronisation avec la bibliothèque Steam (jeux achetés). Au départ, le catalogue contient deux jeux
**fictifs** pour essayer l'interface ; ils sont retirés au premier import de la feuille.

## Importer la feuille de jeux

Paramètres → « Catalogue » → « Choisir la feuille (.xlsx)… », puis choisir l'export Excel de la feuille Google
(Fichier → Télécharger → Microsoft Excel). L'export CSV ne convient pas : il perd les liens Steam
et les images.

- Les colonnes sont reconnues par leur titre : Jeu, Catégories, Essayé avec ma poursuite,
  Compatibilité, Ma note (1-5), Mes remarques, À publier, Lien Steam, Image, Description,
  Date de sortie, Développeur, Genres, Tags Steam, Prix, Français (et « Possédé sur Steam » si
  la colonne existe).
- La feuille liste des jeux réputés jouables au regard : un jeu sans compatibilité dans la
  feuille devient « Supposée (pas encore essayée) » (jauge bleu-gris). Réimporter la feuille
  l'applique aussi aux jeux déjà importés restés « Inconnue ».
- Réimporter met à jour les données Steam, les catégories et les tags, mais ne remplace jamais
  ce qui a été rempli dans Ludopol (note, compatibilité, essayé, remarques, à publier, possédé).
- Deux lignes avec le même lien Steam : celle dont le nom correspond au texte du lien garde les
  données Steam, l'autre est importée sans, et son nom est signalé pour corriger la feuille.

## Jouer

Ludopol regarde quels jeux sont installés au démarrage, après un import, et avec Paramètres → « Catalogue » →
« Rechercher les jeux installés ». La fiche d'un jeu vérifie aussi à chaque affichage, et quand on
revient dans Ludopol (après une installation dans Steam, par exemple).

- **Jeux Steam** : Ludopol lit les fichiers de Steam (dossier trouvé dans le registre, puis
  `steamapps\libraryfolders.vdf` pour toutes les bibliothèques, et un `appmanifest_<numéro>.acf`
  par jeu). Steam n'a pas besoin d'être ouvert. Un jeu en cours de téléchargement ne compte pas.
- **« Jouer »** demande une confirmation (« Oui, jouer » / « Non ») pour qu'un regard ne lance pas
  un jeu par erreur, puis lance le programme choisi s'il y en a un, sinon demande à Steam de
  lancer le jeu.
- **« Installer avec Steam »** (jeu Steam pas installé) ouvre la fenêtre d'installation de Steam.
- **« Choisir le programme du jeu… »** : pour un jeu hors Steam, ou pour lancer un jeu Steam
  autrement. On choisit un `.exe` ou un raccourci (`.lnk`, `.url`) ; le jeu compte alors comme
  installé tant que ce fichier existe.

## Synchroniser avec Steam (jeux achetés)

Les fichiers de Steam sur l'ordinateur ne disent que ce qui est installé. La liste des jeux
achetés est demandée à Steam par Internet, dans Paramètres → « Bibliothèque Steam » :

1. Le compte Steam est trouvé tout seul (le dernier utilisé dans Steam sur cet ordinateur ; s'il
   y en a plusieurs, on choisit).
2. Il faut une **clé Steam** (gratuite, une seule fois) : bouton « Obtenir ma clé Steam » → page
   de Steam, nom de domaine au choix (par exemple `localhost`), puis copier la clé dans le champ
   (ou la dicter). Elle est enregistrée dans `settings.json` : la garder pour soi.
3. « Synchroniser avec Steam » : chaque jeu du catalogue qui a un numéro Steam passe à
   « Possédé sur Steam » oui ou non ; les jeux sans numéro Steam gardent ce qui a été mis à la
   main. Les jeux achetés absents du catalogue s'affichent en boutons : un bouton ajoute
   ce jeu seul, « Ajouter les N jeux d'un coup » les ajoute tous (nom, numéro et image Steam),
   dans la catégorie « À trier (Steam) ». Un import de
   la feuille complète ensuite ceux qui y sont.

Les jeux ajoutés depuis Steam sont ensuite complétés depuis le magasin Steam, en arrière-plan
(environ 1,5 s par jeu) : description, développeur, date, genres, prix, français et tags. Seuls
les champs vides sont remplis ; la catégorie reste « À trier (Steam) ». Le bouton « Compléter
les fiches depuis le magasin Steam » relance cela pour tous les jeux Steam sans description, et
la fiche d'un tel jeu se complète aussi à son ouverture. Sur la fiche de tout jeu présent sur Steam (acheté ou
non), « Compléter la fiche depuis Steam » fait de même à la demande.

Si Steam ne donne pas la liste : dans Steam, Profil → Modifier le profil → Confidentialité,
mettre « Détails des jeux » sur Public.

Dans les listes, le filtre « Installés » passe par « Installés seulement » et « Pas installés » ;
avec « Possédés sur Steam », on voit les jeux achetés mais pas installés.

## Rechercher

Accueil → « Rechercher un jeu » : un champ en haut (clavier de Prunel ou Dragon) ; la liste se
réduit à chaque lettre, sans tenir compte des majuscules ni des accents.

## Listes et navigation

Le titre d'une liste donne son nombre de jeux, par exemple « Tous les jeux (316) ». À droite du
titre, « Tri : … » ouvre le choix du critère (nom, date d'ajout, note, compatibilité) et le
bouton voisin inverse le sens (A → Z / Z → A, plus récents d'abord…). Les jeux sans note
restent à la fin. Les pages Catégories, Tags et Développeurs ont aussi leur nombre et leur tri
(par nom ou par nombre de jeux ; les tags commencent par les plus utilisés).

Partout où le contenu défile, une fine jauge à droite montre où l'on est dans la page, et le
clavier fonctionne : Page suivante / Espace, Page précédente, Début, Fin, flèches haut et bas.
La touche Retour arrière fait comme le bouton « Retour ». Pendant la saisie dans un champ (la
recherche), seules Page suivante et Page précédente font défiler : les autres touches servent au texte.

## Fiche d'un jeu

Les catégories, les tags Steam et le développeur sont des boutons : chacun ouvre la liste des
jeux qui l'ont (« Retour » ramène à la fiche). « Modifier les catégories », au bout de la rangée des
catégories, ouvre la liste de toutes les catégories : chaque bouton s'allume (bleu) ou s'éteint,
un champ permet d'en créer une nouvelle, « Terminé » enregistre. Choisir une vraie catégorie
retire « À trier (Steam) ». Attention : réimporter la feuille remet les catégories de la feuille
pour les jeux qui y sont.

Un jeu Steam pas installé propose « Installer avec Steam » s'il est possédé. Tout jeu présent
sur Steam a « Voir le jeu sur le magasin Steam » (page dans Steam, ou dans le navigateur sans
Steam). « Choisir le programme du jeu… » n'est proposé que pour un jeu non possédé sur Steam.

## Compiler et lancer

1. Ouvrir `Ludopol.slnx` (ou `Ludopol.csproj`) dans Visual Studio 2022, avec la charge de travail
   « Développement .NET Desktop » (.NET 8).
2. Lancer avec F5.

Ludopol s'ouvre en plein écran. Pour le fermer : bouton « Quitter » en haut à droite.

## Fichiers de Ludopol

Dans `%LocalAppData%\Ludopol` :

| Fichier | Contenu |
| --- | --- |
| `settings.json` | Réglages : langue, tailles des boutons et du texte, activation au survol. À modifier Ludopol fermé. |
| `ludopol.db` | Le catalogue (base SQLite). Le supprimer remet les deux jeux fictifs. |
| `ludopol.log` | Ce qui s'est passé, pour chercher une erreur. |
| `Categories\` | Facultatif : mes propres images de catégories, nommées comme la catégorie (`.png`, `.jpg`). Les caractères interdits dans un nom de fichier deviennent `_` : `Romans visuels _ Narratif.png`. Sans image, la carte prend celle d'un jeu de la catégorie. |

## Réglages

Le bouton « Paramètres » (colonne de gauche) ouvre un menu de grandes cases (Écran et langue,
Tailles, Survol, Catalogue, Bibliothèque Steam), chacune sur une page sans défilement. On y règle au regard le plein écran, le survol, son délai, la taille
du texte, des boutons et des tuiles, et la langue. Tout est enregistré dans `settings.json`,
qu'on peut aussi modifier à la main, Ludopol fermé :


| Réglage | Par défaut | Rôle |
| --- | --- | --- |
| `Language` | `fr` | Langue de l'interface (`fr` ou `en`). |
| `Theme` | `dark` | Couleurs : `dark` (sombre), `light` (clair) ou `system` (comme Windows). |
| `FullScreen` | `true` | Plein écran ; `false` : fenêtre ordinaire, déplaçable et redimensionnable. |
| `TileWidth`, `TileHeight` | 320, 300 | Taille des tuiles de jeux. |
| `ButtonMinSize` | 150 | Taille minimale de tout bouton. |
| `Spacing` | 24 | Écart entre deux boutons. |
| `FontSize`, `TitleFontSize` | 28, 40 | Taille du texte et des titres. |
| `HoverActivation` | `true` | Un bouton s'active seul quand le curseur reste dessus (mode mouvement de Prunel) ; une jauge jaune se remplit pendant le délai. `false` : clic seulement. |
| `HoverDelayMs` | 1000 | Délai de l'activation au survol, en millisecondes. |
| `SteamApiKey` | vide | Clé Steam pour la synchronisation des jeux achetés. |
| `SteamAccountId` | vide | Compte Steam synchronisé (vide : le dernier utilisé sur l'ordinateur). |

Les tailles sont en unités WPF (1/96 de pouce) : avec une mise à l'échelle Windows de 200 %,
une unité fait 2 pixels de l'écran 4K.

## Licence

MIT, voir [LICENSE](LICENSE).
