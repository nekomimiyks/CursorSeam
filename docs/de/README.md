# CursorSeam

[日本語](../../README.md) · [English](../en/README.md) · [한국어](../ko/README.md) · [简体中文](../zh-Hans/README.md) · [繁體中文](../zh-Hant/README.md) · **Deutsch** · [Français](../fr/README.md) · [Español](../es/README.md) · [Português (Brasil)](../pt-BR/README.md)

CursorSeam korrigiert die Position des Mauszeigers beim Wechsel zwischen Bildschirmen unter Windows. Die App ist für zwei oder mehr Bildschirme mit unterschiedlichen Größen oder Auflösungen gedacht.

![Anordnung von Bildschirmen im Hoch- und Querformat. Grüne Linien zeigen mögliche Übergänge.](layout.png)

## Installation

Lade `CursorSeam-…-Setup.exe` aus der [neuesten Version](https://github.com/nekomimiyks/CursorSeam/releases/latest) herunter und führe die Datei aus. Wähle am Ende **Ja**, um die App zu starten, oder **Nein**, um das Setup ohne Start zu beenden. Deine Einstellungen bleiben bei Updates erhalten.

## Bedienung

1. Klicken Sie auf **Bildschirme erkennen**, um die Nummern zu prüfen. Ziehen Sie die Rahmen an die Positionen Ihrer Bildschirme auf dem Schreibtisch.
2. Wählen Sie **Größe kalibrieren** und ziehen Sie an einer Kante oder Ecke, bis die Größe stimmt. Halten Sie beim Ziehen die Umschalttaste gedrückt, um an einer benachbarten Kante einzurasten.
3. Klicken Sie auf **Einstellungen speichern** und bewegen Sie den Mauszeiger über eine Bildschirmkante, um das Ergebnis zu prüfen.

## Neu in 1.1.5

Der Einstellungsbereich wurde neu angeordnet. In breiten Fenstern stehen die Sprachauswahl und die Schaltflächen zum Sichern, Wiederherstellen und Löschen von Einstellungen in einer Zeile. In schmalen Fenstern rutschen die Schaltflächen in die nächste Zeile. Das App-Symbol hat jetzt einen leuchtend himmelblauen Hintergrund.

Unter **Über die App** unten rechts findest du die laufende Version und Links zu den öffentlichen Apps auf GitHub und im Microsoft Store. Kann die Store-App nicht starten, öffnet sich die Liste im Standardbrowser.

## Einstellungen sichern und wiederherstellen

Wähle **Sicherung speichern**, um eine `.cursorseam`-Datei zu speichern. Wähle zum Wiederherstellen **Sicherung wiederherstellen**, öffne die Datei und bestätige. Anordnung, Abmessungen, Infobereich und Sprache werden wiederhergestellt. Stelle den Windows-Autostart separat ein.

![Dialog zum Wiederherstellen der Einstellungen](backup.png)

## Automatische Update-Prüfung

Wenn **Automatisch nach Updates suchen** aktiviert ist, prüft die GitHub-Ausgabe kurz nach dem Start und einmal täglich während der Laufzeit auf neue Versionen. Wähle **Aktualisieren**, um das Installationsprogramm herunterzuladen, zu prüfen und zu starten. Bei der Installation kannst du eine Desktop-Verknüpfung anlegen.

## Lizenz

[MIT-Lizenz](../../LICENSE)
