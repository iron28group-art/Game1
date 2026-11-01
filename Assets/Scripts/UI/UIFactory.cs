using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicTree
{
    /// <summary>씬 파일 없이 코드로 UI 를 만드는 도우미.</summary>
    public static class UIFactory
    {
        static Font _font;
        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                // Unity 2022.2+ 내장 폰트. 한글 글리프는 OS 폰트 폴백으로 표시된다.
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // 세로형 모바일 기준
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color? color = null)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color ?? Color.white;
            img.preserveAspect = sprite != null;
            return img;
        }

        public static Text CreateText(string name, Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        {
            var rt = CreateRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2, -2);
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            return t;
        }

        public static Button CreateButton(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = CreateImage(name, parent, sprite, color);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }
    }
}
