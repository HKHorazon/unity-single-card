using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardSystem
{
    [Serializable]
    public class CardTextField
    {
        [HorizontalGroup("Row", Width = 120), LabelText("Key"), LabelWidth(30)]
        public string key = "field";

        [FoldoutGroup("Layout")]
        [LabelText("Anchor Min"), LabelWidth(90)]
        public Vector2 anchorMin = new Vector2(0f, 0f);

        [FoldoutGroup("Layout")]
        [LabelText("Anchor Max"), LabelWidth(90)]
        public Vector2 anchorMax = new Vector2(1f, 0.15f);

        [FoldoutGroup("Layout")]
        [LabelText("Font Size"), LabelWidth(90), Range(0.01f, 0.2f)]
        public float fontSize = 0.06f;

        [FoldoutGroup("Layout")]
        [LabelText("Alignment"), LabelWidth(90)]
        public TextAlignmentOptions alignment = TextAlignmentOptions.Center;

        [HideInInspector] public TextMeshProUGUI tmp;
    }

    public class CardTextSnapshotManager : MonoBehaviour
    {
        public static CardTextSnapshotManager Instance { get; private set; }

        [BoxGroup("Settings")]
        [SerializeField, LabelText("Resolution"), ValueDropdown("ResolutionOptions")]
        private int snapshotResolution = 512;
        private static int[] ResolutionOptions => new[] { 256, 512, 1024 };

        [BoxGroup("Text Fields")]
        [SerializeField, ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "key")]
        private List<CardTextField> textFields = new List<CardTextField>
        {
            new CardTextField { key = "name",        anchorMin = new Vector2(0.1f,  0.85f), anchorMax = new Vector2(0.9f,  0.95f), fontSize = 0.06f },
            new CardTextField { key = "description", anchorMin = new Vector2(0.05f, 0.35f), anchorMax = new Vector2(0.95f, 0.75f), fontSize = 0.04f, alignment = TextAlignmentOptions.TopLeft },
            new CardTextField { key = "cost",        anchorMin = new Vector2(0.0f,  0.0f),  anchorMax = new Vector2(0.2f,  0.15f), fontSize = 0.07f },
            new CardTextField { key = "attack",      anchorMin = new Vector2(0.0f,  0.15f), anchorMax = new Vector2(0.2f,  0.3f),  fontSize = 0.07f },
            new CardTextField { key = "health",      anchorMin = new Vector2(0.8f,  0.15f), anchorMax = new Vector2(1.0f,  0.3f),  fontSize = 0.07f },
        };

        [FoldoutGroup("Internal References"), Sirenix.OdinInspector.ReadOnly]
        [SerializeField] private Camera snapshotCamera;
        [FoldoutGroup("Internal References"), Sirenix.OdinInspector.ReadOnly]
        [SerializeField] private Canvas snapshotCanvas;

        private RenderTexture _renderTexture;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupOffscreenScene();
        }

        private void SetupOffscreenScene()
        {
            var desc = new RenderTextureDescriptor(snapshotResolution, snapshotResolution)
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB,
                depthBufferBits = 16,
                sRGB = true,
                msaaSamples = 1,
            };
            _renderTexture = new RenderTexture(desc);
            _renderTexture.wrapMode   = TextureWrapMode.Clamp;
            _renderTexture.filterMode = FilterMode.Bilinear;
            _renderTexture.Create();

            if (snapshotCamera == null)
            {
                var camGO = new GameObject("SnapshotCamera");
                camGO.transform.SetParent(transform);
                snapshotCamera = camGO.AddComponent<Camera>();
                var urpData = camGO.AddComponent<UniversalAdditionalCameraData>();
                urpData.renderType = CameraRenderType.Base;
                urpData.renderPostProcessing = false;
                urpData.antialiasing = AntialiasingMode.None;
            }
            snapshotCamera.targetTexture    = _renderTexture;
            snapshotCamera.clearFlags       = CameraClearFlags.SolidColor;
            snapshotCamera.backgroundColor  = new Color(0f, 0f, 0f, 0f);
            snapshotCamera.orthographic     = true;
            snapshotCamera.orthographicSize = 0.5f;
            int uiLayer = LayerMask.NameToLayer("UI");
            Debug.Log($"[Snapshot] UI layer index={uiLayer}");
            snapshotCamera.cullingMask = uiLayer >= 0 ? (1 << uiLayer) : ~0;
            snapshotCamera.depth            = -100;
            snapshotCamera.enabled          = false;
            snapshotCamera.transform.position = new Vector3(99999f, 99999f, -1f);

            if (snapshotCanvas == null)
            {
                var canvasGO = new GameObject("SnapshotCanvas");
                canvasGO.transform.SetParent(transform);
                canvasGO.layer = LayerMask.NameToLayer("UI");
                snapshotCanvas = canvasGO.AddComponent<Canvas>();
            }
            snapshotCanvas.renderMode  = RenderMode.WorldSpace;
            snapshotCanvas.worldCamera = snapshotCamera;

            var rt = snapshotCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1f, 1f);
            rt.position  = new Vector3(99999f, 99999f, 0f);

            foreach (var field in textFields)
                field.tmp = CreateTMP(snapshotCanvas.transform, field);
        }

        private TextMeshProUGUI CreateTMP(Transform parent, CardTextField f)
        {
            var go = new GameObject(f.key);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font          = TMP_Settings.defaultFontAsset;
            tmp.fontSize      = f.fontSize;
            tmp.alignment     = f.alignment;
            tmp.color         = Color.white;
            tmp.alpha         = 1f;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;

            if (tmp.font == null)
                Debug.LogError("[Snapshot] TMP_Settings.defaultFontAsset is NULL. Open Window → TextMeshPro → Settings to create it.");

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = f.anchorMin;
            rect.anchorMax = f.anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return tmp;
        }

        [BoxGroup("Settings")]
        [Button("Regenerate All Cards", ButtonSizes.Medium)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void EditorRegenerateAll()
        {
            foreach (var ctrl in FindObjectsByType<CardController>(FindObjectsSortMode.None))
                ctrl.RegenerateSnapshot();
        }

        private readonly Queue<(Dictionary<string,string>, Dictionary<string,Color>, Action<Texture2D>)> _queue = new();
        private bool _rendering = false;

        public void GenerateTextSnapshotAsync(Dictionary<string, string> values, Dictionary<string, Color> colors, Action<Texture2D> onDone)
        {
            _queue.Enqueue((values, colors, onDone));
            if (!_rendering)
                StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _rendering = true;
            while (_queue.Count > 0)
            {
                var (values, colors, onDone) = _queue.Dequeue();
                foreach (var field in textFields)
                {
                    if (field.tmp == null) continue;
                    // 每次都把 textFields 設定同步到 TMP，runtime 改參數立即生效
                    var rect = field.tmp.rectTransform;
                    rect.anchorMin = field.anchorMin;
                    rect.anchorMax = field.anchorMax;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    field.tmp.fontSize  = field.fontSize;
                    field.tmp.alignment = field.alignment;
                    field.tmp.text  = values.TryGetValue(field.key, out var v) ? v : "";
                    var col = colors.TryGetValue(field.key, out var c) ? c : Color.white;
                    col.a = 1f;
                    field.tmp.color = col;
                    field.tmp.ForceMeshUpdate(true, true);
                }
                // 等一個 frame 讓 TMP layout/mesh 完全穩定
                yield return null;
                yield return RenderCoroutine(onDone);
            }
            _rendering = false;
        }

        private IEnumerator RenderCoroutine(Action<Texture2D> onDone)
        {
            Canvas.ForceUpdateCanvases();

            bool rendered = false;
            Action<ScriptableRenderContext, Camera> onEnd = null;
            onEnd = (ctx, cam) =>
            {
                if (cam == snapshotCamera)
                {
                    rendered = true;
                    RenderPipelineManager.endCameraRendering -= onEnd;
                }
            };
            RenderPipelineManager.endCameraRendering += onEnd;

            snapshotCamera.enabled = true;
            yield return new WaitForEndOfFrame();

            int timeout = 3;
            while (!rendered && timeout-- > 0)
                yield return null;

            snapshotCamera.enabled = false;
            if (!rendered)
            {
                Debug.LogError("[Snapshot] Camera never rendered — endCameraRendering not fired");
                RenderPipelineManager.endCameraRendering -= onEnd;
            }
            else
            {
                Debug.Log("[Snapshot] Camera rendered OK");
            }

            // AsyncGPUReadback: bypasses the Render Graph CPU/GPU sync issue in Unity 6 URP
            bool readbackDone = false;
            Texture2D tex = null;
            AsyncGPUReadback.Request(_renderTexture, 0, TextureFormat.RGBA32, req =>
            {
                if (req.hasError)
                {
                    Debug.LogError("[Snapshot] AsyncGPUReadback error");
                    readbackDone = true;
                    return;
                }
                tex = new Texture2D(snapshotResolution, snapshotResolution, TextureFormat.RGBA32, false);
                tex.SetPixelData(req.GetData<byte>(), 0);
                tex.Apply(false, false);
                Debug.Log($"[Snapshot] Readback done, tex={tex.width}x{tex.height}");

#if UNITY_EDITOR
                try
                {
                    // 取四角 pixel 確認背景 alpha
                    var c00 = tex.GetPixel(0, 0);
                    var cMid = tex.GetPixel(tex.width / 2, tex.height / 2);
                    var cTL  = tex.GetPixel(20, tex.height - 20);
                    Debug.Log($"[Snapshot] Pixel(0,0)={c00} Mid={cMid} TopLeft={cTL}");

                    var bytes = tex.EncodeToPNG();
                    var path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "CardSystem/_DebugSnapshot.png");
                    System.IO.File.WriteAllBytes(path, bytes);
                    UnityEditor.AssetDatabase.Refresh();
                    Debug.Log($"[Snapshot] PNG saved to Assets/CardSystem/_DebugSnapshot.png");
                }
                catch (System.Exception e) { Debug.LogError($"[Snapshot] PNG save failed: {e.Message}"); }
#endif

                tex.Apply(false, true);
                readbackDone = true;
            });

            while (!readbackDone)
                yield return null;

            onDone?.Invoke(tex);
        }

        public void ReleaseSnapshot(Texture2D texture)
        {
            if (texture != null) Destroy(texture);
        }

        private void OnDestroy()
        {
            if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); }
        }
    }
}
