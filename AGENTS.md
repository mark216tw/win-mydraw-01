# Windows .NET 10 / C# 桌面應用開發規範（簡化版）

本檔是簡單型 Windows 桌面應用的共用開發筆記。開發新專案時複製到專案根目錄，opencode 會自動讀取；專案專屬規則寫在文末。

---

## 1. 技術基線

| 項目 | 版本 |
| --- | --- |
| 語言 | C#（含 nullable reference types、file-scoped namespace） |
| 執行環境 | .NET 10（`net10.0-windows10.0.19041.0`） |
| UI | WinUI 3（Windows App SDK）+ MVVM（`Microsoft.Extensions.Hosting` 啟動） |
| 平台 | Windows 10 1809（build 17763）以上；Windows App SDK 支援至 Windows 10 1809 |
| 架構 | x64（必要時提供 AnyCPU；不支援 ARM 需於補充說明） |
| 建置 | .NET SDK 風格專案（`*.csproj`）+ Central Package Management（`Directory.Packages.props`） |

- UI 框架固定為 WinUI 3（`Microsoft.WindowsAppSDK`），不要混用 WPF／WinForms 控制項。
- MVVM 使用 `CommunityToolkit.Mvvm`（`ObservableObject`、`RelayCommand`）；不要手寫 `INotifyPropertyChanged`。
- 套件版本一律集中寫在 `Directory.Packages.props`，不要在 `*.csproj` 寫死版本。
- 不要任意升級 .NET SDK／套件；升級需在 `docs/DEV_LOG.md` 說明原因。
- 啟用 `Nullable` 與 `TreatWarningsAsErrors`；不關閉警告。

## 2. 專案結構

```
src/<AppName>/                 # 應用程式專案
  Views/                       # XAML 頁面／視窗與使用者控制項
  ViewModels/                  # ViewModel（MVVM）
  Models/                      # 資料模型
  Services/                    # 商業邏輯、I/O、外部整合
  Helpers/                     # 轉換器、擴充方法、共用工具
  Assets/                      # 圖示、圖片、字型
  Strings/                     # 多語系資源（.resw，預設 zh-Hant）
  Styles/                      # 樣式與主題資源字典（.xaml）
  App.xaml / App.xaml.cs
  Package.appxmanifest         # MSIX 封裝設定（打包模式需要）
tests/<AppName>.Tests/         # 單元測試（選用，建議 xUnit）
<AppName>.sln
Directory.Build.props          # 共用編譯設定
Directory.Packages.props       # 套件版本集中管理
README.md
CHANGELOG.md
RELEASE_NOTES.md
docs/DEV_LOG.md                # 開發過程紀錄（必要）
```

- 命名空間用反向網域命名（例：`Com.Example.AppName`）。
- 使用者可見文案放 `Strings/**/*.resw`，透過 `x:Uid` 或 `ResourceLoader` 取得，不要寫死在 XAML／程式碼；預設繁體中文。
- 一個檔案一個公開型別，檔名與型別同名。

## 3. 程式碼規範

- 4 空格縮排；類名／方法用 PascalCase、區域變數／參數用 camelCase、常數用 PascalCase、私有欄位用 `_camelCase`。
- 非同步方法以 `Async` 結尾並回傳 `Task`／`Task<T>`；函式庫層加 `ConfigureAwait(false)`，UI 層不加。
- 不在 UI 執行緒做 I/O；長工作放 `Task.Run` 或 `async` 流程並回報進度。
- 依賴注入用 `Microsoft.Extensions.DependencyInjection`，在 `App.xaml.cs` 註冊服務；不要在 View 直接 new 服務。
- 設定用 `IOptions` + `appsettings.json`（或 `Microsoft.Extensions.Configuration`）；使用者設定用 `Properties.Settings` 或 JSON 檔（`%AppData%/<AppName>`）。
- 資料儲存：結構化資料用 SQLite + EF Core，其餘用檔案／JSON。
- 例外要記錄並處理，不要吞例外；記錄用 `Microsoft.Extensions.Logging`，正式版不輸出到主控台。
- 不要引入未在 `Directory.Packages.props` 宣告的依賴。

## 4. 共用界面

- 以 WinUI 3 原生的 Fluent 控制項（`NavigationView`、`ListView`、`Card` 風格 `Border`、`ToggleSwitch`）搭配共用的 `Style`，樣式放 `Styles/`。
- 間距用 4 的倍數（WinUI 單位為 DIP）；圓角 8。
- 顏色與筆刷定義在 `Styles/Themes/` 的資源字典，不要在 XAML 寫死 `#FF...`；以 `ThemeResource`（跟隨主題）或 `StaticResource` 引用。
- 字型用共用的 `TextBlock` 樣式與 `FontSize` 資源，不要逐處指定。
- 空狀態、載入中、錯誤都要有畫面（可用 `InfoBar`、`ProgressRing`）；刪除等破壞性操作需 `ContentDialog` 二次確認。
- 可點擊元素要有 `AutomationProperties.Name`，圖示按鈕需有 `ToolTipService.ToolTip`。
- 視窗要有合理的 `MinWidth`／`MinHeight`，關閉前若有未儲存變更需提示。

## 5. 常用功能（需要時再實作）

