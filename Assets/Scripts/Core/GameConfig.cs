using System;

namespace MagicTree
{
    /// <summary>밸런스 수치. 기획 조정은 여기서.</summary>
    public static class GameConfig
    {
        // 나무 성장 단계 (누적 마나 기준)
        public static readonly string[] StageNames = { "씨앗", "새싹", "어린 나무", "큰 나무", "요술나무" };
        public static readonly double[] StageThresholds = { 0, 50, 300, 1500, 8000 };

        // 열매는 이 단계부터 열린다
        public const int FruitMinStage = 2;
        public const int MaxFruits = 5;
        public const float BaseFruitInterval = 8f;   // 초
        public const float FruitTapMultiplier = 10f; // 열매 = 터치 10회분

        // 오프라인 보상 최대 시간 (초)
        public const double MaxOfflineSeconds = 60 * 60 * 8;

        public static double TapPower(GameState s) => 1 + s.wateringLevel;
        public static double ManaPerSecond(GameState s) => s.fairyLevel * 0.5;
        public static float FruitInterval(GameState s) => BaseFruitInterval / (1f + s.sunlightLevel * 0.25f);

        public static int StageFor(double totalMana)
        {
            int stage = 0;
            for (int i = 0; i < StageThresholds.Length; i++)
                if (totalMana >= StageThresholds[i]) stage = i;
            return stage;
        }

        public static double Cost(UpgradeType type, int level)
        {
            switch (type)
            {
                case UpgradeType.Watering: return Math.Floor(10 * Math.Pow(1.5, level));
                case UpgradeType.Fairy:    return Math.Floor(25 * Math.Pow(1.6, level));
                case UpgradeType.Sunlight: return Math.Floor(100 * Math.Pow(2.0, level));
                default: return double.MaxValue;
            }
        }

        public static string Format(double v)
        {
            if (v < 1000) return Math.Floor(v).ToString("0");
            string[] units = { "K", "M", "B", "T" };
            int u = -1;
            while (v >= 1000 && u < units.Length - 1) { v /= 1000; u++; }
            return v.ToString("0.0") + units[u];
        }
    }

    public enum UpgradeType { Watering, Fairy, Sunlight }
}
