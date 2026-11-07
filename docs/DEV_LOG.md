# 開發紀錄

最新紀錄放最上面。每次開發工作都要更新，不要事後補寫。

---

## 2026-11-07 - v0.14.1 Undo/Redo 修正

### 目標
修正復原／重做的渲染與歷史狀態問題，讓鍵盤快速鍵與選單行為一致。

### 變更
- `LayerRenderCache`：復原最後一筆改以 dirty tile 閉包重建；同圖塊其他筆畫不再被漏畫，重畫筆畫不再與舊像素重複疊加；dirty 範圍過大時退回整層重建。
- `DrawingDocument`：`EndStroke` 走 `PushHistory` 清除 redo；Undo／Redo 會先收尾進行中的筆畫。
- `DrawingDocument`：`IsModified` 改由存檔歷史狀態推導，復原回存檔點會清除未儲存標記。
- `MainWindow`：新增 Ctrl+Z／Ctrl+Y／Ctrl+Shift+Z 快速鍵；Edit 選單改用 `x:Bind`。
- 新增 `LayerRenderCacheTests` 與 undo／redo 狀態測試。
- `MyDraw.csproj`、`MyDraw.sln`：加入 arm64 平台與條件式 `PlatformTarget`／`RuntimeIdentifier`。
- 新增繁體中文專案文件：`README.md`、`docs/專案說明.md`、`docs/使用指南.md`、`docs/系統架構與技術文件.md`、`docs/系統設計文件.md`、`docs/開發指南.md`、`docs/README.md`。
- 準備 0.14.1 GitHub Release：x64 與 arm64 non-self-contained 發行檔。

### 問題與解法
- 原本 `RemoveLastStroke` 只重畫與被移除筆畫範圍相交的筆畫，會漏掉同 tile 但未重疊的筆畫；改為 tile 閉包後修正。
- 復原到存檔點仍顯示 `*`：改以 undo 疊頂端項目作為存檔狀態 token，並處理存檔後分支。
- `dotnet build` 因執行中的 MyDraw 鎖住 exe 失敗；關閉執行中的程式後重試。
- 第一次跑 smoke 時啟動出現前次復原對話框，導致後續 ContentDialog 衝突；暫時移開 recovery 檔後重跑，結束後已還原使用者 recovery 檔。

### 驗證
- `dotnet build MyDraw.sln -c Debug -p:Platform=x64`：0 警告 0 錯誤。
- `dotnet test MyDraw.sln -c Debug -p:Platform=x64`：24/24 通過。
- `dotnet format MyDraw.sln --verify-no-changes --no-restore`：通過。
- `Verify-BrushEffects.ps1`：全部通過（慢／快噴槍亮度 156）。
- `Verify-UiSmoke.ps1`：全部通過（重複筆畫延遲 36 ms）。
- `dotnet build src\MyDraw\MyDraw.csproj -c Debug -p:Platform=arm64`：通過。
- x64／arm64 non-self-contained `dotnet publish`：通過；輸出不含 `coreclr.dll`、`hostfxr.dll`、`Microsoft.WindowsAppRuntime.dll`。
- 以 PE header 確認 `MyDraw.exe`／`MyDraw.dll` 分別為 Amd64 與 Arm64。
- 以 x64 發行版實際啟動，確認視窗可正常顯示。

### 待辦／已知限制
- 圖層屬性（改名、透明度、顯示、鎖定）與畫布尺寸仍不納入 Undo（本次依指示略過）。
- 尚未實機長時間驗證 keyboard accelerator 與高 DPI。

---

## 2026-11-07 - v0.14.0 資料安全、英文介面與品質調整

### 目標
依序完成品質清單 1–4、7–16：資料安全、錯誤訊息、無障礙、UI 測試穩定化、MainWindow 漸進拆分、圖層復原、匯出進度、Undo 圖塊、即時筆畫、螢光筆順序與裝置遺失處理。

### 變更
- 第一層工具列原子筆與鉛筆互換，預設畫筆改為原子筆。
- 新增 `RecoveryService`：30 秒自動復原、復原／捨棄提示、7 天清理；成功儲存時清除，關閉時寫入最後復原快照並保留。
- `ProjectFileService`：儲存前保留最新 `.bak`；可將目前筆畫一併寫入復原快照。
- `JsonSettingsService`：損壞的 `settings.json` 隔離為 `.corrupt`。
- 新增最近開啟清單（最多 10 筆）與檔案選單子選單。
- 刪除圖層／最愛畫筆的確認對話框預設改為取消。
- 新增 `Strings/en-US/Resources.resw`，與 zh-Hant 完整 key 對齊；新增資源 key 對齊測試。
- 新增英文資源；介面語言跟隨 Windows 語言，移除未封裝版本無效的手動語言選單。
- 移除硬編碼 A4 文案與服務層錯誤訊息。
- 關閉程式時不再詢問是否儲存；有未儲存變更時先寫入 recovery 快照，再直接關閉。
- 畫布補 `AutomationProperties.Name`／`HelpText`，工具狀態加入 LiveSetting。
- 最愛畫筆按鈕改為 `AutomationId`，UI smoke 不再用中文 Name 查找；英文資源 key 亦可用 ID 測試。
- `DrawingDocument`：新增圖層可 Undo／Redo；刪除圖層不再清空歷史。
- PNG 匯出新增進度對話框與取消；取消時刪除半成品。
- `LayerRenderCache`：Undo 最後一筆只重建受影響 tile；移除獨立高亮圖塊，螢光筆回到正常 z-order（重疊略加深）。
- `MainWindow` 先抽出 `FilePickerService`；Dialog／ProjectCoordinator 仍待後續漸進拆分。
- Win2D `CanvasDevice.DeviceLost` 時清除快取並重建。
- 版本與文件更新至 0.14.0。

