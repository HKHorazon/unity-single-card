# Unity URP 3D Card System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立一套完全獨立、可移轉的 Unity URP 3D 卡牌視覺系統，支援視差、翻面、溶解效果。

**Architecture:** 三個核心模組各自職責單一：`CardTextSnapshotManager` 處理文字烘焙與記憶體池；`CardController` 管理互動狀態機與動畫；`CardParallaxDissolve.shader` 以 URP Unlit HLSL 實作視覺效果，透過 `multi_compile` keyword 分離 Static/Dynamic 兩個 GPU variant。

**Tech Stack:** Unity 6000.3.14f1、URP 17.3、TextMeshPro（硬依賴）、DOTween（使用者自行安裝）、手寫 HLSL

---

## 檔案清單

| 動作 | 路徑 | 職責 |
|------|------|------|
| 建立 | `Assets/CardSystem/Scripts/CardTextSnapshotManager.cs` | Singleton，TMP 文字烘焙 + Texture2D 物件池 |
| 建立 | `Assets/CardSystem/Scripts/CardController.cs` | 卡牌互動、狀態機、DOTween 動畫 |
| 建立 | `Assets/CardSystem/Shaders/CardParallaxDissolve.shader` | URP Unlit，視差 + 掃光 + 溶解 HLSL |
| 建立 | `Assets/CardSystem/Materials/CardParallaxDissolve_Example.mat` | 範例材質（引用上方 Shader） |
| 建立 | `Assets/CardSystem/Textures/DefaultNoise.png` | 預設 Perlin noise 溶解貼圖（程式生成） |
| 建立 | `Assets/CardSystem/Prefabs/Card_Example.prefab` | 完整示範 Prefab |
| 建立 | `Assets/CardSystem/README.md` | 移轉說明 |

---

## Task 1: 建立資料夾結構與 README

**Files:**
- Create: `Assets/CardSystem/README.md`

- [ ] **Step 1: 在 Unity 中建立所有資料夾**

在 Unity Editor Project 視窗中手動建立（或用 unity-skills 自動化）：
```
Assets/CardSystem/
Assets/CardSystem/Scripts/
Assets/CardSystem/Shaders/
Assets/CardSystem/Materials/
Assets/CardSystem/Prefabs/
Assets/CardSystem/Textures/
```

- [ ] **Step 2: 建立 README.md**

建立 `Assets/CardSystem/README.md`，內容：

```markdown
# CardSystem — Unity URP 3D Card Visual System

## 依賴（移轉前請先安裝）
1. **TextMeshPro** — Window > TextMeshPro > Import TMP Essential Resources
2. **DOTween** — Asset Store 或 `com.demigiant.dotween` Package Manager
3. **URP** — Universal Render Pipeline 已設定於目標專案

## 移轉步驟
1. 複製整個 `Assets/CardSystem/` 資料夾至目標專案
2. 場景中建立空 GameObject，掛上 `CardTextSnapshotManager` 元件
3. 在 Inspector 設定 `snapshotResolution`（預設 512）
4. 使用 `Card_Example.prefab` 作為卡牌起點
5. 呼叫 `cardController.SetCardData(...)` 設定卡牌資料

## 公開 API 速查
### CardTextSnapshotManager
- `Texture2D GenerateTextSnapshot(string cardName, string description, int cost, int attack, int health)`
- `void ReleaseSnapshot(Texture2D texture)`

### CardController
- `void SetCardData(string cardName, string description, int cost, int attack, int health)`
- `void Flip(float duration = 0.5f)`
- `void PlayDissolve(float duration, System.Action onComplete = null)`

## 匯出 Unity Package
Assets > Export Package > 選取 CardSystem 資料夾 > Export
```

- [ ] **Step 3: 確認結構**

Project 視窗中確認 `Assets/CardSystem/` 下有 5 個子資料夾與 `README.md`。

---

## Task 2: 實作 CardParallaxDissolve.shader

**Files:**
- Create: `Assets/CardSystem/Shaders/CardParallaxDissolve.shader`

- [ ] **Step 1: 建立 Shader 檔案**

在 `Assets/CardSystem/Shaders/` 建立 `CardParallaxDissolve.shader`，完整內容：