- **顯示模式**：淺色／深色／跟隨系統，設定 `Window.Content` 的 `ElementTheme`（`Application.Current.RequestedTheme` 只影響初始值）。
- **設定頁**：`IOptions` + 設定檔，或 `NavigationView` 內的 Settings 頁。
- **About**：顯示 `Version`、`FileVersion`、授權與隱私說明（由組件屬性讀取）。
- **通知**：使用 Windows App SDK 的 `AppNotificationBuilder`／`AppNotificationManager`。
- **單一實例**：用 `Mutex` 或具名 Pipe 避免多開，第二次啟動時帶回原視窗。
- **系統匣**：WinUI 3 無原生支援，需要時透過 `System.Windows.Forms.NotifyIcon` 或 `Shell_NotifyIcon` 互通並提供結束選單。
- **標題列**：可用 `AppWindowTitleBar` 自訂標題列與按鈕顏色，或 `ExtendsContentIntoTitleBar` 自繪。

## 6. 建置與版本

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
  <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>
```

- 開發與測試一律使用 `Debug`；不要自動建置或發佈 `Release`，只有使用者明確要求時才執行。
- 版本放在 `Directory.Build.props` 的 `Version`／`AssemblyVersion`／`FileVersion`；`Version` 採 `主.次.修`。
- 每次功能變更遞增版本；`AssemblyVersion` 主次版變更時才調整。
- 發佈（僅限明確要求）：未封裝模式用 `dotnet publish -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None`；需要安裝檔時再使用 MSIX 打包。

### 測試分級（依風險）

- **Tier 1（開發迭代）**：每次會影響編譯或 XAML 的修改後，只執行
  `dotnet build MyDraw.sln -c Debug -p:Platform=x64`。不要每改一次就重跑完整 smoke 或 format。
- **Tier 2（功能完成）**：
  - 執行一次 `dotnet format MyDraw.sln --verify-no-changes --no-restore`。
  - 執行 `dotnet test MyDraw.sln -c Debug -p:Platform=x64`（`tests/MyDraw.Tests`）。
  - 筆刷／渲染／輸入變更：執行 `tools/Verify-BrushEffects.ps1`。
  - 專案存讀／檔案選擇器變更：執行 `tools/Verify-OpenExport.ps1`。
  - 設定／圖層／工具列／AutomationId 變更：執行 `tools/Verify-UiSmoke.ps1` 或對應測試段落。
  - 僅文件／字串變更：未改 `.resw`／XAML 時可只做必要檢查；有改 `.resw` 仍需 `dotnet build`。
- **Tier 3（版本更新或發佈前）**：執行完整 `tools/Verify-UiSmoke.ps1`；渲染變更再補手動視覺確認。
- 測試專案為 `tests/MyDraw.Tests`，已納入 Tier 2；新增純邏輯時優先補測試。
- 常用指令：
  ```powershell
  dotnet build MyDraw.sln -c Debug -p:Platform=x64
  dotnet test MyDraw.sln -c Debug -p:Platform=x64
  dotnet format MyDraw.sln --verify-no-changes --no-restore
  .\tools\Verify-BrushEffects.ps1 -ExecutablePath .\src\MyDraw\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\MyDraw.exe
  .\tools\Verify-OpenExport.ps1 -ExecutablePath .\src\MyDraw\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\MyDraw.exe
  .\tools\Verify-UiSmoke.ps1 -ExecutablePath .\src\MyDraw\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\MyDraw.exe
  ```
- 完整 `Verify-UiSmoke.ps1` 是版本／發佈前門檻，不是每次功能完成的必要步驟。


## 7. 開發過程紀錄（docs/DEV_LOG.md）

每次開發工作都要更新 `docs/DEV_LOG.md`，不要事後補寫。內容包含：

1. **本次目標**：要做什麼、為什麼。
2. **變更內容**：改了哪些檔案／功能。
3. **遇到的問題與解法**：錯誤訊息、嘗試過的方法、最後怎麼解。
4. **待辦／已知限制**：尚未完成或未驗證的部分。
5. **日期與版本**：對應的版本號。

格式建議（最新紀錄放最上面）：

```markdown
## 2026-10-30 - v1.1.0

### 目標
新增深色模式切換。

### 變更
- `Styles/Themes/`：加入深色主題資源字典
- `Views/SettingsPage.xaml`：新增主題切換項目

### 問題與解法
- 主題切換後部分樣式未更新 → 改用 `ThemeResource` 並重設 `ElementTheme`

### 待辦
- 尚未實測 Windows 10 1809
```

- 只記重點，不貼整段程式碼。
- 不要記錄 API Key、密碼等敏感資訊。

## 8. 文件與禁止事項

- `README.md` 寫功能與建置方式；`CHANGELOG.md` 記錄版本異動；`RELEASE_NOTES.md` 寫給使用者看。
- 不要提交 `bin/`、`obj/`、`.vs/`、`*.user`、`*.suo`、發佈輸出與憑證檔。
- 不要留 `TODO` 當完成功能、不要留測試假資料或除錯輸出（`Console.WriteLine`／`Debug.WriteLine` 需移除）。
- 不要更動無關檔案；未經確認不要改組件名稱、簽章或目標框架版本。
- 不要把 API Key、密碼、金鑰寫進原始碼或版本控制；機密用 User Secrets、環境變數或 Windows 認證管理員。

---

## 專案專屬補充

- 專案名稱：
- 命名空間／組件名稱：
- 特殊需求：
- 已知限制：
