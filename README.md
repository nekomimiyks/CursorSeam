# CursorSeam

**日本語** · [English](docs/en/README.md) · [한국어](docs/ko/README.md) · [简体中文](docs/zh-Hans/README.md) · [繁體中文](docs/zh-Hant/README.md) · [Deutsch](docs/de/README.md) · [Français](docs/fr/README.md) · [Español](docs/es/README.md) · [Português (Brasil)](docs/pt-BR/README.md)

カーソルがモニターの境目を越えるとき、上下や左右にずれるのを補正する Windows アプリです。サイズや解像度が異なるモニターを2台以上つないで使います。

![縦長と横長のモニターを並べた配置ボード。緑の線はカーソルが通れる範囲](docs/layout.png)

## 入手方法

[最新版のリリース](https://github.com/nekomimiyks/CursorSeam/releases/latest)から `CursorSeam-…-Setup.exe` をダウンロードして実行します。インストール完了時に **はい** を選ぶと起動し、**いいえ** を選ぶと起動せずに終了します。更新しても既存の設定は引き継がれます。

## 使い方

1. **画面を識別**で番号を確認し、枠をドラッグして机の上と同じ配置にします。
2. **実寸を合わせる**を押し、枠の辺や角をドラッグしてモニターの大きさを合わせます。Shift を押しながらドラッグすると、隣の画面の辺に吸着します。
3. **設定を保存**を押し、画面の境目でカーソルの動きを確かめます。

## 1.1.1 の変更

Windows の配色に連動するダークモードを追加し、設定欄を ON/OFF スイッチ、幅の広い言語選択、バックアップ・復元ボタンに変更しました。GitHub 版は、1.1.1 以降が起動中のときに更新インストーラーが安全に終了します。1.1.0 以前からの更新では、先に通知領域から CursorSeam を終了してください。

## 設定のバックアップと復元

**設定をバックアップ**で `.cursorseam` ファイルを保存します。戻すには **バックアップから復元**でそのファイルを選び、確認します。配置・実寸・常駐・言語が復元されます。Windows 起動時の自動起動は個別に設定してください。

![設定をバックアップから復元する確認画面](docs/backup.png)

## ライセンス

[MITライセンス](LICENSE)