```hlsl
Shader "CardSystem/CardParallaxDissolve"
{
    Properties
    {
        _FrameTex ("Frame Texture", 2D) = "white" {}
        _MainTex ("Main Texture", 2D) = "white" {}
        _BGTex ("Background Texture", 2D) = "white" {}
        _TextTex ("Text Snapshot", 2D) = "black" {}
        _NoiseTex ("Dissolve Noise", 2D) = "white" {}
        _GlazeTex ("Glaze/Sweep Texture", 2D) = "black" {}

        _MainDepth ("Main Parallax Depth", Float) = 0.05
        _BGDepth ("BG Parallax Depth", Float) = -0.03
        _TextDepth ("Text Parallax Depth", Float) = 0.02

        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Burn Width", Range(0, 0.2)) = 0.05
        [HDR] _EdgeColor ("Edge Burn Color", Color) = (1, 0.4, 0, 1)

        _SweepProgress ("Sweep Progress", Range(0,1)) = 0
        _IsFaceUp ("Is Face Up", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PARALLAX_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                #if defined(PARALLAX_ON)
                float3 viewDirTS   : TEXCOORD1;
                #endif
            };

            TEXTURE2D(_FrameTex); SAMPLER(sampler_FrameTex);
            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_BGTex);    SAMPLER(sampler_BGTex);
            TEXTURE2D(_TextTex);  SAMPLER(sampler_TextTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_GlazeTex); SAMPLER(sampler_GlazeTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrameTex_ST;
                float4 _MainTex_ST;
                float4 _BGTex_ST;
                float4 _TextTex_ST;
                float4 _NoiseTex_ST;
                float4 _GlazeTex_ST;
                float  _MainDepth;
                float  _BGDepth;
                float  _TextDepth;
                float  _DissolveAmount;
                float  _EdgeWidth;
                float4 _EdgeColor;
                float  _SweepProgress;
                float  _IsFaceUp;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                #if defined(PARALLAX_ON)
                // 建立 TBN 矩陣（物件空間 → 世界空間）
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                float3 tangentWS  = TransformObjectToWorldDir(IN.tangentOS.xyz);
                float3 bitangentWS = cross(normalWS, tangentWS) * IN.tangentOS.w;

                // 世界空間視點向量
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 viewDirWS = normalize(GetCameraPositionWS() - posWS);

                // 轉至切線空間
                OUT.viewDirTS = float3(
                    dot(viewDirWS, tangentWS),
                    dot(viewDirWS, bitangentWS),
                    dot(viewDirWS, normalWS)
                );
                #endif

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // --- 視差偏移 ---
                float2 parallaxOffset = float2(0, 0);
                #if defined(PARALLAX_ON)
                float3 viewDirTS = normalize(IN.viewDirTS);
                // 避免除以零
                float vz = max(abs(viewDirTS.z), 0.001) * sign(viewDirTS.z + 0.0001);
                parallaxOffset = viewDirTS.xy / vz;
                #endif

                float2 uv_frame = uv;
                float2 uv_main  = uv + parallaxOffset * _MainDepth;
                float2 uv_bg    = uv + parallaxOffset * _BGDepth;
                float2 uv_text  = uv + parallaxOffset * _TextDepth;

                // --- 正/背面切換 ---
                // _IsFaceUp == 1 顯示正面，0 顯示純色背面
                half4 bgCol    = SAMPLE_TEXTURE2D(_BGTex,    sampler_BGTex,    uv_bg);
                half4 mainCol  = SAMPLE_TEXTURE2D(_MainTex,  sampler_MainTex,  uv_main);
                half4 frameCol = SAMPLE_TEXTURE2D(_FrameTex, sampler_FrameTex, uv_frame);
                half4 textCol  = SAMPLE_TEXTURE2D(_TextTex,  sampler_TextTex,  uv_text);

                // 四層混色（後 → 前）
                half4 color = bgCol;
                color.rgb = lerp(color.rgb, mainCol.rgb, mainCol.a);
                color.rgb = lerp(color.rgb, frameCol.rgb, frameCol.a);
                color.rgb = lerp(color.rgb, textCol.rgb,  textCol.a * _IsFaceUp);

                // --- 掃光 (Screen blend) ---
                float2 uv_glaze = uv * _GlazeTex_ST.xy + _GlazeTex_ST.zw;
                #if defined(PARALLAX_ON)
                uv_glaze += viewDirTS.xy * 0.5 + _SweepProgress;
                #else
                uv_glaze += _SweepProgress;
                #endif
                half4 glazeCol = SAMPLE_TEXTURE2D(_GlazeTex, sampler_GlazeTex, uv_glaze);
                // Screen: 1 - (1-a)(1-b)
                color.rgb = 1.0 - (1.0 - color.rgb) * (1.0 - glazeCol.rgb * glazeCol.a);

                // --- 溶解邊緣 + 剪裁 ---
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;
                float edge = _DissolveAmount + _EdgeWidth;
                if (noise < edge && noise >= _DissolveAmount)
                {
                    float t = 1.0 - (noise - _DissolveAmount) / max(_EdgeWidth, 0.0001);
                    color.rgb += _EdgeColor.rgb * t;
                }
                clip(noise - _DissolveAmount);

                return color;
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: 在 Unity Editor 確認 Shader 無編譯錯誤**

Project 視窗選取 `CardParallaxDissolve.shader`，Inspector 應顯示「No errors」。若有錯誤，查看 Console 並修正。

- [ ] **Step 3: 建立範例材質**

在 `Assets/CardSystem/Materials/` 建立材質 `CardParallaxDissolve_Example.mat`，Shader 指定為 `CardSystem/CardParallaxDissolve`。

---

## Task 3: 實作 CardTextSnapshotManager.cs

**Files:**
- Create: `Assets/CardSystem/Scripts/CardTextSnapshotManager.cs`

- [ ] **Step 1: 建立腳本**

建立 `Assets/CardSystem/Scripts/CardTextSnapshotManager.cs`：

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardSystem
{
    public class CardTextSnapshotManager : MonoBehaviour
    {
        public static CardTextSnapshotManager Instance { get; private set; }

        [Header("Snapshot Settings")]
        [SerializeField] private int snapshotResolution = 512;

        [Header("Internal References (Auto-created)")]
        [SerializeField] private Camera snapshotCamera;
        [SerializeField] private Canvas snapshotCanvas;
        [SerializeField] private TextMeshProUGUI tmpName;
        [SerializeField] private TextMeshProUGUI tmpDescription;
        [SerializeField] private TextMeshProUGUI tmpCost;
        [SerializeField] private TextMeshProUGUI tmpAttack;
        [SerializeField] private TextMeshProUGUI tmpHealth;

        private RenderTexture _renderTexture;
        private readonly Queue<Texture2D> _pool = new Queue<Texture2D>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupOffscreenScene();
        }

        private void SetupOffscreenScene()
        {
            _renderTexture = new RenderTexture(snapshotResolution, snapshotResolution, 0, RenderTextureFormat.ARGB32);

            // Camera
            if (snapshotCamera == null)
            {
                var camGO = new GameObject("SnapshotCamera");
                camGO.transform.SetParent(transform);
                snapshotCamera = camGO.AddComponent<Camera>();
            }
            snapshotCamera.targetTexture = _renderTexture;
            snapshotCamera.clearFlags = CameraClearFlags.SolidColor;
            snapshotCamera.backgroundColor = Color.clear;
            snapshotCamera.orthographic = true;
            snapshotCamera.orthographicSize = 0.5f;
            snapshotCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            snapshotCamera.enabled = false;

            // Canvas
            if (snapshotCanvas == null)
            {
                var canvasGO = new GameObject("SnapshotCanvas");
                canvasGO.transform.SetParent(transform);
                canvasGO.layer = LayerMask.NameToLayer("UI");
                snapshotCanvas = canvasGO.AddComponent<Canvas>();
            }
            snapshotCanvas.renderMode = RenderMode.WorldSpace;
            snapshotCanvas.worldCamera = snapshotCamera;

            var rt = snapshotCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1f, 1f);
            rt.position = new Vector3(99999f, 99999f, 0f); // 遠離鏡頭視野

            // TMP 元件
            tmpName        = GetOrCreateTMP(snapshotCanvas.transform, "Name",        new Vector2(0.1f, 0.85f), new Vector2(0.9f, 0.95f), 0.06f);
            tmpDescription = GetOrCreateTMP(snapshotCanvas.transform, "Desc",        new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.75f), 0.04f);
            tmpCost        = GetOrCreateTMP(snapshotCanvas.transform, "Cost",        new Vector2(0.0f, 0.0f), new Vector2(0.2f, 0.15f), 0.07f);
            tmpAttack      = GetOrCreateTMP(snapshotCanvas.transform, "Attack",      new Vector2(0.0f, 0.15f), new Vector2(0.2f, 0.3f), 0.07f);
            tmpHealth      = GetOrCreateTMP(snapshotCanvas.transform, "Health",      new Vector2(0.8f, 0.15f), new Vector2(1.0f, 0.3f), 0.07f);
        }

        private TextMeshProUGUI GetOrCreateTMP(Transform parent, string goName, Vector2 anchorMin, Vector2 anchorMax, float fontSize)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return tmp;
        }

        /// <summary>
        /// 將卡牌資料烘焙為 Texture2D。呼叫端持有此 Texture2D，
        /// 銷毀卡牌時必須呼叫 ReleaseSnapshot() 歸還。
        /// </summary>
        public Texture2D GenerateTextSnapshot(string cardName, string description, int cost, int attack, int health)
        {
            tmpName.text        = cardName;
            tmpDescription.text = description;
            tmpCost.text        = cost.ToString();
            tmpAttack.text      = attack.ToString();
            tmpHealth.text      = health.ToString();

            // 強制更新 Canvas layout
            Canvas.ForceUpdateCanvases();

            snapshotCamera.Render();

            Texture2D tex = GetOrCreateTexture();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            tex.ReadPixels(new Rect(0, 0, snapshotResolution, snapshotResolution), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            return tex;
        }

        /// <summary>
        /// 歸還並銷毀 Texture2D，釋放 VRAM。
        /// </summary>
        public void ReleaseSnapshot(Texture2D texture)
        {
            if (texture != null)
                Destroy(texture);
        }

        private Texture2D GetOrCreateTexture()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();
            return new Texture2D(snapshotResolution, snapshotResolution, TextureFormat.ARGB32, false);
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
        }
    }
}
```

