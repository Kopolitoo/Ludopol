# Ludopol

Bibliothèque de jeux persos, à parcourir au regard (Tobii PCEye 5 avec
[Prunel](https://github.com/Kopolitoo/Prunel)).

Ludopol est un logiciel Windows local : un catalogue de jeux avec de gros boutons, sans petits
menus, pour retrouver les jeux jouables avec une poursuite oculaire, noter leur compatibilité et
les lancer.

## État

Étape 3 : import de la feuille de jeux. Au départ, le catalogue contient deux jeux **fictifs**
pour essayer l'interface ; ils sont retirés au premier import. Le bouton « Jouer » (étape 4)
viendra ensuite.

## Importer la feuille de jeux

Paramètres → « Choisir la feuille (.xlsx)… », puis choisir l'export Excel de la feuille Google
(Fichier → Télécharger → Microsoft Excel). L'export CSV ne convient pas : il perd les liens Steam
et les images.

- Les colonnes sont reconnues par leur titre : Jeu, Catégories, Essayé avec ma poursuite,
  Compatibilité, Ma note (1-5), Mes remarques, À publier, Lien Steam, Image, Description,
  Date de sortie, Développeur, Genres, Tags Steam, Prix, Français (et « Possédé sur Steam » si
  la colonne existe).
- Réimporter met à jour les données Steam, les catégories et les tags, mais ne remplace jamais
  ce qui a été rempli dans Ludopol (note, compatibilité, essayé, remarques, à publier, possédé).
- Deux lignes avec le même lien Steam : celle dont le nom correspond au texte du lien garde les
  données Steam, l'autre est importée sans, et son nom est signalé pour corriger la feuille.

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

Le bouton « Paramètres » (colonne de gauche) règle au regard le plein écran, le survol, son délai, la taille
du texte, des boutons et des tuiles, et la langue. Tout est enregistré dans `settings.json`,
qu'on peut aussi modifier à la main, Ludopol fermé :


| Réglage | Par défaut | Rôle |
| --- | --- | --- |
| `Language` | `fr` | Langue de l'interface (`fr` ou `en`). |
| `FullScreen` | `true` | Plein écran ; `false` : fenêtre ordinaire, déplaçable et redimensionnable. |
| `TileWidth`, `TileHeight` | 320, 300 | Taille des tuiles de jeux. |
| `ButtonMinSize` | 150 | Taille minimale de tout bouton. |
| `Spacing` | 24 | Écart entre deux boutons. |
| `FontSize`, `TitleFontSize` | 28, 40 | Taille du texte et des titres. |
| `HoverActivation` | `true` | Un bouton s'active seul quand le curseur reste dessus (mode mouvement de Prunel) ; une jauge jaune se remplit pendant le délai. `false` : clic seulement. |
| `HoverDelayMs` | 1000 | Délai de l'activation au survol, en millisecondes. |

Les tailles sont en unités WPF (1/96 de pouce) : avec une mise à l'échelle Windows de 200 %,
une unité fait 2 pixels de l'écran 4K.

## Licence

MIT, voir [LICENSE](LICENSE).
