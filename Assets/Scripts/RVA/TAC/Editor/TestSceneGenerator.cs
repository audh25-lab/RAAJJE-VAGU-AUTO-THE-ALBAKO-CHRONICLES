#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RVA.TAC.Editor {
    public static class TestSceneGenerator {
        [MenuItem("RVA:TAC/Generate Test Scene")]
        public static void GenerateTestScene() {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RVA_TAC_TestScene";
            
            // Add bootstrapper
            var bootstrapper = new GameObject("GameBootstrapper");
            bootstrapper.AddComponent<GameBootstrapper>();
            
            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/RVA_TAC_TestScene.unity");
            AssetDatabase.Refresh();
            
            Debug.Log("[RVA:TAC] Test scene generated in repository");
        }
    }
}
#endif
