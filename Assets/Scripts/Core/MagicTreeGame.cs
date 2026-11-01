using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MagicTree
{
    /// <summary>
    /// 요술나무 키우기 (방치형 클리커) 메인 로직.
    /// - 나무를 터치하면 마나 이슬을 얻는다.
    /// - 누적 마나로 나무가 씨앗 → 새싹 → 어린 나무 → 큰 나무 → 요술나무로 자란다.
    /// - 어린 나무부터 요술 열매가 열리고, 터치하면 큰 보너스.
    /// - 마나로 물뿌리개/요정 친구/햇살을 업그레이드한다.
    /// </summary>
    public class MagicTreeGame : MonoBehaviour
    {
        GameState _state;
        int _stage = -1;
        float _fruitTimer;
        float _saveTimer;

        // UI refs
        RectTransform _root;
        Image _treeImage;
        RectTransform _treeRect;
        Text _manaText, _rateText, _stageText, _toastText;
        RectTransform _progressFill;
        RectTransform _fruitLayer;
        readonly List<GameObject> _fruits = new List<GameObject>();
        readonly Dictionary<UpgradeType, (Button btn, Text title, Text cost)> _upgradeUI =
            new Dictionary<UpgradeType, (Button, Text, Text)>();

        static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

        void Awake()
        {
            _state = GameState.Load();
            BuildUI();
            ApplyOfflineEarnings();
            RefreshAll(force: true);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            double mps = GameConfig.ManaPerSecond(_state);
            if (mps > 0) AddMana(mps * dt);

            if (_stage >= GameConfig.FruitMinStage && _fruits.Count < GameConfig.MaxFruits)
            {
                _fruitTimer += dt;
                if (_fruitTimer >= GameConfig.FruitInterval(_state))
                {
                    _fruitTimer = 0;
                    SpawnFruit();
                }
            }

            _saveTimer += dt;
            if (_saveTimer >= 5f) { _saveTimer = 0; _state.Save(); }

            RefreshAll();
        }

        void OnApplicationPause(bool paused) { if (paused) _state.Save(); }
        void OnApplicationQuit() => _state.Save();

        // ───────────────────────── 게임 로직 ─────────────────────────

        void AddMana(double amount)
        {
            _state.mana += amount;
            _state.totalMana += amount;
        }

        void OnTreeTapped()
        {
            double gain = GameConfig.TapPower(_state);
            AddMana(gain);
            StartCoroutine(Pulse(_treeRect, 1.06f));
            SpawnFloatingText("+" + GameConfig.Format(gain), _treeRect.anchoredPosition + new Vector2(UnityEngine.Random.Range(-120f, 120f), 150f), Color.white);
        }

        void SpawnFruit()
        {
            var btn = UIFactory.CreateButton("Fruit", _fruitLayer, ArtLibrary.Get("fruit_magic", new Color(1f, 0.45f, 0.75f)), Color.white);
            var rt = (RectTransform)btn.transform;
            // 나무 윗부분(수관) 주변 랜덤 위치
            var pos = _treeRect.anchoredPosition + new Vector2(UnityEngine.Random.Range(-260f, 260f), UnityEngine.Random.Range(80f, 380f));
            UIFactory.Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(110, 110));
            var go = btn.gameObject;
            btn.onClick.AddListener(() => CollectFruit(go));
            _fruits.Add(go);
            StartCoroutine(Pulse(rt, 1.3f));
        }

        void CollectFruit(GameObject fruit)
        {
            double gain = GameConfig.TapPower(_state) * GameConfig.FruitTapMultiplier;
            AddMana(gain);
            SpawnFloatingText("+" + GameConfig.Format(gain), ((RectTransform)fruit.transform).anchoredPosition, Gold);
            _fruits.Remove(fruit);
            Destroy(fruit);
        }

        void TryBuy(UpgradeType type)
        {
            int level = GetLevel(type);
            double cost = GameConfig.Cost(type, level);
            if (_state.mana < cost) return;
            _state.mana -= cost;
            switch (type)
            {
                case UpgradeType.Watering: _state.wateringLevel++; break;
                case UpgradeType.Fairy: _state.fairyLevel++; break;
                case UpgradeType.Sunlight: _state.sunlightLevel++; break;
            }
            _state.Save();
            RefreshAll(force: true);
        }

        int GetLevel(UpgradeType t) =>
            t == UpgradeType.Watering ? _state.wateringLevel :
            t == UpgradeType.Fairy ? _state.fairyLevel : _state.sunlightLevel;

        void ApplyOfflineEarnings()
        {
            if (_state.lastSaveUnix <= 0) return;
            double secs = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _state.lastSaveUnix;
            secs = Math.Min(Math.Max(0, secs), GameConfig.MaxOfflineSeconds);
            double gain = GameConfig.ManaPerSecond(_state) * secs;
            if (gain >= 1)
            {
                AddMana(gain);
                ShowToast($"요정들이 자리를 비운 동안\n마나 이슬 {GameConfig.Format(gain)}개를 모았어요!");
            }
        }

        // ───────────────────────── 화면 갱신 ─────────────────────────

        void RefreshAll(bool force = false)
        {
            _manaText.text = "마나 이슬 " + GameConfig.Format(_state.mana);
            double mps = GameConfig.ManaPerSecond(_state);
            _rateText.text = $"터치 +{GameConfig.Format(GameConfig.TapPower(_state))}   |   초당 +{mps:0.0}";

            int stage = GameConfig.StageFor(_state.totalMana);
            if (stage != _stage)
            {
                bool grewUp = _stage >= 0 && stage > _stage;
                _stage = stage;
                _treeImage.sprite = ArtLibrary.Get("tree_stage_" + stage, StageColor(stage));
                float s = 0.45f + stage * 0.14f; // 단계별로 커짐
                _treeRect.sizeDelta = new Vector2(900, 900) * s;
                if (grewUp)
                {
                    ShowToast($"{GameConfig.StageNames[stage]}(으)로 자랐어요!");
                    StartCoroutine(Pulse(_treeRect, 1.2f));
                }
            }

            bool maxStage = _stage >= GameConfig.StageThresholds.Length - 1;
            if (maxStage)
            {
                _stageText.text = $"{GameConfig.StageNames[_stage]} (최종 단계)";
                SetProgress(1f);
            }
            else
            {
                double lo = GameConfig.StageThresholds[_stage], hi = GameConfig.StageThresholds[_stage + 1];
                _stageText.text = $"{GameConfig.StageNames[_stage]}  →  {GameConfig.StageNames[_stage + 1]}  ({GameConfig.Format(_state.totalMana)}/{GameConfig.Format(hi)})";
                SetProgress((float)((_state.totalMana - lo) / (hi - lo)));
            }

            foreach (var kv in _upgradeUI)
            {
                int lv = GetLevel(kv.Key);
                double cost = GameConfig.Cost(kv.Key, lv);
                kv.Value.title.text = $"{UpgradeName(kv.Key)} Lv.{lv}";
                kv.Value.cost.text = GameConfig.Format(cost);
                kv.Value.btn.interactable = _state.mana >= cost;
            }
        }

        void SetProgress(float t)
        {
            _progressFill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
        }

        static string UpgradeName(UpgradeType t) =>
            t == UpgradeType.Watering ? "물뿌리개" : t == UpgradeType.Fairy ? "요정 친구" : "햇살";

        static string UpgradeDesc(UpgradeType t) =>
            t == UpgradeType.Watering ? "터치당 +1" : t == UpgradeType.Fairy ? "초당 +0.5" : "열매 더 빨리";

        static string UpgradeIcon(UpgradeType t) =>
            t == UpgradeType.Watering ? "icon_watering_can" : t == UpgradeType.Fairy ? "icon_fairy" : "icon_sunlight";

        static Color StageColor(int stage)
        {
            Color[] c =
            {
                new Color(0.55f, 0.38f, 0.2f),  // 씨앗
                new Color(0.55f, 0.85f, 0.4f),  // 새싹
                new Color(0.3f, 0.75f, 0.35f),  // 어린 나무
                new Color(0.15f, 0.6f, 0.3f),   // 큰 나무
                new Color(0.55f, 0.45f, 0.95f), // 요술나무
            };
            return c[Mathf.Clamp(stage, 0, c.Length - 1)];
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("MagicTreeCanvas");
            canvas.transform.SetParent(transform, false);
            _root = (RectTransform)canvas.transform;

            // 배경
            var bg = UIFactory.CreateImage("Background", _root, ArtLibrary.Get("bg_meadow", new Color(0.62f, 0.85f, 0.95f), circle: false));
            bg.preserveAspect = false;
            UIFactory.Stretch(bg.rectTransform);
            bg.raycastTarget = false;
            if (!ArtLibrary.HasApproved("bg_meadow"))
            {
                // 임시 잔디 바닥
                var ground = UIFactory.CreateImage("Ground", _root, null, new Color(0.45f, 0.75f, 0.35f));
                var grt = ground.rectTransform;
                grt.anchorMin = Vector2.zero; grt.anchorMax = new Vector2(1, 0.35f);
                grt.offsetMin = grt.offsetMax = Vector2.zero;
                ground.raycastTarget = false;
            }

            // 상단 정보
            _manaText = UIFactory.CreateText("Mana", _root, "", 72);
            UIFactory.Place(_manaText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(1000, 100));
            var manaIcon = UIFactory.CreateImage("ManaIcon", _root, ArtLibrary.Get("icon_mana", new Color(0.5f, 0.85f, 1f)));
            UIFactory.Place(manaIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-420, -130), new Vector2(90, 90));
            manaIcon.raycastTarget = false;

            _rateText = UIFactory.CreateText("Rate", _root, "", 40);
            UIFactory.Place(_rateText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(1000, 60));

            _stageText = UIFactory.CreateText("Stage", _root, "", 40);
            UIFactory.Place(_stageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(1000, 60));

            var barBg = UIFactory.CreateImage("ProgressBg", _root, null, new Color(0, 0, 0, 0.35f));
            UIFactory.Place(barBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -350), new Vector2(800, 30));
            barBg.raycastTarget = false;
            var fill = UIFactory.CreateImage("ProgressFill", barBg.rectTransform, null, new Color(0.6f, 1f, 0.5f));
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = Vector2.zero; _progressFill.anchorMax = new Vector2(0, 1);
            _progressFill.offsetMin = _progressFill.offsetMax = Vector2.zero;
            fill.raycastTarget = false;

            // 나무 (화면 중앙 아래쪽에 뿌리를 둠)
            var treeBtn = UIFactory.CreateButton("Tree", _root, ArtLibrary.Get("tree_stage_0", StageColor(0)), Color.white);
            treeBtn.transition = Selectable.Transition.None;
            _treeImage = (Image)treeBtn.targetGraphic;
            _treeRect = (RectTransform)treeBtn.transform;
            UIFactory.Place(_treeRect, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(400, 400));
            treeBtn.onClick.AddListener(OnTreeTapped);

            _fruitLayer = UIFactory.CreateRect("Fruits", _root);
            UIFactory.Stretch(_fruitLayer);

            // 하단 업그레이드 버튼 3개
            var types = new[] { UpgradeType.Watering, UpgradeType.Fairy, UpgradeType.Sunlight };
            for (int i = 0; i < types.Length; i++)
                BuildUpgradeButton(types[i], new Vector2((i - 1) * 340, 170));

            _toastText = UIFactory.CreateText("Toast", _root, "", 52, TextAnchor.MiddleCenter, Gold);
            UIFactory.Place(_toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 520), new Vector2(1000, 160));
            _toastText.gameObject.SetActive(false);
        }

        void BuildUpgradeButton(UpgradeType type, Vector2 pos)
        {
            var btn = UIFactory.CreateButton("Upgrade_" + type, _root, null, new Color(0.15f, 0.2f, 0.35f, 0.85f));
            var rt = (RectTransform)btn.transform;
            UIFactory.Place(rt, new Vector2(0.5f, 0f), pos, new Vector2(310, 260));
            var colors = btn.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = colors;

            var icon = UIFactory.CreateImage("Icon", rt, ArtLibrary.Get(UpgradeIcon(type), new Color(1f, 0.9f, 0.5f)));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -65), new Vector2(100, 100));
            icon.raycastTarget = false;

            var title = UIFactory.CreateText("Title", rt, "", 36);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(300, 50));
            var desc = UIFactory.CreateText("Desc", rt, UpgradeDesc(type), 28, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f));
            UIFactory.Place(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(300, 40));
            var cost = UIFactory.CreateText("Cost", rt, "", 36, TextAnchor.MiddleCenter, Gold);
            UIFactory.Place(cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 35), new Vector2(300, 50));

            btn.onClick.AddListener(() => TryBuy(type));
            _upgradeUI[type] = (btn, title, cost);
        }

        // ───────────────────────── 연출 ─────────────────────────

        void SpawnFloatingText(string msg, Vector2 pos, Color color)
        {
            var t = UIFactory.CreateText("Float", _root, msg, 56, TextAnchor.MiddleCenter, color);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(300, 80));
            StartCoroutine(FloatUp(t));
        }

        IEnumerator FloatUp(Text t)
        {
            var rt = t.rectTransform;
            Vector2 start = rt.anchoredPosition;
            Color c = t.color;
            for (float e = 0; e < 0.9f; e += Time.deltaTime)
            {
                float k = e / 0.9f;
                rt.anchoredPosition = start + new Vector2(0, 160 * k);
                t.color = new Color(c.r, c.g, c.b, 1 - k);
                yield return null;
            }
            Destroy(t.gameObject);
        }

        IEnumerator Pulse(RectTransform rt, float peak)
        {
            const float dur = 0.15f;
            for (float e = 0; e < dur * 2 && rt != null; e += Time.deltaTime)
            {
                float k = e < dur ? e / dur : 2 - e / dur;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, peak, k);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        Coroutine _toastRoutine;
        void ShowToast(string msg)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(msg));
        }

        IEnumerator ToastRoutine(string msg)
        {
            _toastText.text = msg;
            _toastText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2.5f);
            _toastText.gameObject.SetActive(false);
        }
    }
}
