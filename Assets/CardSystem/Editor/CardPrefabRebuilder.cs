using UnityEngine;
using UnityEditor;

namespace CardSystem.Editor
{
    [InitializeOnLoad]
    public static class CardPrefabRebuilder
    {
        static CardPrefabRebuilder()
        {
            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            EditorApplication.delayCall -= RunOnce;
            const string key = "CardSystem_PrefabRebuilt_v2";
            if (EditorPrefs.GetBool(key, false)) return;
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardSystem/Prefabs/Card_Example.prefab");
            if (prefabRoot == null) return;
            // check if already has FrontFace child
            bool hasChild = false;
            foreach (Transform t in prefabRoot.transform)
                if (t.name == "FrontFace") { hasChild = true; break; }
            if (hasChild) { EditorPrefs.SetBool(key, true); return; }
            Rebuild();
            EditorPrefs.SetBool(key, true);
        }

        [MenuItem("CardSystem/Rebuild Card Prefab")]
        public static void Rebuild()
        {
            var backMatPath = "Assets/CardSystem/Materials/CardBackFace_Example.mat";
            var backShader  = Shader.Find("CardSystem/CardBackFace");
            if (backShader == null) { Debug.LogError("[CardSystem] CardBackFace shader not found"); return; }

            var backMat = AssetDatabase.LoadAssetAtPath<Material>(backMatPath);
            if (backMat == null)
            {
                backMat = new Material(backShader);
                AssetDatabase.CreateAsset(backMat, backMatPath);
                AssetDatabase.SaveAssets();
            }

            var frontMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/CardSystem/Materials/CardParallaxDissolve_Example.mat");
            if (frontMat == null) { Debug.LogError("[CardSystem] Front material not found"); return; }

            var prefabPath = "Assets/CardSystem/Prefabs/Card_Example.prefab";
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            // remove old mesh components from root
            var oldMF = prefabRoot.GetComponent<MeshFilter>();
            var oldMR = prefabRoot.GetComponent<MeshRenderer>();
            if (oldMF != null) Object.DestroyImmediate(oldMF);
            if (oldMR != null) Object.DestroyImmediate(oldMR);

            // remove old children
            for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(prefabRoot.transform.GetChild(i).gameObject);

            // FrontFace
            var frontGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frontGO.name = "FrontFace";
            Object.DestroyImmediate(frontGO.GetComponent<MeshCollider>());
            frontGO.transform.SetParent(prefabRoot.transform, false);
            frontGO.GetComponent<MeshRenderer>().sharedMaterial = frontMat;

            // BackFace — rotated 180° on Y, slightly behind front to avoid z-fighting
            var backGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backGO.name = "BackFace";
            Object.DestroyImmediate(backGO.GetComponent<MeshCollider>());
            backGO.transform.SetParent(prefabRoot.transform, false);
            backGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            backGO.transform.localPosition = new Vector3(0f, 0f, 0.001f);
            backGO.GetComponent<MeshRenderer>().sharedMaterial = backMat;

            // wire CardController
            var ctrl = prefabRoot.GetComponent<CardController>();
            if (ctrl != null)
            {
                var so = new SerializedObject(ctrl);
                so.FindProperty("frontFace").objectReferenceValue = frontGO.GetComponent<MeshRenderer>();
                so.FindProperty("backFace").objectReferenceValue  = backGO.GetComponent<MeshRenderer>();
                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            AssetDatabase.Refresh();

            Debug.Log("[CardSystem] Card_Example prefab rebuilt: FrontFace (Cull Back) + BackFace (Cull Front).");
        }
    }
}