### 問題與解法
- 自動復原第一次寫入因復原目錄不存在而失敗；改為寫入前 `Directory.CreateDirectory`。
- 未封裝 WinUI 的 MRT 不支援執行期手動切換 XAML 語言；本版本改為跟隨 Windows 語言，不再提供手動語言選單。
- 關閉時只寫 recovery、不自動覆寫原 `.mydraw`；未命名檔案下次啟動可由 recovery 復原。
- `ResourceLoader.GetString` 對 `.Text` resource key 需使用 `/Text` 形式；修正最近開啟選單的 key。
- 螢光筆改回正常 z-order 後，重疊會略為加深，已依討論接受。

### 驗證
- `dotnet build`：0 警告、0 錯誤。
- `dotnet format`：通過。
- `dotnet test`：19/19 通過（含資源 key 對齊、最近專案、備份、目前筆畫存檔）。
- `Verify-UiSmoke.ps1`：全部通過，150 筆延遲 41 ms。
- `Verify-BrushEffects.ps1`、`Verify-OpenExport.ps1`：通過。
- 手動建立復原快照後啟動，確認可復原並載入 800×600 專案。

### 待辦／已知限制
- 英文介面由 Windows 語言決定；未封裝版本不支援 App 內手動切換。
- MainWindow 尚未完全拆出 DialogService／ProjectCoordinator。
- 圖層改名、透明度、顯示與鎖定尚未納入 Undo；目前新增、刪除、排序可復原。
- 即時筆畫仍以整筆準備 + 受影響 tile 重畫；穩定前段 sampling/geometry 快取尚未完成。
- 自動復原 30 秒實機長時間測試、高 DPI／High Contrast 尚未完整驗證。

---

## 2026-11-07 - MIT License

### 目標
將專案授權明確化，並提交到 GitHub 公開檔案庫。

### 變更
- 新增根目錄 `LICENSE`（MIT License，Copyright 2026 mark216tw）。
- `README.md` 新增授權章節連結。
- About 對話框的授權說明改為 MIT License。
- 不變更應用程式版本。

### 驗證
- `dotnet build MyDraw.sln -c Debug -p:Platform=x64` 通過。
- 以 `mark216tw <mark216tw@gmail.com>` 提交並推送至 `https://github.com/mark216tw/win-mydraw-01`。

---

## 2026-11-07 - v0.13.0 工具列、最愛排序與檔案關聯

### 目標
調整常用畫筆順序、提供最愛畫筆排序、增加常用顏色、簡化筆刷設定，並加入 `.mydraw` 檔案關聯與命令列開啟。

### 變更
- `Views/MainWindow.xaml`：第一層改為鉛筆、原子筆、螢光筆；更多畫筆改為馬克筆、噴槍、鋼筆、蠟筆、書法筆；最愛畫筆移到橡皮擦下方；最愛列加上／下移按鈕；色票改為 20 色（5×4）；移除細／中／粗／特粗按鈕。
- `Views/MainWindow.xaml.cs`：更新畫筆選取映射與更多畫筆圖示；新增最愛排序 handler；新增「設定 → 檔案關聯設定」對話框；支援 `QueueOpenProject`、命令列開啟與待開啟專案排程。
- `ViewModels/MainViewModel.cs`：預設畫筆改為鉛筆；新增 `MoveFavoriteBrush`。
- 新增 `Services/FileAssociationService.cs`：HKCU `Software\Classes` 的 `.mydraw` 關聯建立／移除、狀態檢查與 `SHChangeNotify`。
- 新增 `Services/ProjectForwardingService.cs`：以具名管線將第二實例的 `.mydraw` 路徑轉送給既有視窗。
- `Helpers/ProjectActivation.cs`：解析啟動參數中的專案路徑。
- `App.xaml.cs`：第一實例啟動管線服務；第二實例轉送路徑後重導向；支援命令列開啟。
- `Styles/AppStyles.xaml`：新增琥珀、萊姆、天藍、玫瑰色票筆刷，移除 `QuickSizeButtonStyle`。
- `Strings/zh-Hant/Resources.resw`：新增排序／檔案關聯／20 色資源；移除 QuickSize 資源；主畫面提示移除「Esc 離開全螢幕」文字。
- `tests/MyDraw.Tests`：新增最愛排序與命令列路徑測試，共 15 個測試。
- 版本與文件更新至 0.13.0。

### 問題與解法
- 最愛畫筆按鈕移到橡皮擦後，`Verify-UiSmoke.ps1` 原本要求「More → Favorite → Eraser」的位置檢查需改為「Eraser → Favorite → Pan」；預設畫筆改鉛筆後，hover 測試改用 `PencilToolButton`。
- 畫圓測試改為在繪製前明確選取鉛筆，避免前段最愛畫筆測試留下的工具狀態造成偶發失敗。
- 第二實例原本只重導向 activation，命令列路徑不會傳到既有視窗；改用具名管線轉送 `.mydraw` 路徑，實測可開啟 800×600 測試專案。
- 移除檔案關聯時只刪預設值會留下空機碼；改為在沒有其他值／子機碼時一併刪除 `.mydraw` 機碼。
- 主畫面只移除 Esc 提示文案，Esc 功能與 README 快捷鍵說明保留。

