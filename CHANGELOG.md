# 凱比 SPPB 體能評估 — 技術更新紀錄（CHANGELOG）

給技術人員看。依版本號累積，**新版本加在最上方，舊版本保留不刪**。
版本號以「一輪改動」為單位（從 V2.2 開始記錄）。
給現場人員的白話版另外輸出成桌面的 `V2.x_現場說明.docx`。

---

## V2.5 — 2026-10-22

### 版本資訊

| 項目 | 內容 |
|---|---|
| 對外版本 | V2.5 |
| App 內部版本 | versionName `2.5.0`／versionCode `250`（前一版 `2.2.0`／`220`） |
| 安裝檔 | `Builds/SPPBV2_v2.5.0.apk`（交付名 `SPPBV2.5.apk`），140,336,340 bytes |
| SHA-256（前 16 碼） | `d4af504ec02c0f7c` |
| 打包時間 | 2026-10-22 17:29 |
| 套件名稱／Android／繪圖 API／Unity | 同 V2.2（`com.DefaultCompany.SPPBV2`、minSdk 29／targetSdk 33、GLES3 only、2022.3.62f1） |
| 實機 | 凱比 AIR H300 |

### 1. 中英切換

- **`Core/Lang.cs`（新增）**
  - `Language { Zh, En }`；`Lang.Current` 存在 PlayerPrefs `sppb_language`，下次開啟沿用；`Lang.Changed` 事件。
  - 文字：`Lang.T(zh, en)`，中英寫在同一處。各頁使用者看得到的字串都改用它（`TestPage`、`TeachingPage`、`ScorePage`、`APoseCheck`、`TopBarManager`），原本的 `const string` 改成 property，切換後立即生效。
  - 圖片：`Lang.S(sprite)` 到 `Resources/LangEn/` 找同名英文圖，找不到用原圖；`Lang.Original()` 反查中文原圖。`TopBarManager.SetTitleImage`、`TimerDisplay.SetIcon/SetTitleImage/SetUnitImage` 都套用。
  - `LangSceneSwapper`：`AfterSceneLoad` 自動建立，切換時把場景上所有 Image 換成目前語言的圖，並依 `Lang.SceneTexts` 對照表替換場景/Prefab 裡寫死的 TMP 文字。英文較長時：文字框限制在外框寬 −24 內、開自動縮字。
- **英文圖**：`Resources/LangEn/` 共 42 張（標題、提示大字、計時框標籤、教學頁、分數頁、Exit 等），與中文原圖同名、同尺寸、同風格重繪；`Editor/LangCheck.cs` 檢查每張中文圖是否都有英文對應。
- **英文語音**：`NuwaManager` 英文時呼叫 `SpeakWithLocale(text, "en_US")`（`NuwaPlugin.mRobot.startTTS(text, "en_US")`），中文維持 `Nuwa.startTTS(text)`。
- **語言按鈕**：`TopBarManager.CreateLangButton()`，以離開按鈕為基準位移 (−114.5, −1.2)，130×88，底圖 `Resources/UI/lang_button.png`，加 `ButtonTrigger`（同樣停 2 秒觸發）；標籤顯示「EN／中文」並訂閱 `Lang.Changed`。`ShowLangButton()`：只在首頁顯示（`HomePage` 開、`HideAll` 關）。
- **上方對話框**：`TopBarManager.ConfigureDialogText/ApplyDialogLayout`，英文時 margin 0、寬 560、換行＋自動縮字；中文還原場景原設定（margin 約 −409／−352、寬 200）。原因：場景 TMP 的負 margin 會讓英文長句疊到標題。
- **暫停面板**：說明英文為 "Leaving now won't save this test."；`PausePanel.Initialize()` 避免第一次 `Show()` 被自己的 `Awake` 關掉；「是／否」標籤跟著 `Lang.Changed` 更新。
- **分數頁評語**：`RatingDialog`／`RatingSpeech` 改成 property，四級中英各一組。

### 2. 測驗中提示放大 — `TestPage.ApplyLargeTestLayout()`

在 `Initialize()` 一開始呼叫（必須早於 `CacheHintImageOriginalScale`，否則提示跳出動畫會縮回原尺寸）。倍率皆為 `[SerializeField]`，可在 Inspector 調整：

