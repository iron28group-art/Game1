using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MagicTree
{
    /// <summary>
    /// 승인된 이미지(Assets/Resources/Art/{id}.png)가 있으면 그것을, 없으면 임시 도형을 돌려준다.
    /// 이미지 id 는 art/manifest.json 의 asset id 와 동일하다.
    ///
    /// Art/art_bounds.txt(kie_art.py 가 승인 때 자동 생성)에 적힌 '보이는 영역'으로 스프라이트를 잘라서
    /// 투명 여백을 없앤다. 그래서 Image 의 아래 가장자리 = 그림의 실제 바닥(풀밭 언덕 밑)이 된다.
    /// </summary>
    public static class ArtLibrary
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Dictionary<string, Rect> _bounds;

        public static bool HasApproved(string id) => Resources.Load<Texture2D>("Art/" + id) != null;

        public static Sprite Get(string id, Color placeholderColor, bool circle = true)
        {
            if (Cache.TryGetValue(id, out var cached)) return cached;

            // 임포트 설정(Sprite/Default)에 상관없이 Texture2D 로 읽어 런타임에 Sprite 생성
            var tex = Resources.Load<Texture2D>("Art/" + id);
            Sprite sprite;
            if (tex != null)
            {
                var b = VisibleBounds(id);
                var rect = new Rect(
                    Mathf.Floor(b.xMin * tex.width), Mathf.Floor(b.yMin * tex.height),
                    Mathf.Ceil(b.width * tex.width), Mathf.Ceil(b.height * tex.height));
                rect.width = Mathf.Min(rect.width, tex.width - rect.x);
                rect.height = Mathf.Min(rect.height, tex.height - rect.y);
                sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            }
            else
            {
                sprite = MakePlaceholder(placeholderColor, circle);
            }
            sprite.name = id;
            Cache[id] = sprite;
            return sprite;
        }

        /// <summary>가로/세로 비율 (preserveAspect 없이 크기를 직접 정할 때 사용).</summary>
        public static float Aspect(Sprite s) => s.rect.width / Mathf.Max(1f, s.rect.height);

        /// <summary>0~1 비율의 보이는 영역 (왼쪽 아래 원점). 기록이 없으면 전체.</summary>
        static Rect VisibleBounds(string id)
        {
            if (_bounds == null)
            {
                _bounds = new Dictionary<string, Rect>();
                var txt = Resources.Load<TextAsset>("Art/art_bounds");
                if (txt != null)
                {
                    foreach (var raw in txt.text.Split('\n'))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line[0] == '#') continue;
                        var p = line.Split(' ');
                        if (p.Length < 5) continue;
                        float F(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
                        _bounds[p[0]] = Rect.MinMaxRect(F(1), F(2), F(3), F(4));
                    }
                }
            }
            return _bounds.TryGetValue(id, out var r) ? r : new Rect(0, 0, 1, 1);
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