### 驗證
- `dotnet build`：0 警告、0 錯誤。
- `dotnet format --verify-no-changes`：通過。
- `dotnet test`：15/15 通過。
- `Verify-BrushEffects.ps1`、`Verify-OpenExport.ps1`、完整 `Verify-UiSmoke.ps1` 全部通過，150 筆延遲 38 ms。
- UI Automation 實測檔案關聯建立／移除與登錄檔命令內容。
- 命令列開啟與第二實例轉送實測通過（800×600 專案）。

### 待辦／已知限制
- 檔案關聯採目前使用者 HKCU，不會註冊給其他使用者；未提供安裝程式自動註冊。
- 若 `.mydraw` 已被其他程式設為預設，使用者仍可能需在 Windows「開啟方式」中確認。
- 主畫面 Esc 提示已移除，但 Esc 功能仍保留。

---

## 2026-11-07 - v0.12.0 畫筆手感與即時筆畫最佳化

### 目標
改善長筆畫即時效能、筆尾橡皮擦一致性、取樣與幾何成本，並補上鉛筆材質、螢光筆重疊、流量與筆尖預覽。

### 變更
- `Views/MainWindow.xaml.cs`：筆尾橡皮擦改用 Eraser 的 `BrushToolSettings`（粗細、不透明度、柔和度、流量、壓力曲線），游標大小同步；新增筆尖形狀游標；即時筆畫改用 `LiveStrokeCache`。
- 新增 `Rendering/LiveStrokeCache.cs`：只重畫新筆點影響的圖塊，完成筆畫後釋放。
- `Helpers/StrokeSampler.cs`：取樣步距改依筆刷大小自適應（0.75–3），單段上限由 512 降為 128。
- `Helpers/StrokeOutline.cs`：圓盤段數依半徑分為 8／12／16／20。
- `Helpers/BrushRenderer.cs`：新增鉛筆石墨孔隙、螢光筆獨立高亮處理所用的 `CanvasBlend.Copy`、噴槍／蠟筆流量。
- `Rendering/LayerRenderCache.cs`：螢光筆使用獨立圖塊，避免筆畫重疊累積加深；橡皮擦同時清除一般與高亮圖塊。
- `Models/DrawingStroke.cs`、`BrushPreset.cs`、`BrushToolSettings.cs`、`FavoriteBrush.cs`、`MainViewModel.cs`：新增 `Flow`，最愛畫筆與每支筆設定同步保存；`SettingsVersion` 提升至 2。
- `Services/ProjectFileService.cs`：專案格式提升至 v6，保存流量；舊專案缺少流量時預設 1。
- `Strings/zh-Hant/Resources.resw`、`Views/MainWindow.xaml`：新增流量滑桿與筆尖預覽相關資源。
- `tests/MyDraw.Tests`：新增自適應取樣、流量存讀與螢光筆 Copy 合成測試，共 12 個測試。
- 版本與文件更新至 0.12.0。

### 問題與解法
- 筆尾橡皮擦原本只切換工具 enum，粗細與不透明度仍取自目前鋼筆；改用 Eraser 工具設定後，翻筆擦除與游標大小一致。
- 即時筆畫原本每個 pointer 事件都重取樣整筆並重畫所有圖塊；`LiveStrokeCache` 只在新增點時重畫受影響圖塊，並快取 `PreparedStroke`。
- 螢光筆若直接使用 `CanvasComposite.Multiply` 會受 Win2D composite 語意影響，透明區域可能覆蓋下層；改為獨立高亮圖塊並以 `CanvasBlend.Copy` 避免重疊累積，最後以 SourceOver 合成。
- 鉛筆孔隙以 mask + `CanvasBlend.Copy` 畫透明小點，避免直接擦除影響其他圖層。
- 流量與不透明度分離後，舊專案與舊設定缺少欄位時以預設 1 載入，維持原本筆觸。

### 驗證
- `dotnet build`：0 警告、0 錯誤。
- `dotnet format --verify-no-changes`：通過。
- `dotnet test`：12/12 通過。
- `Verify-BrushEffects.ps1`：全部通過（噴槍密度／模式、橡皮擦、鉛筆材質）。
- `Verify-OpenExport.ps1`：匯出、儲存、開啟全部通過。
- 完整 `Verify-UiSmoke.ps1`：全部通過，150 筆延遲 57 ms。

### 待辦／已知限制
- 螢光筆圖塊在圖層內固定合成於一般筆畫之上；同一圖層若先畫螢光筆再畫鋼筆，鋼筆仍會顯示，但可能帶有高亮色調。後續若要完全依筆畫順序，需改成 per-stroke z-order 合成。
- 即時筆畫增量目前以「重畫受影響圖塊」實作，極長單筆仍會隨筆點增加而增加單次準備成本。
- 流量目前只套用於噴槍與蠟筆，其他筆刷保留原行為。
- 未以實體繪圖板驗證傾角筆尖預覽與筆尾橡皮擦手感。

---

## 2026-11-07 - v0.11.0 底層最佳化與專案開啟

### 目標
在不更換 UI／渲染框架的前提下，改善渲染熱路徑、存檔可靠性、啟動實例管理與可測試性，並補上 `.mydraw` 開啟功能。

### 變更
- `Views/MainWindow.xaml.cs`：
  - PNG 匯出改為背景 `Task.Run` 的獨立渲染路徑；不再呼叫 `EnsureRenderCache`，避免匯出時重建所有圖層圖塊。
  - 存檔時記錄 `StrokeVersion`，只有版本未變才清除 `IsModified`。
  - 格線與筆刷游標拆到透明 overlay `CanvasControl`；滑鼠移動只重畫 overlay。
  - 新增 `Ctrl+O` 與「檔案 → 開啟專案」流程。
  - `LayerRenderCache` 移出成獨立類別；縮放／平移改用 `CanvasViewport`。