| 項目 | 參數 | 倍率 | 基準點 |
|---|---|---|---|
| 短提示（倒數 3/2/1、測驗開始!、測驗失敗!、校準成功!） | `_hintScaleShort` | 2.0 | 自身中心 |
| 完成提示（完成第 N 種姿勢…、已完成坐站測試…等） | `_hintScaleLong` | 1.6 | 自身中心；寬度上限 `_hintMaxWidth=940` |
| 右下資訊區（計時框＋計次框＋步行進度條＋「維持動作!」） | `_infoPanelScale` | 1.6 | 計時框與計次框 `frame` 聯集的右下角，整組一起縮放（`ScaleAboutPoint`），避免各自放大後互相重疊 |
| 左上站姿圖示 | `_stanceIconScale` | 1.4 | 圖中玻璃方塊左上角（圖檔右側/下方為陰影留白；步行圖 y 取 10%） |

### 3. 開發工具（不影響凱比版）

- `Editor/SppbBuild.BuildWindows()`：輸出 `Builds/Win/SPPBV2.exe`（介面檢查用）。Batchmode：`Unity.exe -quit -batchmode -buildTarget Win64 -executeMethod SppbBuild.BuildWindows`。之後打 APK 會自動切回 Android；第一次切回時 Gradle daemon 可能被中止導致失敗，重跑一次即可。
- `Utils/UITour.cs`（僅 `UNITY_STANDALONE_WIN || UNITY_EDITOR` 編譯）：`SPPBV2.exe -uitour` 中、英各走一遍 17 個頁面、測驗中提示圖、分數頁四種評級、暫停面板，截圖到 `Builds/Win/tour/`；截提示圖期間停用 `SppbTestRunner`（否則步行 15 秒逾時會跳到分數頁）。
- `VideoPoseTest`：Windows 獨立版一律跳過 VideoPose（原生追蹤器在 Windows 啟動會當掉）；`PushFrame` 前加 null 防護。

### 已知問題 / 待辦

- 分數頁總分 0–3 分時，中文評語在上方對話框會貼到標題（維持現狀）。
- 英文語音需要凱比系統有 en_US TTS 語音，每台現場機器第一次使用請確認英文念得出來。
- V2.2 的已知問題（對焦、`didUpdateThisFrame`、衣服顏色認人、距離估計值）未處理，仍有效。

---

## V2.2 — 2026-09-29 ～ 2026-10-01

### 版本資訊

| 項目 | 內容 |
|---|---|
| 對外版本 | V2.2 |
| App 內部版本 | versionName `2.2.0`／versionCode `220`（前一版 `0.1.1`／`1`；版本碼規則：主.次.修 → 主×100＋次×10＋修） |
| 安裝檔 | `Builds/SPPBV2_v2.2.0.apk`（交付名 `SPPBV2.2.apk`），139,964,082 bytes |
| SHA-256（前 16 碼） | `5d65a4d661b033c7` |
| 打包時間 | 2026-10-01 17:42（程式內容同 09-30 15:17 版，僅改內部版本號） |
| 套件名稱 | `com.DefaultCompany.SPPBV2` |
| Android | minSdk 29／targetSdk 33（Nuwa SDK 的 BroadcastReceiver 在 targetSdk ≥34 會閃退，需維持 ≤33） |
| 繪圖 API | **OpenGL ES 3 only**（關閉 Auto Graphics API，原本 Vulkan 優先） |
| Unity | 2022.3.62f1 |
| 實機 | 凱比 AIR H300（MT8189） |

### 1. 穩定性：原生記憶體洩漏（Vulkan）

- **現象**：Native Heap 每秒漲約 2.2 MB，約 8 分鐘到 1.1 GB 被 lmkd 以 signal 9 砍掉，App 自動重開。
- **定位**：Mono heap（~4 MB）與 `Profiler.GetTotalAllocatedMemoryLong`（~68 MB）都持平，只有 `dumpsys meminfo` 的 Native Heap 漲。用 `memtest.txt` 開關逐一關閉相機、AsyncGPUReadback、同步回讀、CustomRenderTexture、HandDetect、VideoPose 原生追蹤器，漏速皆不變；漏速與 fps 成正比（60fps≈2.2MB/s、30fps≈1.1MB/s、15fps≈0.5MB/s），約 **37 KB/幀**。
- **修正**：`ProjectSettings` → Android Graphics APIs 改為只用 OpenGLES3（`m_APIs: 0b000000`、`m_Automatic: 0`）。GLES3 下漏速 ≈ 10–15 KB/s（量測誤差），追蹤中整輪 RSS 穩定 580–610 MB；fps 由 ~29 回到 ~59。
- **新增** `Assets/Scripts/Utils/MemoryProbe.cs`：`RuntimeInitializeOnLoadMethod` 自動建立，每 30 秒 log `[Mem]`（Mono 使用/堆積、Unity 配置/保留、`/proc/self/status` VmRSS、追蹤狀態、fps）。

