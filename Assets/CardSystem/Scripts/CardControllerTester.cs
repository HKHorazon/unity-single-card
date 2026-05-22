using Sirenix.OdinInspector;
using UnityEngine;

namespace CardSystem
{
    [RequireComponent(typeof(CardController))]
    public class CardControllerTester : MonoBehaviour
    {
        [BoxGroup("Click To Flip")]
        [SerializeField, LabelText("Enabled"), ToggleLeft]
        private bool clickToFlipEnabled = true;

        [BoxGroup("Keys")]
        [SerializeField, LabelText("Flip")]         private KeyCode keyFlip        = KeyCode.F;
        [BoxGroup("Keys")]
        [SerializeField, LabelText("Dissolve On")]  private KeyCode keyDissolveOn  = KeyCode.D;
        [BoxGroup("Keys")]
        [SerializeField, LabelText("Dissolve Off")] private KeyCode keyDissolveOff = KeyCode.S;

        [BoxGroup("Actions")]
        [Button("Flip", ButtonSizes.Medium)]
        private void EditorFlip() => _card.Flip();

        [BoxGroup("Actions")]
        [Button("Dissolve On", ButtonSizes.Medium)]
        private void EditorDissolveOn() => _card.DissolveOn();

        [BoxGroup("Actions")]
        [Button("Dissolve Off", ButtonSizes.Medium)]
        private void EditorDissolveOff() => _card.DissolveOff();

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
