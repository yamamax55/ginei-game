using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（C-1 #34）最小ビジュアライズのデモを開くエディタメニュー。
    /// 新規シーンに GalaxyView を1つ置くだけ。再生(Play)で銀河マップが表示される。
    /// </summary>
    public static class GalaxyDemoMenu
    {
        [MenuItem("Ginei/戦略マップ デモを開く")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("GalaxyView");
            go.AddComponent<GalaxyView>();
            Selection.activeGameObject = go;

            Debug.Log("戦略マップ デモ：再生(Play)で銀河マップが表示されます。" +
                      "左クリックで艦隊（明るい点）を選択 → 星系（大きい点）をクリックでワープ指示。" +
                      "時間経過で移動・占領（色変化）・回廊での会戦遭遇が起こります。");
        }
    }
}