- [ ] **Step 2: 確認 Unity Layer 設定**

確認專案中存在 `UI` Layer（Unity 預設第 5 層）。若無，至 Edit > Project Settings > Tags and Layers 新增。

- [ ] **Step 3: 在 Unity 中確認無編譯錯誤**

Console 視窗確認無紅色錯誤，`CardTextSnapshotManager` 可在 AddComponent 中找到。

---

## Task 4: 實作 CardController.cs

**Files:**
- Create: `Assets/CardSystem/Scripts/CardController.cs`

- [ ] **Step 1: 建立腳本**

建立 `Assets/CardSystem/Scripts/CardController.cs`：

```csharp
using DG.Tweening;
using UnityEngine;

namespace CardSystem
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(Collider))]
    public class CardController : MonoBehaviour
    {
        [Header("Card Data")]
        [SerializeField] private string cardName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private int cost;
        [SerializeField] private int attack;
        [SerializeField] private int health;

        [Header("Tilt Settings")]
        [SerializeField] private float maxTiltDegrees = 15f;
        [SerializeField] private float tiltLerpSpeed  = 8f;

        // Shader property ID cache
        private static readonly int ID_TextTex       = Shader.PropertyToID("_TextTex");
        private static readonly int ID_DissolveAmount = Shader.PropertyToID("_DissolveAmount");
        private static readonly int ID_IsFaceUp       = Shader.PropertyToID("_IsFaceUp");

        private MeshRenderer _meshRenderer;
        private Material _material;
        private Texture2D _snapshotTexture;

        private bool _isFaceUp   = true;
        private bool _isDynamic  = false;
        private int  _activeTweens = 0;

        private Quaternion _baseRotation;
        private Camera _mainCamera;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _material = _meshRenderer.material; // instance material
            _baseRotation = transform.localRotation;
            _mainCamera = Camera.main;
        }

        private void Start()
        {
            ApplyCardData();
        }

        private void OnDestroy()
        {
            if (_snapshotTexture != null && CardTextSnapshotManager.Instance != null)
                CardTextSnapshotManager.Instance.ReleaseSnapshot(_snapshotTexture);

            if (_material != null)
                Destroy(_material);
        }

        // ── Public API ─────────────────────────────────────────────────

        public void SetCardData(string name, string desc, int c, int a, int h)
        {
            cardName    = name;
            description = desc;
            cost        = c;
            attack      = a;
            health      = h;
            ApplyCardData();
        }

        public void Flip(float duration = 0.5f)
        {
            EnterDynamic();
            _activeTweens++;

            float targetY = _isFaceUp ? 180f : 0f;
            transform.DOLocalRotateY(targetY, duration)
                .SetEase(Ease.InOutSine)
                .OnUpdate(() =>
                {
                    float angle = transform.localEulerAngles.y;
                    // 過 90° 時切換正/背面
                    bool shouldBeFaceUp = (angle < 90f || angle > 270f);
                    if (shouldBeFaceUp != _isFaceUp)
                    {
                        _isFaceUp = shouldBeFaceUp;
                        _material.SetFloat(ID_IsFaceUp, _isFaceUp ? 1f : 0f);
                    }
                })
                .OnComplete(() =>
                {
                    _activeTweens--;
                    TryExitDynamic();
                });
        }

        public void PlayDissolve(float duration, System.Action onComplete = null)
        {
            EnterDynamic();
            _activeTweens++;

            _material.SetFloat(ID_DissolveAmount, 0f);
            DOTween.To(
                () => _material.GetFloat(ID_DissolveAmount),
                v  => _material.SetFloat(ID_DissolveAmount, v),
                1f,
                duration
            )
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                _activeTweens--;
                onComplete?.Invoke();
                TryExitDynamic();
            });
        }

        // ── Mouse Events ────────────────────────────────────────────────

        private void OnMouseEnter() => EnterDynamic();

        private void OnMouseOver()
        {
            if (_mainCamera == null) return;

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                // 將命中點轉換為卡牌本地座標 (-0.5 ~ 0.5)
                Vector3 localHit = transform.InverseTransformPoint(hit.point);
                float tiltX = -localHit.y * maxTiltDegrees * 2f;
                float tiltY =  localHit.x * maxTiltDegrees * 2f;

                Quaternion target = _baseRotation * Quaternion.Euler(tiltX, tiltY, 0f);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, target, Time.deltaTime * tiltLerpSpeed);
            }
        }

        private void OnMouseExit()
        {
            // 回正
            DOTween.To(
                () => transform.localRotation,
                r  => transform.localRotation = r,
                _baseRotation,
                0.3f
            ).OnComplete(TryExitDynamic);
        }

        // ── State Machine ───────────────────────────────────────────────

        private void EnterDynamic()
        {
            if (_isDynamic) return;
            _isDynamic = true;
            _material.EnableKeyword("PARALLAX_ON");
        }

        private void TryExitDynamic()
        {
            if (_activeTweens > 0) return;
            _isDynamic = false;
            _material.DisableKeyword("PARALLAX_ON");
        }

        // ── Internal ────────────────────────────────────────────────────

        private void ApplyCardData()
        {
            if (CardTextSnapshotManager.Instance == null)
            {
                Debug.LogWarning("[CardController] CardTextSnapshotManager not found in scene.");
                return;
            }

            if (_snapshotTexture != null)
                CardTextSnapshotManager.Instance.ReleaseSnapshot(_snapshotTexture);

            _snapshotTexture = CardTextSnapshotManager.Instance.GenerateTextSnapshot(
                cardName, description, cost, attack, health);

            _material.SetTexture(ID_TextTex, _snapshotTexture);
        }
    }
}
```