- `Rendering/LayerRenderCache.cs`：可見 tile 剔除、單圈 bounds 計算、每筆一次 `PreparedStroke`、依 DPI 建立圖塊。
- `Helpers/BrushRenderer.cs`、`Helpers/PreparedStroke.cs`：把取樣、stamp 重取樣與 outline geometry 快取在單次筆畫渲染中重用到所有相交 tile；筆尖快取改為 per-device。
- `Helpers/BrushSample.cs`、`Helpers/CanvasViewport.cs`：拆檔與抽出視埠轉換。
- `Services/ProjectFileService.cs`、`IProjectFileService.cs`、`ProjectLoadResult.cs`、`DrawingDocument.cs`、`DrawingLayer.cs`：新增 `LoadAsync`、格式驗證、載入圖層／筆畫與 `MarkSaved(clearModified)`。
- `Services/JsonSettingsService.cs`、`ISettingsService.cs`：原子寫入、`SaveDebounced`（300 ms 合併）與 `Flush`；切換工具改延遲寫入。
- `App.xaml.cs`：`AppInstance` 單一實例、全域例外記錄、Release 不輸出主控台 logging。
- `Helpers/CrashLogger.cs`：例外寫入 `%LocalAppData%\MyDraw\logs\crash-yyyyMMdd.log`。
- `tests/MyDraw.Tests`：新增 xUnit 專案，涵蓋 `StrokeSampler`、`DrawingDocument` undo／redo／load 與 `ProjectFileService` round-trip。
- 版本與文件更新至 0.11.0。

### 問題與解法
- 匯出原本使用 `CanvasDevice.GetSharedDevice()`，與 `CanvasControl` 自有 device 不同，`EnsureRenderCache` 會丟棄並重建所有圖層快取；改為匯出完全走文件 snapshot 與 `BrushRenderer`，不碰 UI 快取。
- 匯出若在 UI 執行緒建 8192×8192 RenderTarget 會明顯卡頓；改為背景執行，並讓筆尖快取以 device 區分，避免 UI 與匯出互相清除。
- `CanvasControl` 固定 96 DPI 在高 DPI 會偏軟；一般尺寸改用 `CanvasControl.Dpi`，超過 1200 萬像素的畫布仍維持 96 DPI，避免圖塊記憶體暴增。
- 測試直接引用 WinUI 專案，需以 `net10.0-windows10.0.19041.0` 與 x64 執行；9 個測試皆通過，含背景 Win2D `CanvasRenderTarget` 匯出。
- 單一實例以 `AppInstance.FindOrRegisterForKey` 實作；實測第二次啟動會立即結束並將既有視窗帶回前景。

### 待辦／已知限制
- 本輪已跑建置、`dotnet format`、`Verify-BrushEffects.ps1`、`Verify-OpenExport.ps1`、完整 `Verify-UiSmoke.ps1`（全部通過，150 筆延遲 38 ms）與 `dotnet test`。
- 新增 `tools/Verify-OpenExport.ps1` 以 UI Automation 實測 PNG 匯出、專案儲存、新增畫紙與開啟專案流程。
- 背景匯出使用 Win2D shared device；若未來改成同時多個匯出工作，需再確認 device 並行限制。
- 大畫布（>1200 萬像素）仍使用 96 DPI 圖塊，放大後清晰度維持原狀。
- 圖層刪除仍不可復原。

---

## 2026-11-06 - 測試流程調整（v0.10.0）

### 目標
降低每次功能完成後的固定測試成本，改成依風險分級。

### 變更
- 新增 `tools/Verify-BrushEffects.ps1`：只驗證噴槍密度與模式、橡皮擦、鉛筆壓感遮罩。
- `AGENTS.md` 新增 Tier 1／2／3 測試分級；完整 `Verify-UiSmoke.ps1` 改為版本／發佈前門檻。
- 不變更應用程式版本。

### 問題與解法
- 原本每次功能完成都跑完整 UI smoke，會重複驗證不相關功能；改為只跑目標測試，版本／發佈前再跑完整回歸。
- 快速腳本使用隔離的 `settings.json`，測試後還原，避免污染使用者設定。

### 待辦／已知限制
- 快速腳本仍依賴螢幕座標與 UI Automation；少數 DPI／視窗狀態可能需要重跑。
- 尚無單元測試專案，純邏輯檢查仍以建置與 UI smoke 為主。

---

## 2026-11-06 - v0.10.0

### 目標
改善噴槍過淡、密度隨速度不均與橡皮擦硬邊問題，並優化畫筆壓感、平滑與收尾。

### 變更
- 新增 `Models/AirbrushMode.cs`；`DrawingStroke`、`BrushPreset`、`BrushToolSettings`、`FavoriteBrush` 加入柔和度、噴槍模式與壓力曲線。
- 新增 `Helpers/StrokeSampler.cs`：鄰點平滑、向心 Catmull–Rom、弧長重取樣、起收筆 taper 與書法筆方向寬度。
- 新增 `Helpers/BrushRenderer.cs`：噴槍霧狀／顆粒、柔和筆尖快取、逐點壓感透明度遮罩、柔和橡皮擦與 `CanvasComposite.DestinationOut`。
- `Views/MainWindow.xaml.cs`：改用 `BrushRenderer`、加入筆刷大小游標圈、筆尾橡皮擦沿用橡皮擦柔和度、移除舊硬邊 `Copy` 橡皮擦。
- 筆刷設定新增柔和度滑桿、噴槍模式與壓力曲線 ComboBox；`settings.json` 加入版本化載入與舊版噴槍／橡皮擦預設值遷移。
- `ProjectFileService` 專案格式提升至 v5，保存柔和度與噴槍模式。
- `tools/Verify-UiSmoke.ps1` 新增噴槍慢／快密度、橡皮擦與新設定控制項驗證。
- 版本與文件更新至 0.10.0。

