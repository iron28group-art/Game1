using System;
using System.Collections.Generic;
using UnityEngine;
using C = MagicTree.Climb.ClimbConfig;

namespace MagicTree.Climb
{
    public class Platform
    {
        public float X, Y, Width;         // X = 가운데, Y = 밟는 면 높이
        public bool IsCheckpoint;
        public bool IsGoal;               // 별 열매 쉼터
        public float Left => X - Width * 0.5f;
        public float Right => X + Width * 0.5f;
    }

    public class Vine { public float X, Bottom, Top; }
    public class Mushroom { public Platform On; public float X; }
    public class Dew { public float X, Y; public bool Collected; }
    public class Wind { public float Bottom, Top; public int Dir; }

    public enum HazardKind { Caterpillar, Pinecone }

    public class Hazard
    {
        public HazardKind Kind;
        public Platform On;
        public float X;
        public int Dir = 1;
        public float Spin;                // 솔방울 굴러가는 각도 (그림용)
        public float Y => On.Y;
        public float HalfWidth => Kind == HazardKind.Caterpillar ? 0.45f : 0.28f;
        public float Height => Kind == HazardKind.Caterpillar ? 0.45f : 0.55f;
        public float Speed => Kind == HazardKind.Caterpillar ? C.CaterpillarSpeed : C.PineconeSpeed;
    }

    public struct ClimbInput
    {
        public float Horizontal;   // -1 ~ 1
        public float Vertical;     // -1 ~ 1 (덩굴)
        public bool JumpPressed;   // 이번 프레임에 눌림
    }

    public enum PlayerMode { Ground, Air, Climb }

    /// <summary>
    /// 요술나무 오르기의 지형 생성과 움직임 계산 (그림/입력과 분리된 순수 로직).
    /// 좌표: x 가운데 0, y 위로 증가. 토리 위치 = 발 가운데.
    /// </summary>
    public class ClimbWorld
    {
        public readonly List<Platform> Platforms = new List<Platform>();
        public readonly List<Vine> Vines = new List<Vine>();
        public readonly List<Mushroom> Mushrooms = new List<Mushroom>();
        public readonly List<Hazard> Hazards = new List<Hazard>();
        public readonly List<Dew> Dews = new List<Dew>();
        public readonly List<Wind> Winds = new List<Wind>();

        public Vector2 Pos;
        public Vector2 Vel;
        public PlayerMode Mode = PlayerMode.Ground;
        public int Facing = 1;
        public float MaxHeight;
        public float CheckpointHeight;
        public float StunTimer, InvincibleTimer;
        public Vine ClimbingVine;

        // 이벤트 (그림·소리 연출용)
        public event Action Jumped, Landed, Bounced, Hit;
        public event Action<Dew> DewCollected;
        public event Action<float> CheckpointReached;
        public event Action GoalReached;
        public event Action<Platform> PlatformAdded;   // 새로 생긴 것들은 그림 쪽에서 목록을 다시 훑는다

        readonly System.Random _rng;
        float _nextRowY;
        float _nextCheckpointY;
        Platform _last;                  // 바로 아래 줄의 '길' 발판
        int _rowsSinceSpecial;
        float _lastWindY = -999f;
        float _coyote, _jumpBuffer;
        Platform _standing;

        public ClimbWorld(int seed)
        {
            _rng = new System.Random(seed);
            // 맨 아래 땅 (체크포인트 0)
            _last = AddPlatform(0f, 0f, C.HalfPlayWidth * 2f + 1f, checkpoint: true);
            _nextCheckpointY = C.CheckpointEvery;
            _nextRowY = Gap(0f);
            Pos = new Vector2(0f, 0f);
            _standing = _last;
        }

        float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        bool Chance(float p) => _rng.NextDouble() < p;

        Platform AddPlatform(float x, float y, float w, bool checkpoint = false)
        {
            var p = new Platform { X = x, Y = y, Width = w, IsCheckpoint = checkpoint };
            Platforms.Add(p);
            PlatformAdded?.Invoke(p);
            return p;
        }

        float Gap(float y) => R(C.RowGapMin, C.RowGapMax) + C.RowGapHardBonus * C.Difficulty(y);

