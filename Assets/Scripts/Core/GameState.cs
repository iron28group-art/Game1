using System;
using UnityEngine;

namespace MagicTree
{
    /// <summary>저장되는 게임 데이터. PlayerPrefs 에 JSON 으로 보관한다.</summary>
    [Serializable]
    public class GameState
    {
        const string SaveKey = "MagicTree.Save.v1";

        public double mana;            // 현재 보유 마나 이슬
        public double totalMana;       // 누적 획득량 (나무 성장 기준)
        public int wateringLevel;      // 물뿌리개: 터치당 마나 증가
        public int fairyLevel;         // 요정 친구: 초당 자동 마나
        public int sunlightLevel;      // 햇살: 열매 생성 주기 단축
        public long lastSaveUnix;      // 오프라인 보상 계산용

        public static GameState Load()
        {
            var json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(json)) return new GameState();
            try { return JsonUtility.FromJson<GameState>(json) ?? new GameState(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[MagicTree] 저장 데이터 읽기 실패, 새로 시작합니다: {e.Message}");
                return new GameState();
            }
        }

        public void Save()
        {
            lastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Wipe()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
