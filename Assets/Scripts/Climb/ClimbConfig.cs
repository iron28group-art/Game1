namespace MagicTree.Climb
{
    /// <summary>
    /// 요술나무 오르기 — 조작감·난이도 수치를 한 곳에 모았다.
    /// 단위: 1 = 1m (월드 단위). 화면 폭은 항상 ViewWidth 만큼 보인다.
    /// </summary>
    public static class ClimbConfig
    {
        // 화면
        public const float ViewWidth = 10.8f;        // 세로 화면 폭 (1080px 기준 100px = 1m)
        public const float HalfPlayWidth = 4.9f;     // 토리가 갈 수 있는 좌우 범위 (±)
        public const float CameraLead = 2.0f;        // 토리보다 위쪽을 더 보여 줌

        // 토리 몸 (충돌 상자)
        public const float BodyWidth = 0.7f;
        public const float BodyHeight = 1.25f;
        public const float SpriteHeight = 1.7f;      // 보이는 그림 높이 (모자 포함)

        // 움직임
        public const float RunSpeed = 5.2f;
        public const float AirControl = 0.85f;       // 공중 좌우 조작 비율
        public const float Gravity = -30f;
        public const float JumpSpeed = 13f;          // 최고 높이 = JumpSpeed² / (2·|Gravity|) ≈ 2.8m
        public const float MaxFallSpeed = -18f;
        public const float ClimbSpeed = 3.6f;
        public const float BounceSpeed = 18.5f;      // 통통 버섯 (≈ 5.7m)
        public const float CoyoteTime = 0.1f;        // 발판에서 떨어진 직후에도 점프 허용
        public const float JumpBuffer = 0.12f;       // 착지 직전에 누른 점프도 인정

        // 맞았을 때 (목숨 없음: 밀려나서 아래로 떨어질 수 있을 뿐)
        public const float HitStun = 0.55f;
        public const float HitPushX = 4.5f;
        public const float HitPushY = 5f;
        public const float HitInvincible = 1.2f;

        // 지형 생성
        public const float RowGapMin = 1.9f;         // 발판 줄 사이 높이 (최고 점프 2.8m 보다 충분히 낮게)
        public const float RowGapMax = 2.45f;
        public const float RowGapHardBonus = 0.15f;  // 높이 올라갈수록 조금 더 벌어짐 (최대)
        public const float PlatformWidthMin = 1.9f;
        public const float PlatformWidthMax = 3.4f;
        public const float MaxHorizontalStep = 3.2f; // 바로 아래 줄 발판과 가장자리 사이 최대 가로 간격
        public const float PlatformThickness = 0.35f;
        public const float VineGapHeight = 4.4f;     // 덩굴/버섯 구간의 높이
        public const float CheckpointEvery = 50f;    // m
        public const float GenerateAhead = 30f;      // 카메라 위로 미리 만들어 둘 높이
        public const float CleanupBelow = 25f;       // 체크포인트보다 이만큼 아래는 지움

        // 방해물
        public const float CaterpillarSpeed = 1.0f;
        public const float PineconeSpeed = 2.4f;
        public const float WindForce = 7f;           // 꽃가루 바람이 미는 세기 (m/s²)

        // 구간 이름 (높이 m 이상)
        public static readonly float[] AreaHeights = { 0f, 100f, 250f };
        public static readonly string[] AreaNames = { "뿌리 마을", "꽃잎 층", "별빛 가지" };

        public static int AreaFor(float height)
        {
            int a = 0;
            for (int i = 0; i < AreaHeights.Length; i++)
                if (height >= AreaHeights[i]) a = i;
            return a;
        }

        /// <summary>0(시작) ~ 1(어려움). 300m 에서 최대.</summary>
        public static float Difficulty(float height) => UnityEngine.Mathf.Clamp01(height / 300f);
    }
}