        /// <summary>
        /// 높이 g 위의 발판으로 뛰어오를 때 가로로 갈 수 있는 거리(가장자리 기준).
        /// 올라가서 다시 g 높이로 내려올 때까지의 공중 시간 × 공중 이동 속도 − 여유.
        /// (가장자리 끝에서 정확히 뛰지 않아도 닿도록 몸 폭만큼의 덤은 넣지 않는다.
        ///  tools/sim/climb_check.py 로 300개 seed × 400m 모두 닿는 것을 확인함)
        /// </summary>
        public static float HorizontalReach(float g, float upSpeed)
        {
            float a = -C.Gravity;                      // 30
            float disc = upSpeed * upSpeed - 2f * a * g;
            if (disc <= 0f) return 0f;
            float t = (upSpeed + Mathf.Sqrt(disc)) / a;
            return t * C.RunSpeed * C.AirControl - 0.25f;
        }

        public void GenerateUpTo(float yTarget)
        {
            while (_nextRowY < yTarget)
            {
                float y = _nextRowY;
                float d = C.Difficulty(y);

                if (y >= _nextCheckpointY)
                {
                    // 반딧불 쉼터: 화면 폭 전체 가지 → 떨어져도 여기서 멈춘다
                    _last = AddPlatform(0f, _nextCheckpointY, C.HalfPlayWidth * 2f + 1f, checkpoint: true);
                    _last.IsGoal = Mathf.Abs(_nextCheckpointY - C.GoalHeight) < 0.01f;
                    _nextRowY = _nextCheckpointY + Gap(_nextCheckpointY);
                    _nextCheckpointY += C.CheckpointEvery;
                    _rowsSinceSpecial = 0;
                    continue;
                }

                bool special = y > 12f && _rowsSinceSpecial >= 3 && Chance(0.14f + 0.08f * d)
                               && y + C.VineGapHeight < _nextCheckpointY - 1f;
                if (special) { AddSpecialRow(); continue; }

                float g = y - _last.Y;
                float w = Mathf.Lerp(C.PlatformWidthMax, C.PlatformWidthMin + 0.2f, d) * R(0.8f, 1.1f);
                w = Mathf.Clamp(w, C.PlatformWidthMin, C.PlatformWidthMax);
                float x = PlaceNear(_last, w, HorizontalReach(g, C.JumpSpeed));
                var p = AddPlatform(x, y, w);

                // 옆길(보너스) 발판
                if (Chance(0.3f))
                {
                    float side = x > 0 ? -1f : 1f;
                    float bw = R(1.6f, 2.2f);
                    float bx = Mathf.Clamp(x + side * R(3.2f, 4.5f), -C.HalfPlayWidth + bw * 0.5f, C.HalfPlayWidth - bw * 0.5f);
                    if (Mathf.Abs(bx - x) > (bw + w) * 0.5f + 0.4f)
                    {
                        var bonus = AddPlatform(bx, y + R(-0.3f, 0.3f), bw);
                        Dews.Add(new Dew { X = bonus.X, Y = bonus.Y + 0.9f });
                    }
                }

                if (Chance(0.45f)) Dews.Add(new Dew { X = p.X + R(-0.4f, 0.4f) * w, Y = p.Y + 0.9f });

                if (y > 8f && w >= 2.2f)
                {
                    if (Chance(0.1f + 0.25f * d))
                        Hazards.Add(new Hazard { Kind = HazardKind.Caterpillar, On = p, X = p.X, Dir = Chance(0.5f) ? 1 : -1 });
                    else if (Chance(0.06f + 0.2f * d))
                        Hazards.Add(new Hazard { Kind = HazardKind.Pinecone, On = p, X = p.Left + 0.4f, Dir = 1 });
                }

                if (y > C.AreaHeights[1] && y - _lastWindY > 15f && Chance(0.08f + 0.08f * d))
                {
                    Winds.Add(new Wind { Bottom = y - 0.5f, Top = y + 3.5f, Dir = Chance(0.5f) ? 1 : -1 });
                    _lastWindY = y;
                }

                _last = p;
                _rowsSinceSpecial++;
                _nextRowY = y + Gap(y);
            }
        }

