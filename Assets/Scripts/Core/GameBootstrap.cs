using UnityEngine;

namespace MagicTree
{
    /// <summary>
    /// 씬 설정 없이 Play 만 누르면 게임이 뜨도록 자동 생성한다.
    /// (씬에 MagicTreeGame 이 이미 있으면 아무 것도 하지 않음)
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>true 면 예전 '요술나무 키우기'(방치형 클리커), false 면 '요술나무 오르기'(등반 액션).</summary>
        public static bool UseIdleGame = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<MagicTreeGame>() != null) return;
            if (Object.FindFirstObjectByType<Climb.ClimbGame>() != null) return;

            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.85f, 0.95f);
            }

            Application.targetFrameRate = 60;
            var go = new GameObject(UseIdleGame ? "MagicTreeGame" : "ClimbGame");
            if (UseIdleGame) go.AddComponent<MagicTreeGame>();
            else go.AddComponent<Climb.ClimbGame>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
