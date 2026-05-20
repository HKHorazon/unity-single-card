using System.IO;
using UnityEngine;
using UnityEditor;

namespace CardSystem.Editor
{
    public static class CardShaderTestSetup
    {
        [MenuItem("CardSystem/Setup Test Textures")]
        public static void SetupTestTextures()
        {
            // 產生並存檔測試貼圖
            SaveTexture(MakeSolid(new Color(0.2f, 0.1f, 0.4f, 1f)),       "Assets/CardSystem/Textures/Test_BG.png");
            SaveTexture(MakeGradient(),                                      "Assets/CardSystem/Textures/Test_Main.png");
            SaveTexture(MakeFrame(),                                         "Assets/CardSystem/Textures/Test_Frame.png");
            SaveTexture(MakeSolid(new Color(1f, 1f, 1f, 0.3f)),            "Assets/CardSystem/Textures/Test_Glaze.png");
            AssetDatabase.Refresh();

            // 載入存檔後的貼圖
            var bg    = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CardSystem/Textures/Test_BG.png");
            var main  = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CardSystem/Textures/Test_Main.png");
            var frame = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CardSystem/Textures/Test_Frame.png");
            var glaze = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CardSystem/Textures/Test_Glaze.png");
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CardSystem/Textures/DefaultNoise.png");

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/CardSystem/Materials/CardParallaxDissolve_Example.mat");
            if (mat == null) { Debug.LogError("[CardSystem] Material not found."); return; }

            mat.SetTexture("_BGTex",    bg);
            mat.SetTexture("_MainTex",  main);
            mat.SetTexture("_FrameTex", frame);
            mat.SetTexture("_GlazeTex", glaze);
            mat.SetTexture("_NoiseTex", noise);
            mat.SetFloat("_DissolveAmount", 0f);
            mat.SetFloat("_IsFaceUp", 1f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[CardSystem] Test textures assigned.");
        }

        static void SaveTexture(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Texture2D MakeSolid(Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = c;
            t.SetPixels(pixels);
            t.Apply();
            return t;
        }

        static Texture2D MakeGradient()
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    t.SetPixel(x, y, new Color(x / 64f, y / 64f, 0.5f, 1f));
            t.Apply();
            return t;
        }

        static Texture2D MakeFrame()
        {
            int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool border = x < 4 || x >= s - 4 || y < 4 || y >= s - 4;
                    t.SetPixel(x, y, border ? new Color(1f, 0.9f, 0.5f, 1f) : Color.clear);
                }
            t.Apply();
            return t;
        }
    }
}
