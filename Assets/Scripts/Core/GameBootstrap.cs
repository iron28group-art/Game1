using UnityEngine;

namespace MagicTree
{
    /// <summary>
    /// 씬 설정 없이 Play 만 누르면 게임이 뜨도록 자동 생성한다.
    /// (씬에 MagicTreeGame 이 이미 있으면 아무 것도 하지 않음)
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<MagicTreeGame>() != null) return;

            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.85f, 0.95f);
            }

            Application.targetFrameRate = 60;
            var go = new GameObject("MagicTreeGame");
            go.AddComponent<MagicTreeGame>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