### 2. 受測者鎖定（多人場域）— `Core/Detection/SubjectLock.cs`（新增）

- 狀態：`Unlocked → Capturing → Tracking / OtherPerson / Lost`。校準成功時 `BeginLock(refBoxHeight)` 取 8 幀上衣（肩→髖 t=0.5–0.8）與褲子（髖→膝）的色度＋亮度當基準。
- 比對：`FeatureDistance` = max(色度差/0.07, 亮度 log 比/log 1.8)；總分 = 上衣 0.65 + 褲子 0.35（無褲子時只看上衣），>1 判不符；另有中心跳動 >0.3×畫面寬判換人；符合/不符各需持續 0.3 s 確認。
- `IdentityCheckEnabled`：步行、**坐站**關閉（坐站蹲下時上衣遮擋變暗、大腿水平取到椅背景，容易誤判）。
- ROI 遮罩：受測者以外區域 `Array.Clear` 塗黑（只作用在送 VideoPose 的 buffer），頭部追蹤主動時 `maskSuspended` 暫停遮罩。
- 最小身形 = max(畫面高×0.25, 校準身高×0.3)。
- 對焦跟隨：每 1 s 檢查，受測者中心移動 >5% 畫面寬才 `SetFocusPoint`（見「已知問題」）。
- `TestPage`：暫停分「偵測不到您」與「偵測到其他人」兩種提示與語音；`EnterRepositionPause(Presence)`。

### 3. 頭部全身入鏡追蹤 — `Core/HeadTracking/SppbHeadTracker.cs`（新增，移植自 KebbiHeadTracking）

- BODY_25 keyPoints 框位：頭/腳是否入鏡、頸部水平置中；8 Hz 指令；`neck_z`（+右）、`neck_y`（+低頭，`pitchUpSign=-1`）。
- 測驗進行中 `Frozen=true`（轉頭會改變相機座標，影響腳距與步行深度）；剛凍結時 `HoldCurrent()`。
- 目標不超前實機：水平 `maxLeadDeg=8`；垂直 `maxPitchLeadDeg=20`，**只在馬達推不動時加碼**（neck_y 離目標 6–8° 內會停住不動）。
- 垂直指令節流：目標變化 ≥2° 或 0.5 s 且 ≥0.3° 才重送（neck_y 每收到指令就重新起步，同目標重送會卡住）。
- 模式切換（入鏡／找頭↔找腳）時目標拉回實機角度；全身置中移動量限制在另一端邊距一半內（避免來回擺）。
- 凱比 TTS 期間系統會接管頭部馬達（已知、無法避免）。
- `UIManager.GoHome` → `ReturnHome()`。

### 4. A-pose 定位校準

- `APoseCheck.IsAPose` 新增 `out string hint` 口語提示（手臂太低/太高、手肘、膝蓋）。
- `TestPage.UpdateCalibrationGuidance`：依 BODY_25 判斷，**21 個必要關節**（0–14、19–24，不含眼耳）都在畫面內（信心 ≥0.55、離邊 3%）才算全身；加腿長檢查 `(踝−髖)/(髖−頸) ≥ 0.9`，擋掉太近時 VideoPose 在畫面內猜出的腳點。
- 校準成功條件新增 `_guideFullBody`；維持時間 `_aposeHoldTime` 1.0 → **2.0 s**，人形框中央顯示綠色 `DwellRing` 進度。
- 提示只用語音（上方對話框維持原簡短文字，避免爆版）；同句不重複、間隔 ≥7 s；「保持不動」立即播放。
- 教學頁 `APoseCalibration_Teaching` 語音改為完整步驟。

### 5. 互動／UI

