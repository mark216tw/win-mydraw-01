# MyDraw 繪圖板

MyDraw 是使用 WinUI 3 與 Win2D 打造的 Windows 原生學生繪圖工具，著重快速啟動、直覺筆刷、低延遲手寫與最大化畫布空間。專案以 .NET 10 開發，採 MIT License 釋出。

- 目前版本：**0.14.1**
- 平台：Windows 10 1809（build 17763）以上，支援 **x64** 與 **arm64**
- 授權：[MIT License](LICENSE)

## 目錄

- [專案簡介](#專案簡介)
- [主要功能](#主要功能)
- [系統需求](#系統需求)
- [下載與安裝](#下載與安裝)
- [快速開始](#快速開始)
- [快捷鍵](#快捷鍵)
- [文件索引](#文件索引)
- [專案結構](#專案結構)
- [建置與測試](#建置與測試)
- [發行](#發行)
- [資料與隱私](#資料與隱私)
- [已知限制](#已知限制)
- [授權](#授權)

## 專案簡介

MyDraw 的目標是提供一個啟動快速、操作集中、適合課堂筆記與日常塗鴉的輕量繪圖程式。介面以單一畫布為主，工具列可置於左側或右側，常用筆刷與筆刷設定集中在同一區塊，減少尋找功能的時間。

本專案採用下列技術：

- .NET 10 / C#（nullable、file-scoped namespace）
- WinUI 3（Windows App SDK 1.8）與 CommunityToolkit.Mvvm
- Win2D（Microsoft.Graphics.Win2D）GPU 加速繪圖
- Microsoft.Extensions.Hosting 依賴注入與記錄

## 主要功能

- **畫布與介面**：WinUI 3 系統選單列、可切換左／右側的工具列、Win2D 極大化畫布、沉浸模式與全螢幕。
- **筆刷工具**：鋼筆、鉛筆、原子筆、馬克筆、螢光筆、書法筆、噴槍、蠟筆、橡皮擦與平移工具。
- **筆刷設定**：粗細、不透明度、柔和度、流量、噴槍霧狀／顆粒模式、壓力曲線與壓感開關；每支筆刷分開記憶。
- **最愛畫筆**：可將目前筆刷的顏色與設定儲存為具名最愛，支援套用、重新命名、刪除與排序。
- **圖層**：新增、選取、重新命名、顯示、鎖定、透明度、上下移動與刪除；新增／刪除／排序可復原。
- **復原／重做**：筆畫與圖層操作支援復原；復原最後一筆只重建受影響圖塊。
- **專案檔案**：`.mydraw` 可編輯專案（ZIP + JSON），支援開啟、儲存、另存新檔、最近開啟與 `.bak` 備份。
- **PNG 匯出**：含進度顯示與取消。
- **自動復原**：每 30 秒寫入復原快照；異常結束後可選擇復原或捨棄，7 天後自動清理。
- **檔案關聯與單一實例**：可建立 `.mydraw` 關聯；再次開啟檔案會轉送到既有視窗。
- **外觀**：跟隨系統／淺色／深色，以及護眼黃、海洋藍、森林綠、薰衣草紫、中性灰主題。
- **在地化**：內建繁體中文與英文資源，介面語言跟隨 Windows 語言。
- **無障礙**：主要控制項提供 AutomationId、AutomationProperties 與即時狀態朗讀。

## 系統需求

### 使用 GitHub Releases 發行版

本專案提供的 Release zip 為 **framework-dependent（non-self-contained）**，需要先在系統安裝：

- Windows 10 1809（17763）或更新版本
- **.NET 10 Desktop Runtime**（x64 或 arm64，依系統架構）
- **Windows App SDK 1.8 Runtime**（x64 或 arm64）

下載位置：

- .NET 10 下載：<https://dotnet.microsoft.com/download/dotnet/10.0>
- Windows App SDK 下載：<https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads>

若缺少執行階段，啟動時會顯示 Windows App SDK 或 .NET 的錯誤訊息，請安裝對應架構的 Runtime 後再啟動。

### 開發環境

- .NET 10 SDK（本專案使用 10.0.403 驗證）
- Windows 10/11
- Visual Studio 2026 或可建置 WinUI 3 的工具（可選）
- x64 開發機可建置 x64；若要建置 arm64 需還原 arm64 執行階段套件（`dotnet publish` 會自動處理）

## 下載與安裝

1. 前往 [GitHub Releases](https://github.com/mark216tw/win-mydraw-01/releases)。
2. 依系統架構下載：
   - `MyDraw-0.14.1-win-x64.zip`
   - `MyDraw-0.14.1-win-arm64.zip`
3. 解壓縮到任意資料夾（不需系統管理員權限）。
4. 先安裝上方列出的 .NET 10 Desktop Runtime 與 Windows App SDK 1.8 Runtime。
5. 執行 `MyDraw.exe`。

> 目前發行檔未做程式碼簽章，Windows SmartScreen 可能顯示警告；請確認檔案來自本專案 Releases 後再執行。

## 快速開始

1. 啟動 MyDraw，預設會建立 1080 × 1080 畫布。
2. 從左側（或右側）工具列選擇筆刷，調整粗細與顏色後直接在畫布上繪圖。
3. 使用 `Ctrl+S` 儲存 `.mydraw` 專案；使用 `Ctrl+E` 匯出 PNG。
4. 需要多層次編輯時，開啟「圖層」面板新增圖層。
5. 程式每 30 秒自動寫入復原快照；若不慎關閉或當機，下次啟動可選擇復原。

詳細操作請見 [使用指南](docs/使用指南.md)。

## 快捷鍵

| 快捷鍵 | 功能 |
| --- | --- |
| `Ctrl+Z` | 復原 |
| `Ctrl+Y` / `Ctrl+Shift+Z` | 重做 |
| `Ctrl+N` | 新增空白畫紙 |
| `Ctrl+O` | 開啟 `.mydraw` 專案 |
| `Ctrl+S` | 儲存專案 |
| `Ctrl+Shift+S` | 另存新檔 |
| `Ctrl+E` | 匯出 PNG |
| `Ctrl+Alt+C` | 變更畫紙大小 |
| `Ctrl+G` | 顯示／隱藏輔助格線 |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | 放大／縮小／重設縮放 |
| `Ctrl+,` | 外觀與色彩主題設定 |
| `P` / `M` / `E` / `H` | 鋼筆／螢光筆／橡皮擦／平移 |
| `[` / `]` | 減少／增加筆刷粗細 |
| `Space` + 拖曳 | 暫時平移畫布 |
| 滑鼠右鍵拖曳 | 平移畫布 |
| 滑鼠滾輪 | 縮放畫布 |
| `Tab` | 切換沉浸模式 |
| `F11` | 全螢幕 |
| `Esc` | 離開全螢幕或關閉對話框 |

## 文件索引

| 文件 | 內容 |
| --- | --- |
| [docs/專案說明.md](docs/專案說明.md) | 專案背景、目標、範圍與名詞定義 |
| [docs/使用指南.md](docs/使用指南.md) | 安裝、介面操作、工具、圖層、專案與疑難排解 |
| [docs/系統架構與技術文件.md](docs/系統架構與技術文件.md) | 技術基線、模組、渲染、檔案格式、測試與發行 |
| [docs/系統設計文件.md](docs/系統設計文件.md) | 設計目標、關鍵決策、流程與資料模型 |
| [docs/開發指南.md](docs/開發指南.md) | 開發環境、建置、測試分級、版本與發行流程 |
| [docs/DEV_LOG.md](docs/DEV_LOG.md) | 逐次開發紀錄與已知限制 |
| [CHANGELOG.md](CHANGELOG.md) | 版本變更紀錄 |
| [RELEASE_NOTES.md](RELEASE_NOTES.md) | 目前版本的使用者向發行說明 |
| [LICENSE](LICENSE) | MIT License |

## 專案結構

```text
src/MyDraw/
  Views/          XAML 視窗與畫布輸入／繪製 code-behind
  ViewModels/     MVVM ViewModel（MainViewModel）
  Models/         筆畫、圖層、設定、筆刷預設等資料模型
  Services/       檔案、設定、復原、主題、關聯、轉送等服務
  Rendering/      圖層圖塊快取與即時筆畫快取
  Helpers/        筆刷渲染、取樣、外框、視埠、啟動參數與當機記錄
  Styles/         共用樣式與主題資源字典
  Strings/        zh-Hant / en-US 資源
  Assets/         圖示與圖片
tests/MyDraw.Tests/
tools/             UI、筆刷與開檔／匯出驗證腳本
docs/              專案文件與開發紀錄
```

## 建置與測試

```powershell
dotnet restore MyDraw.sln
dotnet build MyDraw.sln -c Debug -p:Platform=x64
dotnet test MyDraw.sln -c Debug -p:Platform=x64
dotnet format MyDraw.sln --verify-no-changes --no-restore
```

開發時只建置 Debug；Release 僅在明確發行時使用。詳細測試分級與驗證腳本請見 [開發指南](docs/開發指南.md)。

## 發行

### non-self-contained（GitHub Release 使用）

```powershell
# x64
dotnet publish src\MyDraw\MyDraw.csproj -c Release -p:Platform=x64 -r win-x64 `
  --self-contained false -p:WindowsAppSDKSelfContained=false `
  -p:WindowsPackageType=None -o publish\win-x64

# arm64
dotnet publish src\MyDraw\MyDraw.csproj -c Release -p:Platform=arm64 -r win-arm64 `
  --self-contained false -p:WindowsAppSDKSelfContained=false `
  -p:WindowsPackageType=None -o publish\win-arm64
```

### self-contained（可選）

若要打包成不依賴系統 Runtime 的版本，將上述參數改為：

```powershell
--self-contained true -p:WindowsAppSDKSelfContained=true
```

self-contained 檔案較大，但不需要預先安裝 .NET 與 Windows App SDK Runtime。

## 資料與隱私

MyDraw 不收集遙測、不上傳畫布內容，也不含網路同步功能。所有資料都存放在本機：

| 資料 | 位置 |
| --- | --- |
| 使用者設定 | `%LOCALAPPDATA%\MyDraw\settings.json` |
| 自動復原快照 | `%LOCALAPPDATA%\MyDraw\Recovery\` |
| 當機記錄 | `%LOCALAPPDATA%\MyDraw\logs\` |
| 專案檔 | 使用者選擇的 `.mydraw` 路徑 |
| `.mydraw` 檔案關聯 | `HKCU\Software\Classes`（僅在使用者按下設定時寫入） |

## 已知限制

- 圖層改名、透明度、顯示、鎖定與畫布尺寸變更目前**不納入**復原／重做。
- 匯出格式目前僅支援 PNG；PDF／JPG 尚未實作。
- 未封裝版本的介面語言跟隨 Windows 語言，不支援執行期手動切換。
- 發行檔未簽章，Windows SmartScreen 可能提示警告。
- arm64 版本已可建置與發行，但主要開發驗證環境為 x64。

## 授權

本專案採用 [MIT License](LICENSE) 釋出，Copyright (c) 2026 mark216tw。
