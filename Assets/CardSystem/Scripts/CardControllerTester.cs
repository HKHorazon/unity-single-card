using Sirenix.OdinInspector;
using UnityEngine;

namespace CardSystem
{
    [RequireComponent(typeof(CardController))]
    public class CardControllerTester : MonoBehaviour
    {
        [FoldoutGroup("Click To Flip")]
        [SerializeField, LabelText("Enabled"), ToggleLeft]
        private bool clickToFlipEnabled = true;

        [FoldoutGroup("Keys")]
        [SerializeField, LabelText("Flip")]         private KeyCode keyFlip        = KeyCode.F;
        [FoldoutGroup("Keys")]
        [SerializeField, LabelText("Dissolve On")]  private KeyCode keyDissolveOn  = KeyCode.D;
        [FoldoutGroup("Keys")]
        [SerializeField, LabelText("Dissolve Off")] private KeyCode keyDissolveOff = KeyCode.S;
        [FoldoutGroup("Keys")]
        [SerializeField, LabelText("Halo On")]      private KeyCode keyHaloOn      = KeyCode.G;
        [FoldoutGroup("Keys")]
        [SerializeField, LabelText("Halo Off")]     private KeyCode keyHaloOff     = KeyCode.H;

        [FoldoutGroup("Actions")]
        [Button("Flip", ButtonSizes.Medium)]
        private void EditorFlip() => _card.Flip();

        [FoldoutGroup("Actions")]
        [Button("Dissolve On", ButtonSizes.Medium)]
        private void EditorDissolveOn() => _card.DissolveOn();

        [FoldoutGroup("Actions")]
        [Button("Dissolve Off", ButtonSizes.Medium)]
        private void EditorDissolveOff() => _card.DissolveOff();

        [FoldoutGroup("Actions")]
        [Button("Halo On", ButtonSizes.Medium)]
        private void EditorHaloOn() => _card.HaloOn();

        [FoldoutGroup("Actions")]
        [Button("Halo Off", ButtonSizes.Medium)]
        private void EditorHaloOff() => _card.HaloOff();

        private CardController _card;
        private Camera         _mainCamera;

        private void Awake()
        {
            _card       = GetComponent<CardController>();
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (_card == null) return;

            if (keyFlip        != KeyCode.None && Input.GetKeyDown(keyFlip))        _card.Flip();
            if (keyDissolveOn  != KeyCode.None && Input.GetKeyDown(keyDissolveOn))  _card.DissolveOn();
            if (keyDissolveOff != KeyCode.None && Input.GetKeyDown(keyDissolveOff)) _card.DissolveOff();
            if (keyHaloOn      != KeyCode.None && Input.GetKeyDown(keyHaloOn))      _card.HaloOn();
            if (keyHaloOff     != KeyCode.None && Input.GetKeyDown(keyHaloOff))     _card.HaloOff();

            if (clickToFlipEnabled && _mainCamera != null && Input.GetMouseButtonDown(0))
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                foreach (var h in Physics.RaycastAll(ray, 100f))
                {
                    if (h.transform == transform)
                    {
                        _card.Flip();
                        break;
                    }
                }
            }
        }
    }
}