### 問題與解法
- 噴槍原本每點固定 10 顆硬邊小點，透明度再乘 0.2，預設 opacity 0.2 時實際只有 0.04；改為依弧長 stamp 並以重疊次數換算 alpha。
- 柔和筆尖若以白色遮罩直接畫在白紙上會看不見；改為依筆畫顏色快取筆尖，白色只用於橡皮擦遮罩。
- `ResourceLoader.GetString` 對 `.Content` 後綴鍵會拋出 NamedResource 找不到；改為使用無後綴資源鍵並在開啟 Flyout 時填入 ComboBoxItem。
- XAML 編譯器對沒有 `Content` 的 `ComboBoxItem` 搭配 `x:Uid` 會靜默失敗；改為在程式碼設定本地化內容並保留 AutomationId。
- 舊版設定中的噴槍 opacity 0.2／size 36 與橡皮擦 `UseSizePressure` 會保留舊行為；以 `SettingsVersion < 1` 做一次性遷移。

### 待辦
- Debug/x64 建置 0 警告 0 錯誤；`dotnet format --verify-no-changes` 通過。
- UI smoke 全部通過（含 AirbrushAndEraser），150 筆延遲約 50 ms。
- 放大時的圖塊清晰度（固定 96 DPI）仍可再優化；鉛筆材質與噴槍顆粒密度可在實機微調。

---

## 2026-11-06 - v0.9.0

### 目標
改善多筆繪製後筆跡落後的問題、加入可命名與管理的最愛畫筆，並處理檔案總管持續顯示舊 EXE 圖示。

### 變更
- `Views/MainWindow.xaml.cs`：完成筆畫改為 512×512 按需點陣圖塊快取，只更新筆畫相交圖塊；輸入點加入畫面距離與壓力變化篩選。
- `Services/DrawingDocument.cs`：新增輕量 `StrokePreviewChanged`，新增點時不再觸發完整文件與工具列狀態更新。
- 新增 `Models/FavoriteBrush.cs`；`AppSettings` 與 `MainViewModel` 支援最愛畫筆載入、新增、套用、重新命名、刪除及立即保存。
- `Views/MainWindow.xaml`：在「更多畫筆」下方新增「最愛畫筆」按鈕，Flyout 提供儲存、套用、重新命名與刪除操作。
- `tools/Verify-UiSmoke.ps1`：新增最愛畫筆完整流程、設定檔隔離還原及 150 筆連續繪製回應測試。
- 版本與文件更新至 `0.9.0`。

### 問題與解法
- 原快取以每筆一個 `CanvasCommandList` 保存，畫面仍會重播全部筆畫；改為圖層點陣圖塊後，每幀只合成已點陣化的圖塊。
- `AddPoints` 原本每次都觸發圖層掃描、標題與命令狀態更新；拆出筆畫預覽事件，只要求畫布重繪。
- 自訂畫紙最大可達 8192×8192，整層 RenderTarget 會耗用大量記憶體；改用 512×512 相交時才建立的圖塊。
- EXE 已確認內嵌新版多尺寸 ICO，舊圖示來源為 Windows 對固定輸出路徑的 Shell 快取；採乾淨重建、Shell 通知與圖示快取重建。

### 待辦
- `dotnet build`（Debug/x64）通過，0 警告、0 錯誤；`dotnet format MyDraw.sln --verify-no-changes --no-restore` 通過。
- 完整 UI smoke 通過：最愛畫筆新增／套用／重新命名／刪除與設定持久化、既有繪圖及工具功能皆正常。
- 150 筆連續繪製後介面操作回應為 37 ms；測試完成後已還原原本的 `settings.json`。
- `MyDraw.exe` 檔案版本為 0.9.0.0，來源與輸出 ICO SHA-256 一致；已重建 Windows Explorer 圖示與縮圖快取。

## 2026-11-06 - v0.8.0

### 目標
將預設畫紙改為 1080×1080、記住每支筆的筆刷設定、修正色票與快速尺寸按鈕溢出、加入平移游標並移除中鍵平移。

### 變更
- `Models/CanvasDefinition.cs`、`Services/DrawingDocument.cs`：預設畫紙改為 1080×1080。
- 新增 `Models/BrushToolSettings.cs`；`AppSettings` 新增筆刷設定清單。
- `ViewModels/MainViewModel.cs`：依工具記住粗細、不透明度與壓感選項，切換工具與關閉程式時寫入 `settings.json`。
- `Styles/AppStyles.xaml`、`Views/MainWindow.xaml`：色票 hover 縮放降為 1.04 並加上左右留白；快速尺寸按鈕改用 `QuickSizeButtonStyle`。
- 新增 `Views/CursorGrid.cs`，畫布工作區改用可設定 `ProtectedCursor` 的 Grid；平移工具或按住空白鍵顯示手掌游標，拖曳時顯示移動游標。
- 移除中鍵平移判斷，同步更新畫布提示與 README。
- 版本與文件更新至 `0.8.0`。

