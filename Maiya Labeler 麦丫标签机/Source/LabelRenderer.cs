using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace MaiyaLabeler
{
    [StaticConstructorOnStartup]
    public static class LabelRenderer
    {
        private static Font DefaultFont;

        // 缓存生成的 Mesh
        private static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        private static Dictionary<string, Font> fontCache = new Dictionary<string, Font>();

        // 【核心】缓存我们手动创建的 3D 材质
        // Key: 字体名, Value: 对应的世界空间材质
        private static Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        static LabelRenderer()
        {
            // 尝试加载字体
            DefaultFont = Font.CreateDynamicFontFromOSFont("Arial", 20);
            if (DefaultFont == null) DefaultFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 20);
            // 最后的保底，Unity内置字体
            if (DefaultFont == null)
            {
                try { DefaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
        }

        public static void DrawLabel(Vector3 center, string text, Color color, float altitude, string fontName, float opacity)
        {
            if (string.IsNullOrEmpty(text)) return;

            Font fontToUse = GetFont(fontName);
            if (fontToUse == null) return;

            // 1. 获取/生成 Mesh
            string cacheKey = text + "_" + fontToUse.name;
            Mesh mesh = GetMesh(text, fontToUse, cacheKey);
            if (mesh == null) return;

            // 2. 获取/生成 3D 材质
            // 字体自带的 material 是 UI 用的，放地上会坏掉。我们要手动做一个。
            Material worldMat = GetWorldMaterial(fontToUse);
            if (worldMat == null) return;

            // 3. 绘制参数
            // 高度：确保比地板(Floor)高，比地毯/污渍高。1.0f 比较安全
            Vector3 pos = center;
            pos.y = altitude + 0.5f;

            float size = MaiyaLabelerMod.Settings.defaultFontSize;
            // 保护一下，如果滑条拉没了，就强制为 1
            if (size < 0.1f) size = 1.0f;

            // 旋转 90度 平躺
            Vector3 scale = new Vector3(size, 1f, size);
            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, rotation, scale);

            // 4. 准备材质属性
            // 注意：因为我们是直接画 Material，颜色需要直接改材质属性，或者用 PropertyBlock
            // 为了稳妥，我们使用 PropertyBlock 覆盖材质颜色
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            Color finalColor = color;
            finalColor.a = opacity;
            block.SetColor("_Color", finalColor);
            block.SetTexture("_MainTex", worldMat.mainTexture); // 确保纹理正确

            // 5. 绘制
            Graphics.DrawMesh(mesh, matrix, worldMat, 0, null, 0, block);
        }

        // --- 辅助方法：把 UI 字体材质 转换成 3D 世界材质 ---
        private static Material GetWorldMaterial(Font font)
        {
            if (font == null) return null;
            if (materialCache.TryGetValue(font.name, out Material mat))
            {
                // 检查一下纹理是否丢失（Unity里有时会发生），如果丢了重新获取
                if (mat.mainTexture != null) return mat;
            }

            // 获取字体自带的纹理 (这是一张包含所有文字的大图)
            Texture fontTexture = font.material.mainTexture;
            if (fontTexture == null) return null;

            // 创建一个新材质，使用 RimWorld 的透明 Shader
            // ShaderDatabase.Transparent 是最通用的
            Material newMat = new Material(ShaderDatabase.Transparent);
            newMat.mainTexture = fontTexture;

            // 缓存起来
            materialCache[font.name] = newMat;
            return newMat;
        }

        private static Font GetFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName)) return DefaultFont;
            if (fontCache.TryGetValue(fontName, out Font font)) return font;
            font = Font.CreateDynamicFontFromOSFont(fontName, 20);
            if (font == null) font = DefaultFont; else fontCache[fontName] = font;
            return font;
        }

        private static Mesh GetMesh(string text, Font font, string cacheKey)
        {
            if (meshCache.TryGetValue(cacheKey, out Mesh cached)) return cached;

            // 必须请求纹理，否则材质球是白的
            font.RequestCharactersInTexture(text);

            TextGenerator tg = new TextGenerator();
            TextGenerationSettings settings = new TextGenerationSettings();
            settings.font = font;
            settings.fontSize = 40;
            settings.textAnchor = TextAnchor.MiddleCenter;
            settings.scaleFactor = 1f;
            settings.color = Color.white;
            settings.generationExtents = new Vector2(500f, 200f);
            settings.pivot = new Vector2(0.5f, 0.5f);
            settings.richText = false;

            if (!tg.Populate(text, settings)) return null;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            IList<UIVertex> uiVerts = tg.verts;

            // 缩放比例
            float scale = 0.05f;

            for (int i = 0; i < uiVerts.Count; i++)
            {
                vertices.Add(new Vector3(uiVerts[i].position.x * scale, 0, uiVerts[i].position.y * scale));
                uvs.Add(uiVerts[i].uv0);
            }

            for (int i = 0; i < uiVerts.Count; i += 4)
            {
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            meshCache[cacheKey] = mesh;
            return mesh;
        }

        public static void ClearCache()
        {
            foreach (var m in meshCache.Values) UnityEngine.Object.Destroy(m);
            meshCache.Clear();
            foreach (var mat in materialCache.Values) UnityEngine.Object.Destroy(mat);
            materialCache.Clear();
        }
    }
}