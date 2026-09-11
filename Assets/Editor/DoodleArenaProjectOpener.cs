using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DoodleArena.Editor
{
    [InitializeOnLoad]
    internal static class DoodleArenaProjectOpener
    {
        private const string MainScene = "Assets/Scenes/SampleScene.unity";

        static DoodleArenaProjectOpener()
        {
            EditorApplication.delayCall += OpenMainSceneWhenProjectIsEmpty;
        }

        private static void OpenMainSceneWhenProjectIsEmpty()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene active = SceneManager.GetActiveScene();
            bool isEmptyTemporaryScene = string.IsNullOrEmpty(active.path) && active.rootCount <= 1;
            if (isEmptyTemporaryScene)
                EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        }
    }
}