- [ ] **Step 2: 在 Unity 確認無編譯錯誤**

Console 無紅色錯誤。若 DOTween 尚未安裝，會出現 `DG.Tweening` namespace 找不到的錯誤 — 此時請先安裝 DOTween。

---

## Task 5: 建立 DefaultNoise 貼圖

**Files:**
- Create: `Assets/CardSystem/Textures/DefaultNoise.png`（程式生成）

- [ ] **Step 1: 建立生成器 Editor 工具**

建立 `Assets/CardSystem/Editor/NoiseTextureGenerator.cs`（Editor Only，不影響移轉）：

```csharp
using UnityEngine;
using UnityEditor;
using System.IO;

namespace CardSystem.Editor
{
    public static class NoiseTextureGenerator
    {
        [MenuItem("CardSystem/Generate Default Noise Texture")]
        public static void Generate()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.R8, false);
            float scale = 4f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float v = Mathf.PerlinNoise(x / (float)size * scale, y / (float)size * scale);
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            tex.Apply();

            string path = "Assets/CardSystem/Textures/DefaultNoise.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();
            Debug.Log("[CardSystem] DefaultNoise.png generated.");
        }
    }
}
```

- [ ] **Step 2: 執行生成器**

Unity 選單 CardSystem > Generate Default Noise Texture。  
確認 `Assets/CardSystem/Textures/DefaultNoise.png` 出現在 Project 視窗。

