using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    public static class GameResultStrokeUpgrade
    {
        [MenuItem("Hapio/Upgrade Result UI Strokes")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            GameStrokeDesignUpgrade.ImportLibrary();
            Scene previous=SceneManager.GetActiveScene();
            foreach (string path in new[] {GameScenePaths.GameOver,GameScenePaths.StageResult})
            {
                Scene scene=SceneManager.GetSceneByPath(path);
                bool owned=!scene.IsValid() || !scene.isLoaded;
                if (owned) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    if (scene.isDirty) throw new InvalidOperationException("Save pending scene edits first: "+path);
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var view in root.GetComponentsInChildren<GameOverCharacterView>(true)) GameResultStrokes.Apply(view);
                        foreach (var view in root.GetComponentsInChildren<GameTerritoryResultView>(true)) GameResultStrokes.Apply(view);
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (owned) EditorSceneManager.CloseScene(scene,true); }
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
