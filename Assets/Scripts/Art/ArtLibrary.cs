using System.Collections.Generic;
using UnityEngine;

namespace MagicTree
{
    /// <summary>
    /// 승인된 이미지(Assets/Resources/Art/{id}.png)가 있으면 그것을, 없으면 임시 도형을 돌려준다.
    /// 이미지 id 는 art/manifest.json 의 asset id 와 동일하다.
    /// </summary>
    public static class ArtLibrary
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static bool HasApproved(string id) => Resources.Load<Texture2D>("Art/" + id) != null;

        public static Sprite Get(string id, Color placeholderColor, bool circle = true)
        {
            if (Cache.TryGetValue(id, out var cached)) return cached;

            // 임포트 설정(Sprite/Default)에 상관없이 Texture2D 로 읽어 런타임에 Sprite 생성
            var tex = Resources.Load<Texture2D>("Art/" + id);
            Sprite sprite = tex != null
                ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f)
                : MakePlaceholder(placeholderColor, circle);
            sprite.name = id;
            Cache[id] = sprite;
            return sprite;
        }

        static Sprite MakePlaceholder(Color color, bool circle)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!circle) { px[y * size + x] = color; continue; }
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d); // 가장자리 안티앨리어싱
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