        /// <summary>높은 칸: 덩굴로 오르거나 통통 버섯으로 튀어 오른다.</summary>
        void AddSpecialRow()
        {
            float y = _last.Y + C.VineGapHeight;
            float w = R(2.2f, 3.0f);
            if (Chance(0.5f))
            {
                // 덩굴: 아래 발판 위의 한 점에서 위 발판까지. 두 발판 모두 덩굴 x 를 덮도록.
                float vx = Mathf.Clamp(_last.X + R(-0.3f, 0.3f) * _last.Width, _last.Left + 0.5f, _last.Right - 0.5f);
                float ux = Mathf.Clamp(vx + R(-0.6f, 0.6f) * w, -C.HalfPlayWidth + w * 0.5f, C.HalfPlayWidth - w * 0.5f);
                ux = Mathf.Clamp(ux, vx - w * 0.5f + 0.5f, vx + w * 0.5f - 0.5f);
                var up = AddPlatform(ux, y, w);
                Vines.Add(new Vine { X = vx, Bottom = _last.Y, Top = up.Y });
                _last = up;
            }
            else
            {
                float mx = Mathf.Clamp(_last.X + R(-0.3f, 0.3f) * _last.Width, _last.Left + 0.5f, _last.Right - 0.5f);
                Mushrooms.Add(new Mushroom { On = _last, X = mx });
                float reach = HorizontalReach(C.VineGapHeight, C.BounceSpeed);
                var dummy = new Platform { X = mx, Y = _last.Y, Width = 0.6f };
                var up = AddPlatform(PlaceNear(dummy, w, reach), y, w);
                Dews.Add(new Dew { X = up.X, Y = up.Y - 1.6f }); // 튀어 오르는 길에 이슬
                _last = up;
            }
            _rowsSinceSpecial = 0;
            _nextRowY = y + Gap(y);
        }

        /// <summary>아래 발판 b 에서 가장자리 간격이 reach 이하가 되도록 새 발판 가운데 x 를 고른다.</summary>
        float PlaceNear(Platform b, float w, float reach)
        {
            float maxDx = Mathf.Max(0.8f, reach + (w + b.Width) * 0.5f);
            float minDx = Mathf.Min(1.0f, maxDx);
            float dir = Chance(0.5f) ? 1f : -1f;
            float lim = C.HalfPlayWidth - w * 0.5f;
            if (b.X + dir * minDx > lim || b.X + dir * minDx < -lim) dir = -dir; // 벽 쪽이면 반대로
            float x = b.X + dir * R(minDx, maxDx);
            return Mathf.Clamp(x, -lim, lim);
        }

        /// <summary>체크포인트보다 한참 아래 있는 것들은 지운다 (다시 떨어질 일이 없으므로).</summary>
        public void Cleanup(Action<object> removed)
        {
            float limit = CheckpointHeight - C.CleanupBelow;
            void Sweep<T>(List<T> list, Func<T, float> top)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                    if (top(list[i]) < limit) { removed?.Invoke(list[i]); list.RemoveAt(i); }
            }
            Sweep(Platforms, p => p.Y);
            Sweep(Vines, v => v.Top);
            Sweep(Mushrooms, m => m.On.Y);
            Sweep(Hazards, h => h.Y);
            Sweep(Dews, d => d.Y);
            Sweep(Winds, w => w.Top);
        }

        // ───────────────────────── 움직임 ─────────────────────────

        public void Step(float dt, ClimbInput input)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            StunTimer = Mathf.Max(0f, StunTimer - dt);
            InvincibleTimer = Mathf.Max(0f, InvincibleTimer - dt);
            _coyote = Mathf.Max(0f, _coyote - dt);
            _jumpBuffer = input.JumpPressed ? C.JumpBuffer : Mathf.Max(0f, _jumpBuffer - dt);

            bool control = StunTimer <= 0f;
            float h = control ? Mathf.Clamp(input.Horizontal, -1f, 1f) : 0f;
            float v = control ? Mathf.Clamp(input.Vertical, -1f, 1f) : 0f;
            if (Mathf.Abs(h) > 0.1f) Facing = h > 0 ? 1 : -1;

