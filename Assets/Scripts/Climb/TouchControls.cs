using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicTree.Climb
{
    /// <summary>
    /// 화면 어디서나 손가락 하나로 조작한다.
    /// - 좌우로 끌기: 그 방향으로 이동 (처음 닿은 곳 기준, 멀리 끌수록 빠르게)
    /// - 위로 휙 올리기(빠르게 튕기기): 점프. 끄는 중에도 위로 튕기면 점프
    /// - 위아래로 천천히 끌기: 덩굴 오르내리기
    /// 화면 크기에 상관없이 같은 느낌이 나도록 거리·속도는 화면 높이 비율로 잰다.
    /// 마우스도 같은 방식으로 동작한다 (에디터 테스트용).
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        // ── 조작감 수치 (화면 높이 비율) ──
        const float DeadZone = 0.012f;        // 이만큼은 움직여야 이동 시작
        const float FullTilt = 0.055f;        // 이만큼 끌면 최고 속도
        const float MaxLeash = 0.10f;         // 기준점이 손가락을 따라오는 거리 (방향 전환을 빠르게)
        const float FlickSpeed = 1.1f;        // 위로 이 속도(화면높이/초) 넘게 튕기면 점프
        const float FlickMinRise = 0.035f;    // 그리고 최근에 이만큼은 올라가야 점프
        const float FlickWindow = 0.12f;      // 최근 몇 초 동안의 움직임으로 판단할지
        const float JumpCooldown = 0.22f;

        public float Horizontal { get; private set; }
        public float Vertical { get; private set; }

        bool _jumpQueued;
        int _finger = -1;                     // 추적 중인 손가락 (-2 = 마우스)
        Vector2 _anchor;                      // 화면 높이 비율 좌표
        Vector2 _pos;
        float _cooldown;

        // 최근 위치 기록 (튕기기 판정)
        const int HistoryLen = 16;
        readonly Vector2[] _histPos = new Vector2[HistoryLen];
        readonly float[] _histTime = new float[HistoryLen];
        int _histCount, _histHead;

        // 시각 표시: 기준점과 손가락 위치에 옅은 동그라미
        RectTransform _ring, _dot;

        public bool ConsumeJump()
        {
            bool j = _jumpQueued;
            _jumpQueued = false;
            return j;
        }

        public void Init(RectTransform uiRoot, Sprite circle)
        {
            _ring = MakeMarker(uiRoot, "TouchRing", circle, 150f, new Color(1f, 1f, 1f, 0.22f));
            _dot = MakeMarker(uiRoot, "TouchDot", circle, 70f, new Color(1f, 1f, 1f, 0.4f));
        }

        static RectTransform MakeMarker(RectTransform parent, string name, Sprite sprite, float size, Color color)
        {
            var img = UIFactory.CreateImage(name, parent, sprite, color);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            img.gameObject.SetActive(false);
            return rt;
        }

        void OnDisable()
        {
            Release();
            _jumpQueued = false;
        }

        void Update()
        {
            _cooldown = Mathf.Max(0f, _cooldown - Time.unscaledDeltaTime);
            ReadPointer(out bool began, out bool held, out Vector2 screen, out int id);

            if (began && !OverUI(id))
            {
                _finger = id;
                _pos = _anchor = Norm(screen);
                _histCount = 0;
                Record(_pos);
            }

            if (_finger == -1) { Horizontal = Vertical = 0f; return; }
            if (!held || id != _finger) { Release(); return; }

            _pos = Norm(screen);
            Record(_pos);

            // 기준점이 너무 멀어지면 손가락 쪽으로 끌려온다 → 반대로 끌면 바로 반대 방향
            Vector2 d = _pos - _anchor;
            if (Mathf.Abs(d.x) > MaxLeash) _anchor.x = _pos.x - Mathf.Sign(d.x) * MaxLeash;
            if (Mathf.Abs(d.y) > MaxLeash) _anchor.y = _pos.y - Mathf.Sign(d.y) * MaxLeash;
            d = _pos - _anchor;

            Horizontal = Axis(d.x);
            Vertical = Axis(d.y);

            if (_cooldown <= 0f && IsUpFlick())
            {
                _jumpQueued = true;
                _cooldown = JumpCooldown;
                _anchor.y = _pos.y;          // 점프 후 다시 위로 끌어도 계속 '오르기'로 읽히지 않게
                _histCount = 0;
                Record(_pos);
            }

            UpdateMarkers(true);
        }

        static float Axis(float v)
        {
            float a = Mathf.Abs(v);
            if (a < DeadZone) return 0f;
            return Mathf.Sign(v) * Mathf.Clamp01((a - DeadZone) / (FullTilt - DeadZone));
        }

        bool IsUpFlick()
        {
            if (_histCount < 2) return false;
            float now = Time.unscaledTime;
            // 최근 FlickWindow 안의 가장 오래된 기록과 비교
            Vector2 newest = _pos, oldest = newest;
            float oldestT = now;
            for (int i = 0; i < _histCount; i++)
            {
                int k = (_histHead - 1 - i + HistoryLen) % HistoryLen;
                if (now - _histTime[k] > FlickWindow) break;
                oldest = _histPos[k];
                oldestT = _histTime[k];
            }
            float dt = Mathf.Max(0.016f, now - oldestT);
            float rise = newest.y - oldest.y;
            float side = Mathf.Abs(newest.x - oldest.x);
            return rise >= FlickMinRise && rise / dt >= FlickSpeed && rise > side * 0.8f;
        }

        void Record(Vector2 p)
        {
            _histPos[_histHead] = p;
            _histTime[_histHead] = Time.unscaledTime;
            _histHead = (_histHead + 1) % HistoryLen;
            _histCount = Mathf.Min(_histCount + 1, HistoryLen);
        }

        void Release()
        {
            _finger = -1;
            Horizontal = Vertical = 0f;
            UpdateMarkers(false);
        }

        void UpdateMarkers(bool show)
        {
            if (_ring == null) return;
            _ring.gameObject.SetActive(show);
            _dot.gameObject.SetActive(show);
            if (!show) return;
            // 화면 높이 비율 → 캔버스 좌표 (캔버스 높이 기준)
            var canvas = (RectTransform)_ring.parent;
            float h = canvas.rect.height;
            _ring.anchoredPosition = _anchor * h;
            _dot.anchoredPosition = _pos * h;
        }

        static Vector2 Norm(Vector2 screen) => screen / Mathf.Max(1f, Screen.height);

        /// <summary>터치가 있으면 터치, 없으면 마우스를 읽는다.</summary>
        void ReadPointer(out bool began, out bool held, out Vector2 pos, out int id)
        {
            began = held = false;
            pos = Vector2.zero;
            id = -1;

            if (Input.touchCount > 0)
            {
                // 추적 중인 손가락을 우선, 없으면 새로 닿은 손가락
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (_finger >= 0 && t.fingerId == _finger)
                    {
                        held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                        pos = t.position; id = t.fingerId;
                        return;
                    }
                }
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began)
                    {
                        began = held = true;
                        pos = t.position; id = t.fingerId;
                        return;
                    }
                }
                return;
            }

            if (Input.GetMouseButtonDown(0)) { began = held = true; pos = Input.mousePosition; id = -2; return; }
            if (Input.GetMouseButton(0)) { held = true; pos = Input.mousePosition; id = -2; }
        }

        static bool OverUI(int id)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return id == -2 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(id);
        }
    }
}
