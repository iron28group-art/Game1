using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using C = MagicTree.Climb.ClimbConfig;

namespace MagicTree.Climb
{
    /// <summary>
    /// 요술나무 오르기 — 화면(스프라이트·카메라·UI·입력)과 저장.
    /// 지형과 움직임 계산은 ClimbWorld 가 한다.
    /// </summary>
    public class ClimbGame : MonoBehaviour
    {
        // ── 그림 배치 보정 (생성 이미지에 맞춘 값) ──
        const float BranchSurfaceFromTop = 0.28f;   // 가지 그림 위쪽에서 밟는 면까지 (그림 높이 비율)
        const float BranchVisualExtra = 0.5f;       // 가지 그림을 충돌 폭보다 조금 길게
        const float VineWidth = 0.75f;
        const bool CaterpillarFacesRight = false;  // obj_caterpillar 그림은 머리가 왼쪽

        // 정렬 순서
        const int OrderBg = -100, OrderWind = -5, OrderVine = 0, OrderPlatform = 1, OrderProp = 2,
                  OrderDew = 3, OrderHazard = 4, OrderPlayer = 10;

        static readonly Color SkyColor = new Color(1f, 0.86f, 0.80f);
        static readonly Color PanelColor = new Color(0.36f, 0.24f, 0.30f, 0.35f);
        static readonly Color ButtonColor = new Color(1f, 0.95f, 0.9f, 0.35f);
        static readonly Color Warm = new Color(1f, 0.92f, 0.75f);

        const string KeyBest = "climb_best", KeyDew = "climb_dew", KeySeed = "climb_seed",
                     KeyCheckpoint = "climb_checkpoint", KeyStory = "climb_story_seen", KeyEnding = "climb_ending_seen";

        ClimbWorld _world;
        Camera _cam;
        float _camY;
        bool _playing;

        // 그림
        Transform _worldRoot;
        SpriteRenderer _player;
        readonly Dictionary<object, Transform> _views = new Dictionary<object, Transform>();
        readonly List<SpriteRenderer> _bgTiles = new List<SpriteRenderer>();
        Sprite _bgSprite;
        float _squash = 1f;
        float _climbAnim;

        // UI
        RectTransform _root;
        Text _heightText, _bestText, _dewText, _toastText;
        TouchControls _touch;
        GameObject _title, _hint;
        bool _vineHintShown;
        Coroutine _toastRoutine;

        SoundFx _sfx;
        Text _muteLabel;
        float _climbStep;

        int _dew;
        float _best;
        int _area = -1;

        void Awake()
        {
            _best = PlayerPrefs.GetFloat(KeyBest, 0f);
            _dew = PlayerPrefs.GetInt(KeyDew, 0);

            _sfx = gameObject.AddComponent<SoundFx>();
            SetupCamera();
            _worldRoot = new GameObject("World").transform;
            _worldRoot.SetParent(transform, false);
            BuildBackground();
            BuildUI();
            StartWorld();
            ShowTitle();
        }

        // ───────────────────────── 시작 / 저장 ─────────────────────────

        void StartWorld()
        {
            int seed = PlayerPrefs.GetInt(KeySeed, 0);
            if (seed == 0) { seed = Random.Range(1, int.MaxValue); PlayerPrefs.SetInt(KeySeed, seed); }
            float checkpoint = PlayerPrefs.GetFloat(KeyCheckpoint, 0f);

            _world = new ClimbWorld(seed);
            _world.Jumped += () => { _squash = 1.15f; _sfx.Play(SoundFx.Kind.Jump, 0.7f); };
            _world.Landed += () => { _squash = 0.85f; _sfx.Play(SoundFx.Kind.Land, 0.5f); };
            _world.Bounced += OnBounced;
            _world.Hit += () => { ShowToast("앗, 폭신!"); _sfx.Play(SoundFx.Kind.Hit); };
            _world.DewCollected += OnDewCollected;
            _world.CheckpointReached += OnCheckpoint;
            _world.GoalReached += OnGoal;

            // 같은 seed 면 같은 나무가 다시 만들어진다 → 저장된 쉼터에서 이어 하기
            _world.GenerateUpTo(checkpoint + C.GenerateAhead);
            _world.CheckpointHeight = checkpoint;
            _world.MaxHeight = checkpoint;
            _world.ResetToCheckpoint();
            _camY = _world.Pos.y + C.CameraLead;

            _player = MakeSprite("Tori", _worldRoot, Art("tori_idle", "char_tori", new Color(1f, 0.7f, 0.55f)), OrderPlayer);
        }

