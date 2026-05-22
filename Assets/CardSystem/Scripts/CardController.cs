using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace CardSystem
{
    [Serializable]
    public class CardDataEntry
    {
        [HorizontalGroup("Row", Width = 100), LabelText("Key"), LabelWidth(30)]
        public string key = "name";

        [HorizontalGroup("Row", Width = 160), LabelText("Value"), LabelWidth(40)]
        public string value = "";

        [HorizontalGroup("Row"), LabelText("Color"), LabelWidth(40)]
        public Color color = Color.white;
    }

    [RequireComponent(typeof(Collider))]
    public class CardController : MonoBehaviour
    {
        [BoxGroup("Card Data")]
        [SerializeField, ListDrawerSettings(ShowIndexLabels = false, ListElementLabelName = "key")]
        private List<CardDataEntry> cardData = new List<CardDataEntry>
        {
            new CardDataEntry { key = "name",        value = "Card Name"   },
            new CardDataEntry { key = "description", value = "Description" },
            new CardDataEntry { key = "cost",        value = "0"           },
        };

        [BoxGroup("Card Data")]
        [Button("Apply", ButtonSizes.Medium), PropertyOrder(10)]
        private void EditorApplyCardData() => ApplyCardData();

        [BoxGroup("Card Face")]
        [SerializeField, LabelText("Front Renderer"), Required] private MeshRenderer frontFace;
        [BoxGroup("Card Face")]
        [SerializeField, LabelText("Back Renderer"),  Required] private MeshRenderer backFace;

        [BoxGroup("Textures")]
        [SerializeField, LabelText("Main"),  PreviewField(60)] private Texture mainTexture;
        [BoxGroup("Textures")]
        [SerializeField, LabelText("Frame"), PreviewField(60)] private Texture frameTexture;
        [BoxGroup("Textures")]
        [Button("Apply Textures", ButtonSizes.Medium), PropertyOrder(10)]
        private void EditorApplyTextures() => ApplyTextures();

        [BoxGroup("Tilt")]
        [SerializeField, LabelText("Max Degrees"), Range(1f, 45f)] private float maxTiltDegrees = 15f;
        [BoxGroup("Tilt")]
        [SerializeField, LabelText("Lerp Speed"),  Range(1f, 20f)] private float tiltLerpSpeed  = 8f;
        [BoxGroup("Tilt")]
        [SerializeField, LabelText("Invert Tilt"), ToggleLeft]     private bool  invertTilt      = false;

        [BoxGroup("Flip")]
        [SerializeField, LabelText("Duration"), Range(0.1f, 2f)] private float flipDuration = 0.5f;

        [BoxGroup("Dissolve")]
        [SerializeField, LabelText("Off Amount"), PropertyRange(0f, 1f)]   private float dissolveOffAmount   = 0f;
        [BoxGroup("Dissolve")]
        [SerializeField, LabelText("On Amount"),  PropertyRange(0f, 1f)]   private float dissolveOnAmount    = 1f;
        [BoxGroup("Dissolve")]
        [SerializeField, LabelText("Edge Width"), PropertyRange(0f, 0.2f)] private float dissolveEdgeWidth   = 0.05f;
        [BoxGroup("Dissolve")]
        [SerializeField, LabelText("Duration"),   Range(0.05f, 5f)]        private float dissolveDuration    = 1f;
        [BoxGroup("Dissolve")]
        [SerializeField, LabelText("Start On"),   ToggleLeft]              private bool  dissolveStartOn     = false;

        private static readonly int ID_TextTex        = Shader.PropertyToID("_TextTex");
        private static readonly int ID_DissolveAmount = Shader.PropertyToID("_DissolveAmount");
        private static readonly int ID_EdgeWidth      = Shader.PropertyToID("_EdgeWidth");
        private static readonly int ID_MainTex        = Shader.PropertyToID("_MainTex");
        private static readonly int ID_FrameTex       = Shader.PropertyToID("_FrameTex");

        private Material _frontMat;
        private Material _backMat;
        private Texture2D _snapshotTexture;

        private bool _isFaceUp     = true;
        private int  _flipCount    = 0;
        private bool _isDynamic    = false;
        private int  _activeTweens = 0;
        private bool _dissolveOn   = false;
        private Tweener _dissolveTween;

        private Quaternion _baseRotation;
        private Camera     _mainCamera;

        private void Awake()
        {
            if (frontFace != null) _frontMat = frontFace.material;
            if (backFace  != null) _backMat  = backFace.material;

            _baseRotation = transform.localRotation;
            _mainCamera   = Camera.main;

            var box = GetComponent<BoxCollider>();
            if (box != null) box.size = new Vector3(1f, 1f, 0.1f);
        }

        private void Start()
        {
            ApplyTextures();
            ApplyInitialDissolve();
            ApplyCardData();
        }

        private void OnDestroy()
        {
            if (_snapshotTexture != null && CardTextSnapshotManager.Instance != null)
                CardTextSnapshotManager.Instance.ReleaseSnapshot(_snapshotTexture);

            if (_frontMat != null) Destroy(_frontMat);
            if (_backMat  != null) Destroy(_backMat);
        }

        // ── Public API ──────────────────────────────────────────────────

        public void RegenerateSnapshot() => ApplyCardData();

        public void SetMainTexture (Texture tex) { mainTexture  = tex; ApplyTexture(ID_MainTex,  tex, frontOnly: true); }
        public void SetFrameTexture(Texture tex) { frameTexture = tex; ApplyTexture(ID_FrameTex, tex, frontOnly: false); }

        private void ApplyTextures()
        {
            ApplyTexture(ID_MainTex,  mainTexture,  frontOnly: true);
            ApplyTexture(ID_FrameTex, frameTexture, frontOnly: false);
        }

        private void ApplyTexture(int id, Texture tex, bool frontOnly)
        {
            if (tex == null) return;
            if (_frontMat != null) _frontMat.SetTexture(id, tex);
            if (!frontOnly && _backMat != null) _backMat.SetTexture(id, tex);
        }

        public void SetCardData(Dictionary<string, string> values)
        {
            cardData.Clear();
            foreach (var kv in values)
                cardData.Add(new CardDataEntry { key = kv.Key, value = kv.Value });
            ApplyCardData();
        }

        public void Flip()
        {
            if (_activeTweens > 0) return;
            if (_exitTween != null)
            {
                _exitTween.Kill();
                _exitTween = null;
                _activeTweens--;
            }
            EnterDynamic();
            _activeTweens++;

            _flipCount++;

            const float settleTime = 0.05f;
            float flipTime = Mathf.Max(0.01f, flipDuration - settleTime);

            DOTween.Sequence()
                .Append(transform.DOLocalRotateQuaternion(_baseRotation, settleTime).SetEase(Ease.OutSine))
                .Append(transform.DOLocalRotate(new Vector3(0f, 180f, 0f), flipTime, RotateMode.LocalAxisAdd).SetEase(Ease.InOutSine))
                .OnComplete(() =>
                {
                    _isFaceUp = (_flipCount % 2 == 0);
                    _baseRotation = Quaternion.Euler(0f, _isFaceUp ? 0f : 180f, 0f);
                    transform.localRotation = _baseRotation;
                    _activeTweens--;
                    TryExitDynamic();
                });
        }

        public void DissolveOn(Action onComplete = null)  => SetDissolveState(true,  onComplete);
        public void DissolveOff(Action onComplete = null) => SetDissolveState(false, onComplete);

        public void SetDissolveState(bool on, Action onComplete = null)
        {
            _dissolveOn = on;
            float targetAmount = on ? dissolveOnAmount : dissolveOffAmount;

            EnterDynamic();

            if (_dissolveTween != null)
            {
                _dissolveTween.Kill();
                _dissolveTween = null;
                _activeTweens--;
            }
            _activeTweens++;

            float startAmount = _frontMat != null ? _frontMat.GetFloat(ID_DissolveAmount) : 0f;

            _dissolveTween = DOTween.To(
                () => 0f,
                t  =>
                {
                    float a = Mathf.Lerp(startAmount, targetAmount, t);
                    float e = a * dissolveEdgeWidth;
                    if (_frontMat != null) { _frontMat.SetFloat(ID_DissolveAmount, a); _frontMat.SetFloat(ID_EdgeWidth, e); }
                    if (_backMat  != null) { _backMat.SetFloat(ID_DissolveAmount,  a); _backMat.SetFloat(ID_EdgeWidth,  e); }
                },
                1f, dissolveDuration
            )
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                _dissolveTween = null;
                _activeTweens--;
                onComplete?.Invoke();
                TryExitDynamic();
            });
        }

        private void ApplyInitialDissolve()
        {
            _dissolveOn = dissolveStartOn;
            float amount = _dissolveOn ? dissolveOnAmount : dissolveOffAmount;
            float edge   = amount * dissolveEdgeWidth;
            if (_frontMat != null) { _frontMat.SetFloat(ID_DissolveAmount, amount); _frontMat.SetFloat(ID_EdgeWidth, edge); }
            if (_backMat  != null) { _backMat.SetFloat(ID_DissolveAmount,  amount); _backMat.SetFloat(ID_EdgeWidth,  edge); }
        }

        // ── Mouse Hover ───────────────────────────────────────────────

        private bool    _isHovering = false;
        private Tweener _exitTween;

        private void Update()
        {
            if (_mainCamera == null) return;

            Ray ray  = _mainCamera.ScreenPointToRay(Input.mousePosition);
            bool hit = false;
            RaycastHit hitInfo = default;
            foreach (var h in Physics.RaycastAll(ray, 100f))
                if (h.transform == transform) { hit = true; hitInfo = h; break; }

            if (hit && !_isHovering)
            {
                _isHovering = true;
                if (_exitTween != null)
                {
                    _exitTween.Kill();
                    _exitTween = null;
                    _activeTweens--;
                }
                EnterDynamic();
            }
            else if (!hit && _isHovering)
            {
                _isHovering = false;
                _activeTweens++;
                _exitTween = transform.DOLocalRotate(new Vector3(0f, _flipCount * 180f % 360f, 0f), 0.3f)
                    .SetEase(Ease.OutSine)
                    .OnComplete(() =>
                    {
                        _exitTween = null;
                        _activeTweens--;
                        TryExitDynamic();
                    });
            }

            if (hit && _isHovering && _activeTweens == 0)
            {
                Vector3 localHit = transform.InverseTransformPoint(hitInfo.point);
                float sign  = invertTilt ? -1f : 1f;
                float tiltX = -localHit.y * maxTiltDegrees * 2f * sign;
                float tiltY =  localHit.x * maxTiltDegrees * 2f * sign;

                Quaternion target = _baseRotation * Quaternion.Euler(tiltX, tiltY, 0f);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, target, Time.deltaTime * tiltLerpSpeed);
            }
        }

        // ── State Machine ─────────────────────────────────────────────

        private void EnterDynamic()
        {
            if (_isDynamic) return;
            _isDynamic = true;
            if (_frontMat != null) _frontMat.EnableKeyword("PARALLAX_ON");
        }

        private void TryExitDynamic()
        {
            if (_activeTweens > 0) return;
            _isDynamic = false;
            if (_frontMat != null) _frontMat.DisableKeyword("PARALLAX_ON");
        }

        // ── Internal ──────────────────────────────────────────────────

        private void ApplyCardData()
        {
            if (CardTextSnapshotManager.Instance == null)
            {
                Debug.LogWarning("[CardController] CardTextSnapshotManager not found in scene.");
                return;
            }

            if (_snapshotTexture != null)
            {
                CardTextSnapshotManager.Instance.ReleaseSnapshot(_snapshotTexture);
                _snapshotTexture = null;
            }

            var dict   = new Dictionary<string, string>();
            var colors = new Dictionary<string, Color>();
            foreach (var entry in cardData)
            {
                dict[entry.key]   = entry.value;
                colors[entry.key] = entry.color;
            }

            CardTextSnapshotManager.Instance.GenerateTextSnapshotAsync(dict, colors, tex =>
            {
                _snapshotTexture = tex;
                if (_frontMat != null) _frontMat.SetTexture(ID_TextTex, _snapshotTexture);
                Debug.Log($"[CardController] Snapshot applied: {(tex != null ? $"{tex.width}x{tex.height}" : "NULL")}");
            });
        }
    }
}
