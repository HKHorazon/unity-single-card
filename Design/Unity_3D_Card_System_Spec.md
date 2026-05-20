# Unity URP 高性能 3D 卡牌系統實作開發指南 (AI Prompt Specification)

本文件旨在提供給 Claude (搭配 Unity MCP) 作為精準的技術規格說明書，用以自動化生成、重構或引導開發高品質、可商用且具備低消耗效能的 3D 卡牌系統。

---

## 1. 角色設定與任務目標 (Role & Context)

你是一個精通 Unity URP (Universal Render Pipeline)、高階 HLSL 著色器編寫、以及遊戲架構設計的資深遊戲程式設計師與圖形工程師。
你的任務是根據以下設計規格，實作出一套**高效能、無穿幫（Layer Bleeding）、支援大量卡牌並存**的 3D 空間視差、翻面與整體溶解（Dissolve）系統。

---

## 2. 技術堆疊與環境限制 (Technical Stack)

* **引擎版本**: Unity 2022.3 LTS 或更新版本
* **渲染管線**: URP (Universal Render Pipeline)
* **核心套件**: TextMeshPro, DOTween (或 LeanTween，由程式碼結構動態調配)
* **硬體目標**: 支援大量卡牌（例如場上同時存在 30-50 張），必須嚴格控管 VRAM 與 Draw Calls。

---

## 3. 架構設計藍圖 (Architectural Design)

為解決多圖層 UI 各自執行 Dissolve 時產生的深度穿幫（Z-Fighting / Layer Bleeding），本系統捨棄傳統 UGUI Canvas 多層堆疊，採用 **「單一 Quad 網格 + 多圖層混色著色器 + 文字幕後烘焙」** 架構。

### 核心模組分解：
1.  **`CardTextSnapshotManager.cs` (全域單例管理器)**:
    管理一個幕後隱藏的 Camera 與單一 `RenderTexture`。負責將 TMP 文字、動態數值排版並動態拷貝為輕量化的 `Texture2D`，杜絕每張卡牌佔用獨立 RT 的記憶體怪獸現象。
2.  **`CardController.cs` (單張卡牌控制器)**:
    負責滑鼠懸停傾斜（3D Parallax）、翻面動畫（Rotation）控制、Shader 狀態切換（Static vs Dynamic 材質優化）與 Dissolve 核心數值控制。
3.  **`CardParallaxDissolve.shader` (自訂 HLSL 著色器 / Shader Graph 規格)**:
    利用頂點著色器傳遞的**切線空間視點向量 (Tangent-Space View Direction)**，在片段著色器內即時計算 UV 偏移，實現單一平面下的空間視差（背景、角色、外框、文字），並整合表面 3D 掃光與全域溶解。

---

## 4. 詳細實作任務清單 (Implementation Tasks)

請 Claude 依據以下規格依序生成完整且無語法錯誤的 C# 腳本與 Shader 邏輯：

### 任務一：實作 `CardTextSnapshotManager.cs`

#### 功能需求：
* 採用 Singleton 模式，管理一個 Off-screen 渲染相機與一個共用的 `RenderTexture` (例如 512x512, ARGB32)。
* 提供一個公用方法 `public Texture2D GenerateTextSnapshot(string cardName, string description, int cost, int attack, int health)`。
* **內部優化邏輯**:
    1. 將動態資料填入幕後隱藏的 Canvas/TMP 元件中。
    2. 強制相機渲染該影格 (`snapshotCamera.Render()`)。
    3. 建立或從物件池（Object Pool）取得一張對應解析度的 `Texture2D`。
    4. 使用 `Graphics.CopyTexture` 或 `Texture2D.ReadPixels` 將 RT 內容拷貝至該 `Texture2D`。
    5. 返回該 `Texture2D` 供卡牌材質使用。

---

### 任務二：實作 `CardController.cs`

#### 功能需求：
* **初始化**: 呼叫 `CardTextSnapshotManager` 取得動態文字貼圖，並透過 `_TextTex` 傳入材質。
* **材質狀態管理 (State Optimization)**:
    * *靜態狀態 (Static State)*: 當卡牌無互動、無翻面、無溶解時，切換至簡化版 Shader 或關閉視差計算以省排程。
    * *動態狀態 (Dynamic State)*: 當偵測到滑鼠懸停、開始翻面或執行溶解時，開啟完整功能。
* **滑鼠懸停與視差驅動**:
    * 實作 `OnMouseEnter`, `OnMouseOver`, `OnMouseExit`。
    * 計算滑鼠在卡牌網格上的相對座標或 Delta 位移，利用 `Quaternion.Lerp` 讓卡牌向滑鼠方向產生輕微傾斜（如 $\pm15$ 度）。