### 問題與解法
- 檔案總管顯示舊圖示：以 `PrivateExtractIcons` 比對 exe 內嵌圖示與 `MyDraw.ico`，16–128 px 完全一致，確認是 Windows 圖示快取；需手動執行 `ie4uinit.exe -show` 或清除 `iconcache_*.db` 後重啟檔案總管。
- `MainViewModel` 直接點同一支筆時原本會重置為預設值；改為同工具不重置，切換時才保存／套用各自設定。
- 移除中鍵平移後，中鍵按下會誤觸繪圖；新增僅滑鼠左鍵可開始繪製的檢查。
- 1080×1080 預設畫紙搭配自動縮放後，UI 測試原本的固定座標會落在畫布外；測試改以視窗中心計算。

### 待辦
- `dotnet build`（Debug/x64）通過，0 警告、0 錯誤；`dotnet format MyDraw.sln --verify-no-changes --no-restore` 通過。
- `tools/Verify-UiSmoke.ps1` 通過：預設 1080×1080、設定、圖層、畫圓、自交填色、hover、右鍵與中鍵不繪圖、筆刷設定記憶、全螢幕、關於、左右工具列。
- 另以 Win32 `GetCursorInfo` 實測：鋼筆 → 平移工具手掌游標 → 鋼筆還原游標，空白鍵亦切換手掌游標。
- 重啟程式後實測鋼筆粗細由 `settings.json` 還原為前次設定的 10 px。
- 檔案總管圖示若仍顯示舊圖示，需手動執行 `ie4uinit.exe -show` 或清除 `iconcache_*.db` 後重啟檔案總管。

## 2026-11-06 - v0.7.0

### 目標
更新 App 圖示為一枝筆、開啟時畫布置中、色票 hover 回饋、修復連續繪製延遲，並移除貼上圖片功能。

### 變更
- `tools/Generate-AppIcon.ps1`、`Assets/MyDraw.svg`：改為深色圓角底與單一枝斜放鋼筆，重新產生 `MyDraw.ico` 與 `MyDraw-256.png`。
- `Views/MainWindow.xaml.cs`：新增首次載入與新增畫紙時自動縮放並置中的 `FitCanvasToViewport`。
- `Styles/AppStyles.xaml`：色票改用含視覺狀態的專用模板，hover 顯示高亮外框與 1.08 倍縮放，pressed 縮小。
- `Views/MainWindow.xaml.cs`：新增分層渲染快取，已完成筆劃只在變更時重建並可增量附加；橡皮擦與復原／重做會重建該層，即時筆劃維持逐帧繪製。
- `Services/DrawingDocument.cs`：新增 `StrokeVersion` 與 `AddPoints` 批次加入筆點，減少每點事件成本。
- 移除貼上圖片：刪除 `ClipboardImageService`、`LayerImage`、選單項目、Ctrl+V 分支、進度環與圖片渲染；`ProjectFileService` 專案格式提升至 v4 且不再寫入圖片資產。
- 版本與文件更新至 `0.7.0`。

### 問題與解法
- 原本每帧重建所有筆劃的 `CanvasGeometry`，筆劃越多越慢；改為每圖層 `CanvasCommandList` 快取，正常筆劃增量附加，遇橡皮擦或歷史變動才重建整層。
- 即時橡皮擦若直接以 `Copy` 混色會誤擦其他圖層，改在當帧暫存 command list 內先重播該層快取再套用橡皮擦。
- 視窗若以最大化狀態啟動，首次配置尺寸可能早於最大化完成；於 `Loaded` 後以 `DispatcherQueue` 再置中一次。

### 待辦
- `dotnet build`（Debug/x64）通過，0 警告、0 錯誤；`dotnet format MyDraw.sln --verify-no-changes --no-restore` 通過。
- `tools/Verify-UiSmoke.ps1` 設定、圖層、畫圓與復原、自交填色、已選畫筆 hover、右鍵不繪圖、全螢幕 Esc、關於及左右工具列皆通過；測試腳本移除 `-NoNewWindow`，避免主控台搶占前景導致選單點擊失敗。
- 以 UI Automation 與滑鼠移動事件實測：開啟時視窗中心為白色畫紙、貼上圖片選單已不存在、色票 hover 有像素變化。
- 噴槍與蠟筆的即時筆劃仍為逐帧重算，極長單筆的效能可再觀察。

## 2026-11-06 - v0.6.0

### 目標
常用色票在滑鼠移入與按下時保持原色，支援剪貼簿截圖與圖片檔貼上。

### 變更
- 色票採固定底色模板並保留鍵盤焦點提示。
- 新增 ClipboardImageService，非同步讀取 Bitmap 或 StorageItems，背景解碼、EXIF 方向校正及 PNG 正規化。
- 圖片貼上建立獨立圖層，置中並等比例縮小至畫紙內，小圖不放大。
- 文件復原紀錄改支援筆劃與整批圖片貼上；支援復原／重做。
- 圖片依圖層順序合成並整合 PNG 匯出；專案格式 v3 將 PNG 資產與位置一起存入 ZIP。
- 新增編輯選單「貼上圖片」及 Ctrl+V；文字輸入欄位維持原生文字貼上。

