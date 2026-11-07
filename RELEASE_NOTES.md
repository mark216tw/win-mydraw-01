# MyDraw 繪圖板 0.14.2 版本說明

## 下載

| 系統架構 | 檔案 |
| --- | --- |
| x64（Intel／AMD） | `MyDraw-0.14.2-win-x64.zip` |
| arm64（Windows on ARM） | `MyDraw-0.14.2-win-arm64.zip` |

本版本為 **framework-dependent（non-self-contained）**，解壓縮後需先安裝：

- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)（依架構選擇 x64 或 arm64）
- [Windows App SDK 1.8 Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)（依架構選擇 x64 或 arm64）

安裝完成後執行 `MyDraw.exe`。發行檔未做程式碼簽章，SmartScreen 可能顯示警告。

## 本次修正

- **畫布 tooltip 修正**：啟動程式或滑過畫布時，不再自動顯示 `Ctrl+Z` 等快速鍵提示；復原／重做快速鍵功能不變。
- **復原更正確**：復原最後一筆不會再讓同一圖塊的其他筆畫消失，半透明筆刷也不會在圖塊邊界出現接縫。
- **重做更可靠**：復原後再畫新筆畫會正確清除重做；繪圖中按復原也能正常收尾。
- **存檔狀態更準確**：復原回上次儲存狀態後，標題的未儲存標記會消失，關閉時不再誤判。
- **快速鍵**：`Ctrl+Z` 復原、`Ctrl+Y` 或 `Ctrl+Shift+Z` 重做；文字輸入框維持原生復原行為。
- **arm64 發行**：新增 arm64 建置與 non-self-contained 發行設定。

## 主要功能

- WinUI 3 + Win2D 極大化畫布，支援滑鼠、觸控與手寫筆壓感／傾斜。
- 鋼筆、鉛筆、原子筆、馬克筆、螢光筆、書法筆、噴槍、蠟筆、橡皮擦與平移工具。
- 筆刷粗細、不透明度、柔和度、流量、噴槍模式與壓力曲線；每支筆刷分開記憶。
- 20 色色票、自訂調色盤與可排序的最愛畫筆。
- 多圖層新增、改名、顯示、鎖定、透明度、排序與刪除。
- `.mydraw` 專案開啟／儲存／另存、最近開啟、`.bak` 備份與 PNG 匯出。
- 每 30 秒自動復原、啟動復原提示、7 天清理。
- 單一實例、`.mydraw` 檔案關聯與命令列開檔。
- 淺色／深色／跟隨系統、5 種色彩主題、繁體中文與英文介面。
- 沉浸模式、全螢幕、格線、縮放與平移。

## 已知限制

- 圖層改名、透明度、顯示、鎖定與畫布尺寸變更不納入復原／重做。
- 匯出僅支援 PNG；PDF／JPG 尚未實作。
- 未封裝版本介面語言跟隨 Windows 語言，不支援執行期手動切換。
- 發行檔未簽章。
- arm64 已可建置與發行，主要驗證環境仍為 x64。

## 授權

MIT License，Copyright (c) 2026 mark216tw。