* **翻面控制 (Card Flipping)**:
    * 使用 DOTween 驅動 `transform.localRotation` 的 $Y$ 軸旋轉 ($0 \rightarrow 180$度)。
    * 在轉向 90 度（切換面）時，動態更改 Shader 的 `_IsFaceUp` 參數，或啟用背面貼圖顯示。
* **溶解控制 (Dissolve FX)**:
    * 提供 `public void PlayDissolve(float duration, System.Action onComplete)` 方法，利用 DOTween 漸變材質的 `_DissolveAmount` 屬性。

---

### 任務三：實作 `CardParallaxDissolve.shader` (HLSL 規格說明)

若使用程式碼撰寫，請建立一個 URP PBR 或 Unlit 基礎的自訂著色器。若使用 Shader Graph，請輸出其節點邏輯架構。

#### 屬性輸入 (Properties):
* `_FrameTex` (2D): 卡牌外框貼圖 (帶 Alpha)
* `_CharTex` (2D): 角色貼圖 (帶 Alpha)
* `_BGTex` (2D): 背景貼圖 (不透明)
* `_TextTex` (2D): 動態文字 Snapshot 貼圖 (帶 Alpha)
* `_NoiseTex` (2D): 溶解雜訊貼圖
* `_GlazeTex` (2D): 3D 表面掃光/反光貼圖
* `_CharDepth` (Float): 角色視差深度權重 (建議正值)
* `_BGDepth` (Float): 背景視差深度權重 (建議負值)
* `_TextDepth` (Float): 文字視差深度權重
* `_DissolveAmount` (Range(0, 1)): 溶解進度 (0=完整, 1=完全溶解)
* `_SweepProgress` (Range(0, 1)): 3D 掃光進度

#### 頂點著色器邏輯 (Vertex Shader):
* 計算世界空間下的切線（Tangent）、副切線（Bitangent）與法線（Normal）向量。
* 將**世界空間視點向量 (World Space View Direction)** 轉換至 **切線空間 (Tangent Space View Dir)**：
    $$\mathbf{V}_{ts} = \mathbf{TBN} \times \mathbf{V}_{world}$$
* 將 `V_ts` 傳遞給片段著色器。

#### 片段著色器邏輯 (Fragment Shader):
1.  **視差偏移量計算**:
    取得歸一化的切線空間視點向量 `float3 viewDirTS`。
    計算基本 UV 偏移：`float2 parallaxOffset = viewDirTS.xy / viewDirTS.z;`
2.  **個別圖層 UV 偏移應用**:
    * 外框 UV: `uv_frame = originalUV;`
    * 角色 UV: `uv_char = originalUV + parallaxOffset * _CharDepth;`
    * 背景 UV: `uv_bg = originalUV + parallaxOffset * _BGDepth;`
    * 文字 UV: `uv_text = originalUV + parallaxOffset * _TextDepth;`
3.  **圖層混合 (Alpha Blending)**:
    由後往前採樣並以 `lerp` 混色：
    * `color = bgCol;`
    * `color = lerp(color, charCol, charCol.a);`
    * `color = lerp(color, frameCol, frameCol.a);`
    * `color = lerp(color, textCol, textCol.a);`
4.  **3D 反光/掃光疊加 (3D Sweep)**:
    利用 `viewDirTS.xy` 或 `_Time.y * speed + _SweepProgress` 偏移 `_GlazeTex` 的 UV，並以 `Additive` 或 `Screen` 混合至 `color.rgb`。
5.  **整體溶解剪裁 (Dissolve Clipping)**:
    採樣 `_NoiseTex`。
    執行：`clip(noiseValue - _DissolveAmount);`
    （可選：計算 `noiseValue - _DissolveAmount` 的邊緣差值，若小於臨界值，則疊加 `EdgeBurnColor` 實現發光燒焦邊緣）。

---

## 5. 程式碼生成規範與驗證指標

當你在實作上述程式碼時，必須確保：
1.  **無記憶體洩漏**: 在 `CardTextSnapshotManager` 中動態生成的 `Texture2D`，在卡牌被 `Destroy` 時必須顯式呼叫 `Destroy(texture2D)` 釋放 VRAM。
2.  **防呆處理**: 如果文字還沒烘焙完成，Shader 應有預備機制，避免顯示錯亂。
3.  **效能至上**: 所有 `Shader.PropertyToID` 必須在 `Awake` 中預先快取，禁止在 `Update` 或 `OnMouseOver` 中使用字串傳參（例如 `material.SetFloat("_DissolveAmount", ...)`）。

請依據此規格，開始撰寫/重構此系統。
