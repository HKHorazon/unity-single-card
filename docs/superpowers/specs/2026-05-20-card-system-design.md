# Unity URP 3D 卡牌系統設計文件

**日期**: 2026-05-20  
**版本**: 1.0  
**規格來源**: `Design/Unity_3D_Card_System_Spec.md`

---

## 1. 目標與限制

實作一套高效能、可攜式的 3D 卡牌視覺系統，可整包移轉至任意 Unity URP 專案。

**硬依賴（使用前需手動安裝）**：
- TextMeshPro（Unity 內建套件，需執行 Window > TextMeshPro > Import TMP Essential Resources）
- DOTween（Asset Store 或 Package Manager 安裝）
- URP（Universal Render Pipeline）

---

## 2. 資料夾結構

```
Assets/CardSystem/
├── Scripts/
│   ├── CardTextSnapshotManager.cs
│   └── CardController.cs
├── Shaders/
│   └── CardParallaxDissolve.shader
├── Materials/
│   └── CardParallaxDissolve_Example.mat
├── Prefabs/
│   └── Card_Example.prefab
├── Textures/
│   └── DefaultNoise.png          (預設溶解雜訊貼圖)
└── README.md
```

移轉方式：複製整個 `Assets/CardSystem/` 資料夾至目標專案。  
最終輸出：透過 Assets > Export Package 選取此資料夾，產生 `CardSystem.unitypackage`。

---

## 3. CardTextSnapshotManager

### 職責
管理一個 Off-screen Camera + 共用 RenderTexture，負責將 TMP 文字烘焙為 `Texture2D` 供卡牌材質使用。避免每張卡牌佔用獨立 RT。

### 關鍵設計
- **Singleton**：`CardTextSnapshotManager.Instance`
- **RenderTexture 解析度**：Inspector 可設定，預設 512×512 ARGB32
- **Object Pool**：`GenerateTextSnapshot()` 從池中取得 `Texture2D`，`ReleaseSnapshot()` 歸還並 `Destroy`
- **渲染流程**：填入幕後 Canvas TMP → `snapshotCamera.Render()` → `Graphics.CopyTexture` 至 `Texture2D`
- **防呆**：若烘焙尚未完成，回傳一張純黑 `Texture2D` 避免材質錯亂

### 公開 API
```csharp
Texture2D GenerateTextSnapshot(string cardName, string description, int cost, int attack, int health)
void ReleaseSnapshot(Texture2D texture)
```

---

## 4. CardController

### 職責
單張卡牌的互動、動畫與 Shader 狀態控制器。

### 狀態機
| 狀態 | 說明 | Shader Keyword |
|------|------|----------------|
| Static | 無互動、無動畫 | `PARALLAX_ON` 停用 |
| Dynamic | 懸停 / 翻面 / 溶解進行中 | `PARALLAX_ON` 啟用 |

進入 Dynamic 觸發條件：`OnMouseEnter`、`Flip()` 呼叫、`PlayDissolve()` 呼叫  
離開 Dynamic 條件：`OnMouseExit` 且無進行中 DOTween

### 功能細節
- **初始化**：`Awake` 快取所有 `Shader.PropertyToID`，呼叫 `CardTextSnapshotManager.Instance.GenerateTextSnapshot()` 注入 `_TextTex`
- **懸停傾斜**：`OnMouseOver` 計算滑鼠相對座標，`Quaternion.Lerp` 傾斜 ±15°
- **翻面**：DOTween Y 軸 0→180°，於 90° 時切換 `_IsFaceUp`（0/1 float）
- **溶解**：`PlayDissolve(float duration, Action onComplete)`，DOTween 漸變 `_DissolveAmount` 0→1
- **記憶體**：`OnDestroy` 呼叫 `CardTextSnapshotManager.Instance.ReleaseSnapshot()`

### 公開 API
```csharp
void Flip(float duration = 0.5f)
void PlayDissolve(float duration, System.Action onComplete = null)
void SetCardData(string cardName, string description, int cost, int attack, int health)
```

---

## 5. CardParallaxDissolve.shader

### 基礎
URP Unlit Shader，手寫 HLSL。

### Shader Keywords
```hlsl
#pragma multi_compile _ PARALLAX_ON
```
Static 狀態停用 `PARALLAX_ON`，GPU 編譯出獨立 variant，零運行時 branch 開銷。

### Properties
| 名稱 | 類型 | 說明 |
|------|------|------|
| `_FrameTex` | 2D | 外框貼圖（帶 Alpha） |
| `_MainTex` | 2D | 主圖貼圖（帶 Alpha，原 _CharTex） |
| `_BGTex` | 2D | 背景貼圖（不透明） |
| `_TextTex` | 2D | 動態文字 Snapshot（帶 Alpha） |
| `_NoiseTex` | 2D | 溶解雜訊貼圖 |
| `_GlazeTex` | 2D | 掃光貼圖 |
| `_MainDepth` | Float | 主圖視差深度（正值，原 _CharDepth） |
| `_BGDepth` | Float | 背景視差深度（負值） |
| `_TextDepth` | Float | 文字視差深度 |
| `_DissolveAmount` | Range(0,1) | 溶解進度 |
| `_EdgeWidth` | Range(0,0.2) | 燒焦邊緣寬度 |
| `_EdgeColor` | Color | 燒焦邊緣顏色（可設高 HDR 值發光） |
| `_SweepProgress` | Range(0,1) | 掃光進度 |
| `_IsFaceUp` | Float | 0=背面, 1=正面 |

### 片段著色器流程
1. **視差偏移**（僅 `PARALLAX_ON`）：`parallaxOffset = viewDirTS.xy / viewDirTS.z`
2. **各層 UV**：Frame 不偏移；Main = `uv + offset * _MainDepth`；BG = `uv + offset * _BGDepth`；Text = `uv + offset * _TextDepth`
3. **四層混色**（後→前）：BG → Main → Frame → Text，使用 `lerp(base, layer, layer.a)`
4. **掃光**：`_GlazeTex` UV 由 `viewDirTS.xy * 0.5 + _SweepProgress` 驅動，Screen blend
5. **溶解邊緣**：`edge = _DissolveAmount + _EdgeWidth`；`noiseValue < edge` 時 `color.rgb += _EdgeColor`
6. **溶解剪裁**：`clip(noiseValue - _DissolveAmount)`

### 可擴充設計
`_EdgeColor` 為完整 HDR Color，未來可改為任意邊緣效果（如冰凍藍、電弧白）只需調整此參數或替換邊緣計算邏輯段落。

---

## 6. 效能規範

- 所有 `Shader.PropertyToID` 在 `Awake` 快取，禁止字串傳參
- Static 狀態停用視差 variant，30–50 張卡目標無多餘 Draw Call
- `CardTextSnapshotManager` 全域共用一個 RenderTexture，不隨卡牌數量增加 VRAM

---

## 7. 移轉指南（README 內容摘要）

1. 安裝 TextMeshPro（Import TMP Essential Resources）
2. 安裝 DOTween
3. 複製 `Assets/CardSystem/` 至目標專案
4. 場景中放置一個 `CardTextSnapshotManager` GameObject
5. 使用 `Card_Example.prefab` 作為起點