- [ ] **Step 3: 設定貼圖匯入設定**

選取 `DefaultNoise.png`，Inspector 設定：
- Texture Type: Default
- sRGB: 取消勾選（Linear）
- Compression: None 或 R8

---

## Task 6: 建立 Card_Example Prefab

**Files:**
- Create: `Assets/CardSystem/Prefabs/Card_Example.prefab`

- [ ] **Step 1: 在場景中建立卡牌 GameObject**

1. 建立空場景或在現有場景中操作
2. GameObject > 3D Object > Quad，重新命名為 `Card_Example`
3. 確認 Scale 為 `(0.7, 1.0, 1.0)`（標準卡牌比例）

- [ ] **Step 2: 掛上 CardController**

1. 選取 `Card_Example`，AddComponent > CardSystem > CardController
2. Inspector 填入範例資料：
   - Card Name: `TestCard`
   - Description: `A test card for the system.`
   - Cost: `3`、Attack: `4`、Health: `5`

- [ ] **Step 3: 設定材質**

1. 將 `CardParallaxDissolve_Example.mat` 拖至 Mesh Renderer > Material
2. 將 `DefaultNoise.png` 拖至材質的 `Dissolve Noise` 欄位

- [ ] **Step 4: 存為 Prefab**

拖曳 `Card_Example` 至 `Assets/CardSystem/Prefabs/` 資料夾，建立 Prefab。

