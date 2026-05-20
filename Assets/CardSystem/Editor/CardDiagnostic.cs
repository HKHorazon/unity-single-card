using UnityEngine;
using UnityEditor;

namespace CardSystem.Editor
{
    public static class CardDiagnostic
    {
        [MenuItem("CardSystem/Set Test Card Data")]
        public static void SetTestData()
        {
            var ctrl = Object.FindFirstObjectByType<CardController>();
            if (ctrl == null) { Debug.LogError("[Diag] CardController not found"); return; }
            ctrl.SetCardData(new System.Collections.Generic.Dictionary<string, string>
            {
                { "name",        "Fire Dragon" },
                { "description", "Deals 5 damage to all enemies on entry." },
                { "cost",        "4" },
                { "attack",      "7" },
                { "health",      "3" },
            });
            Debug.Log("[Diag] Test card data applied.");
        }

        [MenuItem("CardSystem/Run Diagnostic")]
        public static void Run()
        {
            var ctrl = Object.FindFirstObjectByType<CardController>();
            if (ctrl == null) { Debug.LogError("[Diag] CardController not found"); return; }

            var col = ctrl.GetComponent<Collider>();
            Debug.Log($"[Diag] Collider={col?.GetType().Name} enabled={col?.enabled}");
            Debug.Log($"[Diag] CardPos={ctrl.transform.position} Scale={ctrl.transform.localScale}");

            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[Diag] No main camera"); return; }
            Debug.Log($"[Diag] CamPos={cam.transform.position} Ortho={cam.orthographic} FOV={cam.fieldOfView}");

            // OverlapBox at card position
            var hits = Physics.OverlapBox(ctrl.transform.position, ctrl.transform.localScale * 0.5f);
            Debug.Log($"[Diag] OverlapBox at card: {hits.Length} hits");

            // Ray from camera to card
            Vector3 cardScreenPos = cam.WorldToScreenPoint(ctrl.transform.position);
            Debug.Log($"[Diag] Card screen pos={cardScreenPos}");
            Ray ray = cam.ScreenPointToRay(cardScreenPos);
            bool rayHit = Physics.Raycast(ray, out RaycastHit hitInfo, 100f);
            Debug.Log($"[Diag] Ray to card: hit={rayHit} obj={hitInfo.collider?.gameObject.name ?? "none"}");

            // Physics layer matrix
            int layer = ctrl.gameObject.layer;
            Debug.Log($"[Diag] Card layer={layer}({LayerMask.LayerToName(layer)}) layerCollidesWith0={!Physics.GetIgnoreLayerCollision(layer, 0)}");
        }
    }
}