- `ButtonTrigger`：觸發改為游標停留 `dwellSeconds=2`（`unscaledDeltaTime`），`DwellRing`（程式產生圓環，`size=60`）顯示在游標上，進度圈改藍 `#2878FF`；游標停在按鈕上時手掌染橘 `(1,0.72,0.2)`，靜態計數處理同時碰兩顆按鈕。
- 游標圖：`pointer.png` 改為 208×208 白色手掌（原 44×44），`Resources/pointer_outline.png` 藍色細外框由 `HandDetect.AddPointerOutline()` 疊在子物件；場景 pointer `sizeDelta` 20→40（scale 2 ⇒ 約 80 px），碰撞框不變。
- `HandDetect`：`videoPoseData` 為空時直接置中返回（原本每幀丟例外）。
- 受測者以 BODY_25 點顯示（無連線，`_skeletonJointSize=8`）：綠 Tracking、紅 OtherPerson、白 校準中。
- `PausePanel`：隱藏原兩顆圖示按鈕，程式產生 120×48 圓角「是／否」按鈕（含 `ButtonTrigger`）；面板開啟時 `Time.timeScale=0`，改 `Physics2D.simulationMode=Script` 並在 `Update` 以 `unscaledDeltaTime` 手動 `Physics2D.Simulate`，關閉時還原（原本暫停時游標觸發不會發生）。
- `ScorePage.ShowRatingComment`：四級評語（`RatingDialog` 文字、`RatingSpeech` 語音，先報總分）。

### 6. 判定調整

| 項目 | 舊值 | 新值 | 依據 |
|---|---|---|---|
| 平衡 1 `sideBySide.toeWidthMax` | 310 | **400** | diag：腳跟併攏外八 fdis≈320，開始 1.8 s 即判失敗；`ankleWidthMax` 260 不變 |
| 坐站初始站直角 `sitStandInitialStandAngle` | 0（從 0 記最大值） | **165°** | 從坐姿開始時第一下不計；實測站直 160–178°、坐 74–110° |
| 步行 `walkDistanceTarget` | 330 | **380** | 現場回饋距離太短 |
| 步行近終點追丟 | — | MaxDiff ≥ 0.9×目標且追丟 ≥0.5 s → 以最遠時刻計時完成 | 太近 VideoPose 追丟（diff 290–350） |
| 步行追丟判定 | — | `timeSinceLastPose>0.3 s` 或 `lastMeanConfidence<0.2` 視為無骨架 | Avatar 會保留最後一幀 |

步行介紹/教學頁語音加入「站到 4.5 公尺外起點」；`Walk_Test` 只改對話框文字（不加語音，避免 TTS 轉頭影響深度）。

### 7. 相機／輸入 — `InputManager.cs`（sample）

- 使用 AsyncGPUReadback 版本；Android 等相機權限（每 5 s 重問）且 `devices` 非空才開相機。
- 新增 `TransformTexture.matrix`／`ToSourceUV`、`IInput.SetFocusPoint`／`IsFocusPointSupported`（`WebCamDevice.isAutoFocusPointSupported`）。

### 資料與隱私

- 程式無任何網路傳輸（無 UnityWebRequest/HttpClient/Socket）；實機 `dumpsys netstats` 中本 App uid 無收發。Manifest 的 INTERNET 權限來自 Nuwa SDK。
- 本機寫入：`SppbTestRunner.writeDiagnostics=true` → `persistentDataPath/diag_*.csv`（判定數值＋27 個 keyPoints，無影像）。正式上線可關閉。

### 已知問題 / 待辦

- **對焦**：建立 `WebCamTexture` 時設 `autoFocusPoint=(0.5,0.5)`，且從未設回 `null` → 一直處於單次對焦模式；另有對焦跟隨與每 300 幀重設，實測約每 2–3 s 觸發一次重新對焦（鏡頭移動期間畫面糊）。依 NB3 相機文件，建議做 A/B/C 對照（現況／純連續對焦／鎖定時對一次再回 `null`＋清晰度觸發），`focusInvertY` 方向未實機驗證。
- `CameraInput.Update` 未檢查 `didUpdateThisFrame`，無新幀時也會回讀並送出同一畫面。
- 身分辨識僅靠衣服顏色：穿著相近者無法區分；脫外衣（黃上衣 vs 偏黃膚色）實測總分 0.97–0.99 未達門檻 1。
- 校準距離「2–3 公尺」與步行起點「4.5 公尺」為估計值，未在場地實測校準（可開 `walkCalibrationMode`）。

---

## V2.2 之前

App 內部版本 `0.1.1`（versionCode 1）。判定核心已由 native `motion_sdk` 改寫為純 C#（`Assets/Scripts/Core/Detection/`）、相機權限等待修正等，未逐項記錄。