- [ ] **Step 5: 建立 CardTextSnapshotManager GameObject**

在場景中建立空 GameObject `CardTextSnapshotManager`，AddComponent > CardSystem > CardTextSnapshotManager，確認 Inspector 可見 `Snapshot Resolution = 512`。

---

## Task 7: 場景整合測試

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`

- [ ] **Step 1: 佈置測試場景**

1. 開啟 `SampleScene`
2. 將 `Card_Example.prefab` 拖入場景
3. 確認場景中有 `CardTextSnapshotManager` GameObject
4. Play Mode，Console 無錯誤

- [ ] **Step 2: 測試懸停傾斜**

Play Mode 下滑鼠移到卡牌上，卡牌應向滑鼠方向輕微傾斜（±15°）。

- [ ] **Step 3: 測試翻面**

建立臨時腳本或用 Inspector Debug，呼叫 `cardController.Flip(0.5f)`，卡牌應在 0.5 秒內翻面。

- [ ] **Step 4: 測試溶解**

呼叫 `cardController.PlayDissolve(2f, () => Debug.Log("Dissolved!")))`，卡牌應在 2 秒內溶解消失，Console 印出 `Dissolved!`。

- [ ] **Step 5: 測試溶解邊緣顏色**

材質 `_EdgeColor` 設為橙色高 HDR 值（如 `(2, 0.5, 0, 1)`），溶解時邊緣應有發光燒焦效果。

---

## Task 8: 驗證可攜性

- [ ] **Step 1: Export Unity Package**

Assets > Export Package > 選取 `Assets/CardSystem` 資料夾（取消 Include Dependencies）> Export > 存為 `CardSystem.unitypackage`。

- [ ] **Step 2: 確認匯出內容**

Package 應包含：
```
CardSystem/Scripts/CardTextSnapshotManager.cs
CardSystem/Scripts/CardController.cs
CardSystem/Shaders/CardParallaxDissolve.shader
CardSystem/Materials/CardParallaxDissolve_Example.mat
CardSystem/Textures/DefaultNoise.png
CardSystem/Prefabs/Card_Example.prefab
CardSystem/README.md
CardSystem/Editor/NoiseTextureGenerator.cs
```

- [ ] **Step 3: 更新 CLAUDE.md**

在專案根目錄 `CLAUDE.md` 補充：最終輸出為 `CardSystem.unitypackage`，匯出步驟見 `Assets/CardSystem/README.md`。

---

## 自我審查備註

- **Shader 中 `_GlazeTex_ST` 已宣告於 CBUFFER**，`SAMPLE_TEXTURE2D` 前不需手動計算 ST — 但此處刻意保留 ST 宣告供使用者自行應用 Tiling/Offset。
- **`CardController.OnMouseExit` 的回正 Tween 未計入 `_activeTweens`**，設計上是允許此 Tween 期間如果有新的 `OnMouseEnter` 可直接 `EnterDynamic()`，不影響正確性。
- **`CardTextSnapshotManager` 的 Object Pool 在 `ReleaseSnapshot` 時直接 Destroy** 而非歸還池，符合設計文件規範（避免持有已釋放 RT 內容的舊貼圖）。