        void Save()
        {
            PlayerPrefs.SetFloat(KeyBest, _best);
            PlayerPrefs.SetInt(KeyDew, _dew);
            PlayerPrefs.SetFloat(KeyCheckpoint, _world.CheckpointHeight);
            PlayerPrefs.Save();
        }

        void OnApplicationPause(bool paused) { if (paused && _world != null) Save(); }
        void OnApplicationQuit() { if (_world != null) Save(); }

        // ───────────────────────── 매 프레임 ─────────────────────────

        void Update()
        {
            float dt = Time.deltaTime;
            if (_playing)
            {
                _world.Step(dt, ReadInput());
                _world.GenerateUpTo(_world.Pos.y + C.GenerateAhead);
                _world.Cleanup(RemoveView);

                if (_world.MaxHeight > _best) _best = _world.MaxHeight;
                int area = C.AreaFor(_world.Pos.y);
                if (area != _area)
                {
                    if (_area >= 0 && area > _area) ShowToast($"~ {C.AreaNames[area]} ~");
                    _area = area;
                }
            }

            UpdateCamera(dt);
            SyncViews();
            UpdatePlayerView(dt);
            UpdateBackground();
            UpdateHud();
        }

        ClimbInput ReadInput()
        {
            float h = 0f, v = 0f;
            // 화면 끌기(TouchControls) + 키보드(에디터·PC)
            float h = _touch.Horizontal, v = _touch.Vertical;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) h -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) v -= 1f;
            bool jump = _touch.ConsumeJump() | Input.GetKeyDown(KeyCode.Space);
            return new ClimbInput { Horizontal = Mathf.Clamp(h, -1f, 1f), Vertical = Mathf.Clamp(v, -1f, 1f), JumpPressed = jump };
        }

        // ───────────────────────── 이벤트 ─────────────────────────

        void OnBounced()
        {
            _squash = 1.25f;
            _sfx.Play(SoundFx.Kind.Bounce, 0.8f);
            foreach (var m in _world.Mushrooms)
                if (Mathf.Abs(m.X - _world.Pos.x) < 0.8f && _views.TryGetValue(m, out var t))
                    StartCoroutine(Squish(t));
        }

        void OnDewCollected(Dew d)
        {
            _dew++;
            _sfx.Play(SoundFx.Kind.Dew, 0.6f);
            if (_views.TryGetValue(d, out var t))
            {
                _views.Remove(d);
                StartCoroutine(PopAndDestroy(t));
            }
        }

        void OnCheckpoint(float y)
        {
            if (Mathf.Abs(y - C.GoalHeight) > 0.01f) ShowToast("반딧불 쉼터에 도착했어요");
            _sfx.Play(SoundFx.Kind.Checkpoint, 0.8f);
            Save();
        }

        void OnGoal()
        {
            Save();
            if (PlayerPrefs.GetInt(KeyEnding, 0) == 1) { ShowToast("별 열매가 반짝이고 있어요"); return; }
            PlayerPrefs.SetInt(KeyEnding, 1);
            _playing = false;
            _touch.enabled = false;
            _sfx.Play(SoundFx.Kind.Ending);
            StartCoroutine(PlayPanels(Ending, EndingArt, "엔딩", () =>
            {
                _touch.enabled = true;
                _playing = true;
                ShowToast("더 높이, 별빛 너머로!");
            }));
        }

        // ───────────────────────── 카메라 / 배경 ─────────────────────────

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                _cam = go.GetComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = SkyColor;
            _cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        float ViewHeight => C.ViewWidth / Mathf.Max(0.3f, _cam.aspect);

        void UpdateCamera(float dt)
        {
            _cam.orthographicSize = ViewHeight * 0.5f;
            float target = _world.Pos.y + C.CameraLead;
            target = Mathf.Max(target, ViewHeight * 0.5f - 2.2f); // 땅 아래는 조금만 보이게
            _camY = Mathf.Lerp(_camY, target, 1f - Mathf.Exp(-7f * dt));
            _cam.transform.position = new Vector3(0f, _camY, -10f);
        }

        void BuildBackground()
        {
            _bgSprite = ArtLibrary.HasApproved("bg_trunk_tile") ? ArtLibrary.Get("bg_trunk_tile", SkyColor, false)
                      : ArtLibrary.HasApproved("bg_climb") ? ArtLibrary.Get("bg_climb", SkyColor, false) : null;
            if (_bgSprite == null) return;
            for (int i = 0; i < 3; i++)
                _bgTiles.Add(MakeSprite("Bg" + i, transform, _bgSprite, OrderBg));
        }

        /// <summary>
        /// 줄기 배경을 세로로 이어 붙인다. 홀수 번째 조각은 위아래를 뒤집어 이음매가 맞닿게 한다.
        /// 배경은 세상보다 천천히 움직인다(시차 0.45).
        /// </summary>
        void UpdateBackground()
        {
            if (_bgSprite == null) return;
            const float parallax = 0.45f;
            float w = C.ViewWidth + 0.2f;
            float h = w / ArtLibrary.Aspect(_bgSprite);
            float local = _camY * parallax;                      // 배경 좌표계에서의 카메라 높이
            int first = Mathf.FloorToInt((local - ViewHeight * 0.5f) / h);
            for (int i = 0; i < _bgTiles.Count; i++)
            {
                int k = first + i;
                var sr = _bgTiles[i];
                FitSize(sr, w, h);
                sr.flipY = (k & 1) != 0;
                float yLocal = k * h + h * 0.5f;
                sr.transform.position = new Vector3(0f, _camY + (yLocal - local), 5f);
            }
        }

        // ───────────────────────── 월드 그림 ─────────────────────────

        void SyncViews()
        {
            foreach (var p in _world.Platforms)
                if (!_views.ContainsKey(p)) _views[p] = MakePlatform(p);
            foreach (var v in _world.Vines)
                if (!_views.ContainsKey(v)) _views[v] = MakeVine(v);
            foreach (var m in _world.Mushrooms)
                if (!_views.ContainsKey(m)) _views[m] = MakeProp("Mushroom", Art("obj_mushroom", null, new Color(1f, 0.6f, 0.65f)), 1.15f, new Vector2(m.X, m.On.Y), OrderProp);
            foreach (var d in _world.Dews)
            {
                if (d.Collected) continue;
                if (!_views.TryGetValue(d, out var t))
                    _views[d] = t = MakeProp("Dew", Art("icon_mana", null, new Color(0.6f, 0.85f, 1f)), 0.6f, new Vector2(d.X, d.Y - 0.3f), OrderDew);
                t.position = new Vector3(d.X, d.Y - 0.3f + Mathf.Sin(Time.time * 3f + d.X) * 0.08f, 0f);
            }
            foreach (var hz in _world.Hazards)
            {
                if (!_views.TryGetValue(hz, out var t))
                {
                    bool cat = hz.Kind == HazardKind.Caterpillar;
                    _views[hz] = t = MakeProp(cat ? "Caterpillar" : "Pinecone",
                        cat ? Art("obj_caterpillar", null, new Color(0.6f, 0.9f, 0.7f)) : Art("obj_pinecone", null, new Color(0.7f, 0.5f, 0.35f)),
                        cat ? 1.0f : 0.62f, new Vector2(hz.X, hz.Y), OrderHazard);
                }
                var sr = t.GetComponentInChildren<SpriteRenderer>();
                if (hz.Kind == HazardKind.Caterpillar)
                {
                    sr.flipX = (hz.Dir > 0) != CaterpillarFacesRight;
                    t.position = new Vector3(hz.X, hz.Y, 0f);
                }
                else
                {
                    t.position = new Vector3(hz.X, hz.Y, 0f);
                    sr.transform.localRotation = Quaternion.Euler(0f, 0f, hz.Spin);
                }
            }
            foreach (var w in _world.Winds)
                if (!_views.ContainsKey(w)) _views[w] = MakeWind(w);
        }

        void RemoveView(object o)
        {
            if (_views.TryGetValue(o, out var t))
            {
                _views.Remove(o);
                if (t != null) Destroy(t.gameObject);
            }
        }

        Transform MakePlatform(Platform p)
        {
            var root = new GameObject(p.IsCheckpoint ? "Checkpoint" : "Branch").transform;
            root.SetParent(_worldRoot, false);
            root.position = new Vector3(p.X, p.Y, 0f);
            var branch = Art("obj_branch", null, new Color(0.65f, 0.48f, 0.38f));
            bool art = ArtLibrary.HasApproved("obj_branch");

            if (!p.IsCheckpoint)
            {
                AddBranchPiece(root, branch, art, 0f, p.Width + BranchVisualExtra, false);
                return root;
            }

            // 쉼터: 짧은 가지를 겹쳐 화면 폭 전체를 덮고, 왼쪽에 반딧불 등불
            float piece = 3.2f;
            int n = Mathf.CeilToInt((p.Width + 0.6f) / (piece * 0.8f));
            float start = -(n - 1) * piece * 0.8f * 0.5f;
            for (int i = 0; i < n; i++)
                AddBranchPiece(root, branch, art, start + i * piece * 0.8f, piece, (i & 1) == 1);
            var lamp = MakeSprite("Lantern", root, Art("obj_firefly", null, Warm), OrderProp);
            FitHeight(lamp, 1.1f);
            lamp.transform.localPosition = new Vector3(-3.6f, 0.55f, 0f);
            lamp.gameObject.AddComponent<Glow>();
            if (p.IsGoal)
            {
                var star = MakeSprite("StarFruit", root, Art("obj_star_fruit", null, new Color(1f, 0.9f, 0.5f)), OrderProp);
                FitHeight(star, 1.3f);
                star.transform.localPosition = new Vector3(2.8f, 0.85f, 0f);
                star.gameObject.AddComponent<Glow>();
            }
            return root;
        }

        void AddBranchPiece(Transform root, Sprite sprite, bool art, float x, float width, bool flip)
        {
            var sr = MakeSprite("Piece", root, sprite, OrderPlatform);
            if (art)
            {
                FitWidth(sr, width);
                float h = width / ArtLibrary.Aspect(sprite);
                sr.transform.localPosition = new Vector3(x, -h * 0.5f + h * BranchSurfaceFromTop, 0f);
            }
            else
            {
                FitSize(sr, width, C.PlatformThickness);
                sr.transform.localPosition = new Vector3(x, -C.PlatformThickness * 0.5f, 0f);
            }
            sr.flipX = flip;
        }

        Transform MakeVine(Vine v)
        {
            var root = new GameObject("Vine").transform;
            root.SetParent(_worldRoot, false);
            root.position = new Vector3(v.X, v.Bottom, 0f);
            var sprite = Art("obj_vine", null, new Color(0.55f, 0.8f, 0.5f));
            float total = v.Top - v.Bottom + 0.3f;
            float seg = ArtLibrary.HasApproved("obj_vine") ? VineWidth / ArtLibrary.Aspect(sprite) : 1f;
            int n = Mathf.Max(1, Mathf.CeilToInt(total / seg));
            float segH = total / n;                              // 정수 개로 나눠 살짝 늘이거나 줄임
            for (int i = 0; i < n; i++)
            {
                var sr = MakeSprite("Seg", root, sprite, OrderVine);
                FitSize(sr, VineWidth, segH + 0.02f);
                sr.flipX = (i & 1) == 1;
                sr.transform.localPosition = new Vector3(0f, segH * (i + 0.5f), 0f);
            }
            return root;
        }

        Transform MakeWind(Wind w)
        {
            var root = new GameObject("Wind").transform;
            root.SetParent(_worldRoot, false);
            var dot = Art("__wind", null, new Color(1f, 1f, 0.95f, 0.7f));
            for (int i = 0; i < 14; i++)
            {
                var sr = MakeSprite("Pollen", root, dot, OrderWind);
                FitSize(sr, 0.12f, 0.12f);
                var drift = sr.gameObject.AddComponent<Drift>();
                drift.Init(w, i / 14f);
            }
            return root;
        }

        Transform MakeProp(string name, Sprite sprite, float width, Vector2 bottom, int order)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_worldRoot, false);
            root.position = new Vector3(bottom.x, bottom.y, 0f);
            var sr = MakeSprite("Sprite", root, sprite, order);
            FitWidth(sr, width);
            float h = width / ArtLibrary.Aspect(sprite);
            sr.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            return root;
        }

        void UpdatePlayerView(float dt)
        {
            var w = _world;
            string id;
            if (w.Mode == PlayerMode.Climb) id = "tori_climb";
            else if (w.Mode == PlayerMode.Air) id = "tori_jump";
            else id = Mathf.Abs(w.Vel.x) > 0.3f ? "tori_run" : "tori_idle";
            var sprite = Art(id, "char_tori", new Color(1f, 0.7f, 0.55f));
            if (_player.sprite != sprite) _player.sprite = sprite;

            _squash = Mathf.MoveTowards(_squash, 1f, dt * 2.5f);
            float bob = 0f;
            if (w.Mode == PlayerMode.Ground && Mathf.Abs(w.Vel.x) > 0.3f) bob = Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.08f;

            float h = C.SpriteHeight;
            float wd = h * ArtLibrary.Aspect(sprite);
            var t = _player.transform;
            var size = sprite.bounds.size;
            t.localScale = new Vector3(wd / size.x * (2f - _squash), h / size.y * _squash, 1f);
            t.position = new Vector3(w.Pos.x, w.Pos.y + h * _squash * 0.5f + bob, -1f);

            if (w.Mode == PlayerMode.Climb)
            {
                _climbAnim += Mathf.Abs(w.Vel.y) * dt;
                _climbStep += Mathf.Abs(w.Vel.y) * dt;
                if (_climbStep > 0.6f) { _climbStep = 0f; _sfx.Play(SoundFx.Kind.Climb, 0.5f); }
                _player.flipX = Mathf.Repeat(_climbAnim, 0.6f) > 0.3f; // 오를 때 좌우로 번갈아
            }
            else _player.flipX = w.Facing < 0;

            // 맞은 뒤 잠깐 깜빡임
            float a = w.InvincibleTimer > 0f && Mathf.Repeat(Time.time, 0.16f) < 0.08f ? 0.4f : 1f;
            _player.color = new Color(1f, 1f, 1f, a);

            // 덩굴 근처에서만 ▲▼ 버튼 표시
            bool nearVine = w.Mode == PlayerMode.Climb;
            if (!nearVine)
                foreach (var v in w.Vines)
                    if (Mathf.Abs(v.X - w.Pos.x) < 0.7f && w.Pos.y >= v.Bottom - 0.3f && w.Pos.y <= v.Top + 0.3f) { nearVine = true; break; }
            if (nearVine && _playing && !_vineHintShown)
            {
                _vineHintShown = true;
                ShowToast("덩굴: 위아래로 천천히 끌어서 오르기");
            }
        }

        // ───────────────────────── UI ─────────────────────────

        void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("ClimbCanvas");
            canvas.transform.SetParent(transform, false);
            _root = (RectTransform)canvas.transform;

            var top = UIFactory.CreateImage("TopPanel", _root, null, PanelColor);
            top.raycastTarget = false;
            var trt = top.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = Vector2.zero; trt.sizeDelta = new Vector2(0, 190);

            _heightText = UIFactory.CreateText("Height", _root, "", 76, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(_heightText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -75), new Vector2(600, 100));
            _bestText = UIFactory.CreateText("Best", _root, "", 34, TextAnchor.MiddleCenter, Warm);
            UIFactory.Place(_bestText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(600, 50));

            var dewIcon = UIFactory.CreateImage("DewIcon", _root, Art("icon_mana", null, new Color(0.6f, 0.85f, 1f)));
            dewIcon.raycastTarget = false;
            UIFactory.Place(dewIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-190, -95), new Vector2(70, 70));
            _dewText = UIFactory.CreateText("Dew", _root, "", 44, TextAnchor.MiddleLeft, Color.white);
            UIFactory.Place(_dewText.rectTransform, new Vector2(1f, 1f), new Vector2(-75, -95), new Vector2(160, 60));

            var mute = UIFactory.CreateButton("Mute", _root, Art("__round", null, Color.white), ButtonColor);
            UIFactory.Place((RectTransform)mute.transform, new Vector2(0f, 1f), new Vector2(90, -95), new Vector2(100, 100));
            _muteLabel = UIFactory.CreateText("Label", mute.transform, "", 30, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(_muteLabel.rectTransform);
            mute.onClick.AddListener(() => { SoundFx.Muted = !SoundFx.Muted; RefreshMute(); });
            RefreshMute();

            _toastText = UIFactory.CreateText("Toast", _root, "", 54, TextAnchor.MiddleCenter, Warm);
            UIFactory.Place(_toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(1000, 120));
            _toastText.gameObject.SetActive(false);

            // 조작: 화면 어디서나 끌기 (버튼 없음)
            _touch = gameObject.AddComponent<TouchControls>();
            _touch.Init(_root, Art("__round", null, Color.white));
            _touch.enabled = false;

            // 처음 몇 초 보여 주는 조작 안내
            _hint = UIFactory.CreateText("ControlHint", _root,
                "← 좌우로 끌어서 이동 →\n위로 휙 올리면 점프!", 46, TextAnchor.MiddleCenter, Color.white).gameObject;
            UIFactory.Place((RectTransform)_hint.transform, new Vector2(0.5f, 0f), new Vector2(0, 260), new Vector2(1000, 150));
            _hint.SetActive(false);
        }

        void RefreshMute() => _muteLabel.text = SoundFx.Muted ? "소리\n끔" : "소리\n켬";

        void UpdateHud()
        {
            _heightText.text = $"{Mathf.FloorToInt(Mathf.Max(0f, _world.Pos.y))} m";
            _bestText.text = $"최고 {Mathf.FloorToInt(_best)} m   ·   {C.AreaNames[Mathf.Max(0, _area)]}";
            _dewText.text = _dew.ToString();
        }

        void ShowToast(string msg)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(msg));
        }

        IEnumerator ToastRoutine(string msg)
        {
            _toastText.text = msg;
            _toastText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
            _toastText.gameObject.SetActive(false);
        }

        IEnumerator ShowHint()
        {
            _hint.SetActive(true);
            yield return new WaitForSeconds(4f);
            _hint.SetActive(false);
        }

        // ───────────────────────── 타이틀 / 이야기 ─────────────────────────

        static readonly string[] Story =
        {
            "숲 마을의 꼬마 토리는\n밤하늘에서 별똥별 하나가\n요술나무 꼭대기로 떨어지는 걸 보았어요.",
            "\"요술나무에 별 열매가 열리면\n소원 하나가 이뤄진단다.\"\n할머니가 들려주신 이야기예요.",
            "토리는 작은 가방에 물병을 챙겨\n요술나무 밑동으로 갔어요.",
            "아픈 숲 친구들이 다시 웃을 수 있게.\n토리는 꼭대기를 향해 오르기 시작했어요.",
        };
        static readonly string[] StoryArt = { "story_1", "story_2", "story_3", "story_4" };

        static readonly string[] Ending =
        {
            "구름 위, 별빛이 내려앉은 가지 끝에서\n토리는 반짝이는 별 열매를 찾았어요.",
            "\"숲 친구들이 다시 웃게 해 주세요.\"\n별 열매가 따뜻하게 빛났어요.",
            "다음 날 아침, 숲에는 웃음소리가 가득했어요.\n토리의 작은 용기 덕분이에요.",
        };
        static readonly string[] EndingArt = { "ending_1", "ending_1", "ending_2" };

        void ShowTitle()
        {
            _title = UIFactory.CreateRect("Title", _root).gameObject;
            var rt = (RectTransform)_title.transform;
            UIFactory.Stretch(rt);

            var bgSprite = ArtLibrary.HasApproved("bg_climb") ? ArtLibrary.Get("bg_climb", SkyColor, false) : null;
            var bg = UIFactory.CreateImage("TitleBg", rt, bgSprite, bgSprite != null ? Color.white : SkyColor);
            bg.preserveAspect = false;
            UIFactory.Stretch(bg.rectTransform);
            if (bgSprite != null)
            {
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = ArtLibrary.Aspect(bgSprite);
            }

            var shade = UIFactory.CreateImage("Shade", rt, null, new Color(0.3f, 0.18f, 0.25f, 0.25f));
            UIFactory.Stretch(shade.rectTransform);

            var title = UIFactory.CreateText("TitleText", rt, "요술나무 오르기", 110, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(1000, 160));

            var tori = UIFactory.CreateImage("Tori", rt, Art("char_tori", null, new Color(1f, 0.7f, 0.55f)));
            tori.raycastTarget = false;
            UIFactory.Place(tori.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 700), new Vector2(520, 620));

            float cp = _world.CheckpointHeight;
            var hint = UIFactory.CreateText("Hint", rt, cp > 0 ? $"화면을 눌러 이어 하기 ({Mathf.FloorToInt(cp)} m 쉼터)" : "화면을 눌러 시작",
                48, TextAnchor.MiddleCenter, Warm);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 280), new Vector2(1000, 80));
            hint.gameObject.AddComponent<Glow>();

            var btn = shade.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            bool storySeen = PlayerPrefs.GetInt(KeyStory, 0) == 1;
            btn.onClick.AddListener(() =>
            {
                if (storySeen) BeginPlay();
                else StartCoroutine(PlayPanels(Story, StoryArt, "이야기", () =>
                {
                    PlayerPrefs.SetInt(KeyStory, 1);
                    BeginPlay();
                }));
            });
        }

        /// <summary>그림 + 글 장면을 차례로 보여 준다 (이야기·엔딩 공용). 그림이 없으면 토리 그림을 쓴다.</summary>
        IEnumerator PlayPanels(string[] lines, string[] artIds, string label, System.Action done)
        {
            var layer = UIFactory.CreateRect("Panels", _root);
            UIFactory.Stretch(layer);

            var bg = UIFactory.CreateImage("PanelBg", layer, null, new Color(1f, 0.93f, 0.86f));
            UIFactory.Stretch(bg.rectTransform);
            var pic = UIFactory.CreateImage("Picture", layer, null);
            pic.raycastTarget = false;
            UIFactory.Place(pic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(940, 940));
            var text = UIFactory.CreateText("PanelText", layer, "", 48, TextAnchor.MiddleCenter, new Color(0.42f, 0.28f, 0.3f));
            text.GetComponent<Shadow>().enabled = false;
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -410), new Vector2(980, 260));
            var next = UIFactory.CreateText("Next", layer, "", 36, TextAnchor.MiddleCenter, new Color(0.7f, 0.5f, 0.5f));
            next.GetComponent<Shadow>().enabled = false;
            UIFactory.Place(next.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(900, 60));

            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            bool clicked = false;
            btn.onClick.AddListener(() => clicked = true);

            for (int i = 0; i < lines.Length; i++)
            {
                string id = i < artIds.Length ? artIds[i] : null;
                bool hasArt = id != null && ArtLibrary.HasApproved(id);
                pic.sprite = hasArt ? ArtLibrary.Get(id, Color.white, false) : Art("char_tori", null, new Color(1f, 0.7f, 0.55f));
                pic.preserveAspect = true;
                pic.rectTransform.sizeDelta = hasArt ? new Vector2(940, 940) : new Vector2(460, 560);
                text.text = lines[i];
                next.text = $"{label} {i + 1}/{lines.Length}   ·   눌러서 계속 ▶";
                clicked = false;
                yield return new WaitUntil(() => clicked);
            }
            Destroy(layer.gameObject);
            if (_title != null) _title.SetActive(false);
            done?.Invoke();
        }

        void BeginPlay()
        {
            if (_title != null) Destroy(_title);
            _touch.enabled = true;
            StartCoroutine(ShowHint());
            _playing = true;
            _area = C.AreaFor(_world.Pos.y);
            ShowToast($"~ {C.AreaNames[_area]} ~");
        }

        // ───────────────────────── 도우미 ─────────────────────────

        static readonly Dictionary<string, Sprite> Generated = new Dictionary<string, Sprite>();

        /// <summary>승인 이미지 → (없으면) 대체 id → (없으면) 색 도형. "__" 로 시작하면 항상 도형.</summary>
        static Sprite Art(string id, string fallbackId, Color color)
        {
            if (!id.StartsWith("__") && ArtLibrary.HasApproved(id)) return ArtLibrary.Get(id, color);
            if (fallbackId != null && ArtLibrary.HasApproved(fallbackId)) return ArtLibrary.Get(fallbackId, color);
            return ArtLibrary.Get(id + "#placeholder", color, circle: id != "obj_branch");
        }

        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void FitWidth(SpriteRenderer sr, float w)
        {
            float k = w / Mathf.Max(0.0001f, sr.sprite.bounds.size.x);
            sr.transform.localScale = new Vector3(k, k, 1f);
        }

        static void FitHeight(SpriteRenderer sr, float h)
        {
            float k = h / Mathf.Max(0.0001f, sr.sprite.bounds.size.y);
            sr.transform.localScale = new Vector3(k, k, 1f);
        }

        static void FitSize(SpriteRenderer sr, float w, float h)
        {
            var s = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(w / Mathf.Max(0.0001f, s.x), h / Mathf.Max(0.0001f, s.y), 1f);
        }

        IEnumerator Squish(Transform t)
        {
            var s0 = t.localScale;
            for (float e = 0; e < 0.25f && t != null; e += Time.deltaTime)
            {
                float k = Mathf.Sin(e / 0.25f * Mathf.PI) * 0.3f;
                t.localScale = new Vector3(s0.x * (1 + k), s0.y * (1 - k), 1f);
                yield return null;
            }
            if (t != null) t.localScale = s0;
        }

        IEnumerator PopAndDestroy(Transform t)
        {
            var s0 = t.localScale;
            for (float e = 0; e < 0.2f && t != null; e += Time.deltaTime)
            {
                t.localScale = s0 * (1f + e * 3f);
                foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>())
                    sr.color = new Color(1, 1, 1, 1f - e / 0.2f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }

    /// <summary>은은하게 밝아졌다 어두워지는 효과 (등불, 안내 문구).</summary>
    public class Glow : MonoBehaviour
    {
        SpriteRenderer _sr;
        Graphic _g;
        float _phase;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _g = GetComponent<Graphic>();
            _phase = Random.value * 6f;
        }

        void Update()
        {
            float a = 0.7f + 0.3f * Mathf.Sin(Time.time * 2.4f + _phase);
            if (_sr != null) { var c = _sr.color; c.a = a; _sr.color = c; }
            if (_g != null) { var c = _g.color; c.a = a; _g.color = c; }
        }
    }

    /// <summary>꽃가루 바람 구간에서 옆으로 흘러가는 점.</summary>
    public class Drift : MonoBehaviour
    {
        Wind _w;
        float _t;

        public void Init(Wind w, float offset)
        {
            _w = w;
            _t = offset;
        }

        void Update()
        {
            if (_w == null) return;
            _t = Mathf.Repeat(_t + Time.deltaTime * 0.35f, 1f);
            float x = Mathf.Lerp(-C.ViewWidth * 0.55f, C.ViewWidth * 0.55f, _w.Dir > 0 ? _t : 1f - _t);
            float y = Mathf.Lerp(_w.Bottom, _w.Top, Mathf.Repeat(_t * 7.3f + transform.GetSiblingIndex() * 0.37f, 1f));
            transform.position = new Vector3(x, y + Mathf.Sin(_t * 20f) * 0.1f, 0f);
        }
    }
}
