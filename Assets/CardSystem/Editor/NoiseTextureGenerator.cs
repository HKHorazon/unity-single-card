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