### 問題與解法
- 舊復原紀錄只能存筆劃，改為 Undo／Redo 動作紀錄以支援新增圖片圖層。
- GPU 圖片依 CanvasDevice 快取，文件移除圖片或關閉時釋放；裝置切換時由保留像素重建。
- 圖層刪除仍不可復原，刪除後清除歷史以避免復原引用已刪除圖層。
- WinUI 的 Grid 沒有 IsEnabled，載入期間改用 HitTest 與快捷鍵攔截並顯示 ProgressRing，避免輸入修改文件。
- UI Automation 的螢幕座標受 PowerShell DPI context 影響，測試時切至 Per-Monitor V2 並在結束還原。
- UI Automation 全桌面遞迴搜尋可能被其他程式的 provider 阻塞，改為只搜尋測試程序的頂層視窗；檔案選擇器以鍵盤操作驗證輸出。

### 待辦／已知限制
- 圖片暫不提供拖曳變形框；保留原始 PNG 與顯示座標供後續擴充。
- Debug/x64 建置通過，0 警告、0 錯誤；既有互動回歸測試通過。
- `dotnet format MyDraw.sln --verify-no-changes --no-restore` 通過。
- `Verify-ImagePaste.ps1` 實測 16 色 hover、Bitmap 貼上、復原／重做、原生文字貼上、透明 PNG 檔案貼上、專案 ZIP 圖片與位置及 PNG 匯出像素均通過。測試還原原剪貼簿內容。

## 2026-11-06 - v0.5.0

### 目標
修復同一筆交叉處的白色孔洞與選取畫筆 hover 變色，提供工具列左右切換、右鍵平移、Esc 離開全螢幕及關於資訊，名稱更新為 MyDraw繪圖板。

### 變更
- `StrokeOutline` 以同向 winding 的圓盤與連接四邊形組成連續筆跡，單次填色避免自交挖空與半透明重疊加深。
- 已選畫筆套用固定底色模板，切換工具與主題時同步更新。
- 設定加入可持久化的工具列位置，同步調整畫布欄位與 Flyout 方向。
- 右鍵拖曳平移保留左鍵繪圖，Esc 離開全螢幕但讓對話框優先處理。
- 設定選單末尾新增關於，從組件讀取版本，更新名稱與產品資訊。

### 問題與解法
- 舊單一輪廓使用交替填色，自交處被視為孔洞；改成一致方向的多個封閉輪廓並用 winding 合併區域。
- 原生按鈕的 PointerOver 樣式覆蓋選取底色；選取按鈕改用固定底色模板，未選取時還原。
- PowerShell 5.1 以本機編碼讀取無 BOM 的 UTF-8 中文註解時，測試行可能被錯誤解析；測試腳本註解改用 ASCII。

### 待辦／已知限制
- Debug/x64 建置通過，0 警告、0 錯誤。
- `dotnet format MyDraw.sln --verify-no-changes --no-restore` 通過。
- UI Automation 測試通過：設定、圖層、畫圓與復原、自交像素填色、已選畫筆 hover 像素比較、右鍵拖曳不繪圖、全螢幕 Esc、關於、左右工具列切換。
- 壓感設備與多種 DPI 的實機視覺效果仍需硬體驗證。

## 2026-11-06 - v0.4.0

### 目標
重整分散的使用者介面，改為「頂部系統選單 + 左側一站式集中工具列 + 第二層彈出選單」佈局，重繪所有畫筆與平移專屬向量圖示，並補齊常用操作與鍵盤快捷鍵。

### 變更
- `Views/MainWindow.xaml`、`Views/MainWindow.xaml.cs`：
  - 移除右側 208 DIP 常駐面板與重複的外觀下拉選單，完全釋放畫布寬度。
  - 頂部改為原生 `MenuBar`（檔案、編輯、畫布、檢視、設定）搭配右側工具／尺寸／縮放狀態膠囊。
  - 左側整合為單一工具列：最上方集中復原／重做，接著為常用 3 支筆（鋼筆、鉛筆、螢光筆）、「更多畫筆」第二層選單（原子筆、書法筆、馬克筆、噴槍、蠟筆，主按鈕即時同步所選筆尖圖示）、橡皮擦、平移工具，以及「筆刷設定」、「顏色」、「圖層」三個第二層 `Flyout` 彈出面板。
  - 全面改用 24×24 自繪幾何向量圖示，解決舊版字型圖示重複、噴槍誤用調色盤、螢光筆與平移辨識不佳問題；平移改為十字四向箭頭。
  - 新增「新增畫紙 (`Ctrl+N`)」、「另存專案 (`Ctrl+Shift+S`)」、「匯出 PNG (`Ctrl+E`)」、「畫紙大小 (`Ctrl+Alt+C`)」、「顯示格線 (`Ctrl+G`)」、「畫面放大／縮小／100% 重設 (`Ctrl++` / `Ctrl+-` / `Ctrl+0`)」、「外觀設定 (`Ctrl+,`)」，以及「按住空白鍵 (`Space`) 臨時拖曳平移」與 `[` / `]` 快速調粗細、`P` / `M` / `E` / `H` 單鍵切換工具。
- `Services/DrawingDocument.cs`：新增 `Reset` 重設空白畫紙與預設圖層流程。
- `ViewModels/MainViewModel.cs`：新增筆刷尺寸預覽直徑、百分比與圖層數量通知屬性。
- `Styles/AppStyles.xaml`、`Strings/zh-Hant/Resources.resw`：新增第二層選單按鈕樣式與完整繁體中文資源字串。
- `tools/Verify-UiSmoke.ps1`、`Directory.Build.props`：同步升級至 `0.4.0` 並更新 UI Automation 冒煙測試腳本。

