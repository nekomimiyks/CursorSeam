# CursorSeam

[日本語](../../README.md) · [English](../en/README.md) · [한국어](../ko/README.md) · [简体中文](../zh-Hans/README.md) · **繁體中文** · [Deutsch](../de/README.md) · [Français](../fr/README.md) · [Español](../es/README.md) · [Português (Brasil)](../pt-BR/README.md)

CursorSeam 是一款 Windows 應用程式，可校正滑鼠指標跨越螢幕邊界時的位置偏移。適用於連接兩台以上螢幕的電腦，包括螢幕尺寸或解析度不同的情況。

![橫向與直向螢幕的配置。綠線標示指標可以跨越的位置。](layout.png)

## 安裝

從[最新版本](https://github.com/nekomimiyks/CursorSeam/releases/latest)下載並執行 `CursorSeam-…-Setup.exe`。安裝完成後，選擇**是**立即啟動，或選擇**否**直接結束安裝程式。更新會保留現有設定。

## 使用方式

1. 點選**識別顯示器**查看編號，再拖曳螢幕邊框，使配置與桌面上的實際擺放一致。
2. 點選**校準實際尺寸**，拖曳邊緣或角落，讓大小與實際螢幕一致。按住 Shift 拖曳即可貼齊相鄰螢幕的邊緣。
3. 點選**儲存設定**，將滑鼠指標移過螢幕邊界確認效果。

## 1.1.1 更新內容

介面會跟隨 Windows 的淺色或深色模式。設定區新增開關、更寬的語言選項，以及獨立的備份和還原按鈕。更新時，GitHub 安裝程式可以關閉正在執行的 1.1.1 或更新版本。從 1.1.0 或更早版本升級時，請先從通知區域結束 CursorSeam。

## 備份與還原設定

選擇**儲存備份**，儲存 `.cursorseam` 檔案。還原時選擇**從備份還原**，選取檔案並確認。還原內容包括配置、實際尺寸、通知區域設定和語言。Windows 自動啟動請另外設定。

![還原設定的確認視窗](backup.png)

## 授權

[MIT 授權條款](../../LICENSE)
