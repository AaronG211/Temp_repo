using UnityEngine;

namespace DoodleArena
{
    public static class DoodleArenaBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (Object.FindAnyObjectByType<DoodleArenaGame>() != null) return;

            var host = new GameObject("Doodle Arena");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<DoodleArenaGame>();
        }
    }
}
