# CursorSeam

[日本語](../../README.md) · [English](../en/README.md) · [한국어](../ko/README.md) · [简体中文](../zh-Hans/README.md) · [繁體中文](../zh-Hant/README.md) · [Deutsch](../de/README.md) · **Français** · [Español](../es/README.md) · [Português (Brasil)](../pt-BR/README.md)

CursorSeam corrige la position du pointeur de la souris lorsqu’il passe d’un écran à l’autre sous Windows. L’application fonctionne avec au moins deux écrans, même s’ils ont des tailles ou des résolutions différentes.

![Disposition des écrans en portrait et en paysage. Les lignes vertes indiquent où le pointeur peut passer.](layout.png)

## Installation

Téléchargez puis exécutez `CursorSeam-…-Setup.exe` depuis la [dernière version](https://github.com/nekomimiyks/CursorSeam/releases/latest). À la fin de l’installation, choisissez **Oui** pour lancer l’application ou **Non** pour terminer sans la lancer. Les mises à jour conservent les réglages existants.

## Utilisation

1. Cliquez sur **Identifier les écrans** pour vérifier leur numéro, puis faites glisser les cadres pour reproduire leur disposition sur votre bureau.
2. Cliquez sur **Calibrer la taille**, puis faites glisser un bord ou un coin pour ajuster la taille réelle. Maintenez Maj enfoncée pendant le déplacement pour aligner les bords voisins.
3. Cliquez sur **Enregistrer les réglages**, puis faites passer le pointeur d’un écran à l’autre pour vérifier le résultat.

## Nouveautés de la version 1.1.0

Vous pouvez sauvegarder vos réglages dans un fichier et les restaurer avec les mêmes écrans. GitHub propose désormais un programme d’installation pour Windows.

## Sauvegarder et restaurer les réglages

Sélectionnez **Sauvegarde → Enregistrer la sauvegarde** pour créer un fichier `.cursorseam`. Pour le restaurer, sélectionnez **Sauvegarde → Restaurer une sauvegarde**, ouvrez le fichier et confirmez. La disposition, les dimensions, la zone de notification et la langue sont restaurées. Le démarrage avec Windows se règle séparément.

![Confirmation de restauration des réglages](backup.png)

## Licence

[Licence MIT](../../LICENSE)