### 問題與解法
- `Flyout` 彈出層在手動切換深淺模式時需同步 `ActualTheme`，且首次開啟前需確保 `DataContext` 已綁定 → 於建構子明確指定各 `Flyout` 根容器 `DataContext` 並在 `Opening` 同步 `RequestedTheme`。
- `WindowActivatedEventArgs` 在 `Windows.UI.Core` 與 `Microsoft.UI.Xaml` 間發生命名衝突 → 明確指定 `Microsoft.UI.Xaml.WindowActivatedEventArgs` 解決。

### 待辦／已知限制
- `.mydraw` 專案開啟、圖片貼上與 PDF／JPG 匯出留待後續階段實作。
- 圖層刪除尚不支援復原（維持二次確認對話框）。

## 2026-11-06 - v0.3.0

### 目標
修復圖層無法操作、設定圖示無明顯反應及圓形筆跡分段問題。

### 變更
- 新增實際圖層模型與選取、新增、重新命名、刪除、上下移動、顯示、鎖定及透明度控制。
- 畫布與 PNG 依圖層順序合成，專案格式 v2 保存圖層與筆劃關聯。
- 設定圖示改開啟外觀設定對話框，套用顯示模式與色彩主題。
- 指標採集改讀取事件內的所有中間取樣，依序處理並補上放開時的終點。
- 基本筆刷改成 Catmull–Rom 插值連續輪廓，一次填色，避免短線段交接與半透明重疊接縫。

### 問題與解法
- 舊圖層區只是停用的預覽，改接實際文件圖層。
- 舊設定按鈕只切換焦點，改成明確的 ContentDialog。
- 舊筆跡只讀每個事件一點，以直線和圓點重疊拼接，改用完整取樣與曲線輪廓。
- `dotnet build MyDraw.sln -c Debug -p:Platform=x64`：0 警告、0 錯誤；`dotnet format --verify-no-changes --no-restore` 通過。
- `tools/Verify-UiSmoke.ps1` 使用 UI Automation 實際點擊設定、新增圖層，並模擬畫圓與復原，三項均通過。

### 待辦／已知限制
- 圖層刪除尚不支援復原，刪除前提供確認。
- 實體筆板與快速畫圓的視覺效果仍需使用者實機確認。

## 2026-11-06 - v0.2.0

### 目標
補齊文件儲存、畫紙尺寸、視窗記憶、更多畫筆與完整外觀設定，並改善高 DPI 顯示清晰度及工具列操作。

### 變更
- 新增 Per-Monitor V2 DPI、視窗位置／大小／最大化狀態記憶及多尺寸 App 圖示。
- 新增畫紙範本、自訂尺寸、縮小確認與非破壞性裁切。
- 新增 Brush Catalog、原子筆、書法筆、噴槍及蠟筆差異化渲染。
- 新增 `.mydraw` ZIP 專案儲存、PNG 匯出、`Ctrl+S` 與未儲存關閉提示。
- 左側工具列縮為 48 DIP、加入滾輪捲動及張開手掌平移圖示。
- 新增三種顯示模式及黃色護眼、海洋藍、森林綠、薰衣草紫、中性灰主題。

### 問題與解法
- 整體介面在 Windows 顯示縮放下可能模糊，於 manifest 明確宣告 Per-Monitor V2 DPI awareness。
- 畫紙縮小若直接刪除筆劃會造成資料遺失，改為只裁切顯示與匯出範圍並保留原始座標。
- 新畫筆若使用 enum 數值儲存會妨礙格式演進，專案檔改存穩定 Brush ID 與版本化 manifest。
- EXE 圖示需要多解析度 ICO，加入可重現的 PowerShell 產生工具並嵌入 16 至 256 px 圖像。

### 待辦／已知限制
- `.mydraw` 已可儲存但尚未提供專案開啟功能。
- 橡皮擦仍以白色筆劃呈現，透明畫布與真正像素擦除留待圖層引擎完成。
- 尚未在多螢幕與實體手寫板硬體上完成所有 DPI、壓感及傾角組合測試。

## 2026-11-06 - v0.1.0

### 目標
依 `docs/功能規劃.md` 建立可執行的階段一繪圖 MVP，先驗證 WinUI 3、Win2D 與手寫筆輸入架構。

### 變更
- 建立 .NET 10、WinUI 3、Win2D、MVVM 與 Hosting 專案骨架。
- 實作 1920x1080 畫布、基本筆刷、橡皮擦、筆壓、平滑曲線及筆尾擦除。
- 實作復原／重做、滾輪縮放、中鍵平移、格線與沉浸模式。
- 建立繁中資源、主題樣式、16 色色票及圖層預覽介面。
- 補齊 README、變更紀錄與版本說明。

### 問題與解法
- Windows App SDK 1.8 的指標 API 使用 `Microsoft.UI.Input`，與舊版規劃範例不同，改用新版 `PointerPoint` 與 `PointerDeviceType`。
- `ObservableProperty` 欄位在 WinUI AOT 情境產生 MVVMTK0045，改由 `ObservableObject.SetProperty` 提供相同通知機制。
- 僅提供 `zh-Hant` 資源時產生 PRI263 警告，於專案宣告 `DefaultLanguage` 後消除。

### 待辦／已知限制
- 目前為單圖層記憶體筆劃；尚未支援專案儲存、匯出及圖片匯入。
- 橡皮擦以白色像素筆劃呈現，透明畫布與真正像素擦除留待圖層引擎階段完成。
- 尚未在實體 Wacom、Huion、XP-Pen 與 Surface Pen 裝置上驗證壓感及傾角。






