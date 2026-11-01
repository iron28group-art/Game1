using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicTree
{
    /// <summary>
    /// 누르고 있는 동안 Held 가 true 인 버튼 (멀티터치 지원).
    /// 누른 순간은 ConsumePressed() 로 한 번만 읽는다 (입력 이벤트가 Update 뒤에 와도 놓치지 않게).
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        readonly HashSet<int> _pointers = new HashSet<int>();
        bool _pressed;
        Graphic _graphic;
        Color _normal;

        public bool Held => _pointers.Count > 0;

        void Awake()
        {
            _graphic = GetComponent<Graphic>();
            if (_graphic != null) _normal = _graphic.color;
        }

        public bool ConsumePressed()
        {
            bool p = _pressed;
            _pressed = false;
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointers.Add(e.pointerId);
            _pressed = true;
            Tint();
        }

        public void OnPointerUp(PointerEventData e) { _pointers.Remove(e.pointerId); Tint(); }
        public void OnPointerExit(PointerEventData e) { _pointers.Remove(e.pointerId); Tint(); }

        public void OnPointerEnter(PointerEventData e)
        {
            // 손가락을 누른 채 옆 버튼으로 미끄러져 들어와도 인정 (모바일 방향키 감각)
            if (Input.touchCount > 0 || Input.GetMouseButton(0))
            {
                _pointers.Add(e.pointerId);
                Tint();
            }
        }

        void OnDisable() { _pointers.Clear(); _pressed = false; Tint(); }

        void Tint()
        {
            if (_graphic == null) return;
            _graphic.color = Held
                ? new Color(_normal.r * 0.8f, _normal.g * 0.8f, _normal.b * 0.8f, Mathf.Min(1f, _normal.a + 0.25f))
                : _normal;
        }
    }
}