            if (Mode != PlayerMode.Climb && control && Mathf.Abs(v) > 0.5f) TryGrabVine(v);

            if (Mode == PlayerMode.Climb) StepClimb(dt, v, control);
            else StepMove(dt, h, control);

            UpdateHazards(dt);
            CheckHazardHits();
            CollectDews();

            if (Pos.y > MaxHeight) MaxHeight = Pos.y;
        }

        void StepMove(float dt, float h, bool control)
        {
            float wind = 0f;
            foreach (var w in Winds)
                if (Pos.y >= w.Bottom && Pos.y <= w.Top) wind += w.Dir * 1.8f;

            if (control)
            {
                float speed = C.RunSpeed * (Mode == PlayerMode.Air ? C.AirControl : 1f);
                Vel.x = h * speed + wind;
            }
            else Vel.x = Mathf.MoveTowards(Vel.x, wind, 6f * dt);

            // 점프 (코요테 타임 + 입력 버퍼)
            if (control && _jumpBuffer > 0f && (Mode == PlayerMode.Ground || _coyote > 0f))
            {
                Vel.y = C.JumpSpeed;
                Mode = PlayerMode.Air;
                _standing = null;
                _coyote = 0f; _jumpBuffer = 0f;
                Jumped?.Invoke();
            }

            if (Mode == PlayerMode.Ground)
            {
                Pos.x += Vel.x * dt;
                ClampX();
                if (_standing == null || Pos.x + C.BodyWidth * 0.5f < _standing.Left || Pos.x - C.BodyWidth * 0.5f > _standing.Right)
                {
                    Mode = PlayerMode.Air;      // 발판 끝에서 걸어 나감
                    _standing = null;
                    _coyote = C.CoyoteTime;
                    Vel.y = 0f;
                }
                else Pos.y = _standing.Y;
                return;
            }

            // 공중
            Vel.y = Mathf.Max(Vel.y + C.Gravity * dt, C.MaxFallSpeed);
            float prevY = Pos.y;
            Pos += Vel * dt;
            ClampX();

            if (Vel.y <= 0f)
            {
                // 한쪽 방향 발판: 위에서 내려올 때만 선다 (아래에서는 통과)
                Platform best = null;
                foreach (var p in Platforms)
                {
                    if (prevY >= p.Y - 0.001f && Pos.y <= p.Y &&
                        Pos.x + C.BodyWidth * 0.5f > p.Left && Pos.x - C.BodyWidth * 0.5f < p.Right &&
                        (best == null || p.Y > best.Y))
                        best = p;
                }
                if (best != null) Land(best);
            }
        }

        void Land(Platform p)
        {
            Pos.y = p.Y;
            foreach (var m in Mushrooms)
            {
                if (m.On == p && Mathf.Abs(Pos.x - m.X) < 0.65f)
                {
                    Vel.y = C.BounceSpeed;
                    Mode = PlayerMode.Air;
                    _standing = null;
                    Bounced?.Invoke();
                    return;
                }
            }
            Vel.y = 0f;
            Mode = PlayerMode.Ground;
            _standing = p;
            Landed?.Invoke();
            if (p.IsCheckpoint && p.Y > CheckpointHeight + 0.01f)
            {
                CheckpointHeight = p.Y;
                CheckpointReached?.Invoke(p.Y);
                if (p.IsGoal) GoalReached?.Invoke();
            }
        }

        void TryGrabVine(float v)
        {
            foreach (var vine in Vines)
            {
                if (Mathf.Abs(Pos.x - vine.X) > 0.55f) continue;
                bool canUp = v > 0 && Pos.y >= vine.Bottom - 0.2f && Pos.y < vine.Top - 0.1f;
                bool canDown = v < 0 && Pos.y > vine.Bottom + 0.1f && Pos.y <= vine.Top + 0.2f;
                if (!canUp && !canDown) continue;
                Mode = PlayerMode.Climb;
                ClimbingVine = vine;
                _standing = null;
                Vel = Vector2.zero;
                Pos.x = vine.X;
                return;
            }
        }

        void StepClimb(float dt, float v, bool control)
        {
            var vine = ClimbingVine;
            if (control && _jumpBuffer > 0f)
            {
                // 덩굴에서 점프로 뛰어내림
                ClimbingVine = null;
                Mode = PlayerMode.Air;
                Vel = new Vector2(Facing * C.RunSpeed * 0.6f, C.JumpSpeed * 0.75f);
                _jumpBuffer = 0f;
                Jumped?.Invoke();
                return;
            }
            Vel = new Vector2(0f, v * C.ClimbSpeed);
            Pos.y += Vel.y * dt;
            Pos.x = vine.X;

            if (Pos.y >= vine.Top)
            {
                // 꼭대기: 위 발판에 올라선다
                ClimbingVine = null;
                Pos.y = vine.Top;
                var top = FindPlatformAt(vine.X, vine.Top);
                if (top != null) Land(top);
                else { Mode = PlayerMode.Air; Vel = Vector2.zero; }
            }
            else if (Pos.y <= vine.Bottom)
            {
                ClimbingVine = null;
                Pos.y = vine.Bottom;
                var bottom = FindPlatformAt(vine.X, vine.Bottom);
                if (bottom != null) Land(bottom);
                else { Mode = PlayerMode.Air; Vel = Vector2.zero; }
            }
        }

        Platform FindPlatformAt(float x, float y)
        {
            foreach (var p in Platforms)
                if (Mathf.Abs(p.Y - y) < 0.05f && x >= p.Left - 0.2f && x <= p.Right + 0.2f) return p;
            return null;
        }

        void ClampX()
        {
            if (Pos.x < -C.HalfPlayWidth) { Pos.x = -C.HalfPlayWidth; if (Vel.x < 0) Vel.x = 0; }
            if (Pos.x > C.HalfPlayWidth) { Pos.x = C.HalfPlayWidth; if (Vel.x > 0) Vel.x = 0; }
        }

        void UpdateHazards(float dt)
        {
            foreach (var hz in Hazards)
            {
                hz.X += hz.Dir * hz.Speed * dt;
                float lo = hz.On.Left + hz.HalfWidth, hi = hz.On.Right - hz.HalfWidth;
                if (hz.X < lo) { hz.X = lo; hz.Dir = 1; }
                if (hz.X > hi) { hz.X = hi; hz.Dir = -1; }
                if (hz.Kind == HazardKind.Pinecone) hz.Spin -= hz.Dir * hz.Speed * dt / 0.28f * Mathf.Rad2Deg;
            }
        }

        void CheckHazardHits()
        {
            if (InvincibleTimer > 0f) return;
            float pl = Pos.x - C.BodyWidth * 0.5f, pr = Pos.x + C.BodyWidth * 0.5f;
            float pb = Pos.y, pt = Pos.y + C.BodyHeight;
            foreach (var hz in Hazards)
            {
                if (pr < hz.X - hz.HalfWidth || pl > hz.X + hz.HalfWidth) continue;
                if (pt < hz.Y || pb > hz.Y + hz.Height) continue;
                int away = Pos.x >= hz.X ? 1 : -1;
                ClimbingVine = null;
                _standing = null;
                Mode = PlayerMode.Air;
                Vel = new Vector2(away * C.HitPushX, C.HitPushY);
                StunTimer = C.HitStun;
                InvincibleTimer = C.HitInvincible;
                Hit?.Invoke();
                return;
            }
        }

        void CollectDews()
        {
            Vector2 center = Pos + new Vector2(0f, C.BodyHeight * 0.5f);
            foreach (var d in Dews)
            {
                if (d.Collected) continue;
                if ((new Vector2(d.X, d.Y) - center).sqrMagnitude < 0.75f * 0.75f)
                {
                    d.Collected = true;
                    DewCollected?.Invoke(d);
                }
            }
        }

        /// <summary>체크포인트로 돌아가기 (일시정지 메뉴 등에서 사용).</summary>
        public void ResetToCheckpoint()
        {
            var p = FindPlatformAt(0f, CheckpointHeight);
            Pos = new Vector2(0f, CheckpointHeight);
            Vel = Vector2.zero;
            ClimbingVine = null;
            if (p != null) { Mode = PlayerMode.Ground; _standing = p; }
            else Mode = PlayerMode.Air;
        }
    }
}
