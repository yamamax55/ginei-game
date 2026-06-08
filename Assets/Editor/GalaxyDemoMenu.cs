using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（C-1/C-3）デモのセットアップ。`Ginei/戦略マップ デモを開く` で
    /// Strategy シーンを作成し、Battle と共に Build Settings へ登録する。
    /// 再生すると銀河マップが動き、回廊で敵対艦隊が接触すると Battle シーンで実会戦になり、
    /// 決着後に Strategy へ戻って結果が反映される（戦略↔戦術の往復）。
    /// </summary>
    public static class GalaxyDemoMenu
    {
        private const string StrategyScenePath = "Assets/Scenes/Strategy.unity";
        private const string BattleScenePath = "Assets/Scenes/Battle.unity";

        [MenuItem("Ginei/戦略マップ デモを開く")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Strategy シーンを作成（Main Camera 付き）し、GalaxyView を1つ置く
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("GalaxyView").AddComponent<GalaxyView>();

            if (!Directory.Exists("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, StrategyScenePath);

            RegisterBuildScenes();

            Debug.Log("戦略マップ デモ：Strategy シーンを作成し Build Settings に登録しました。再生で銀河マップが動きます。\n" +
                      "・左クリック=選択／右クリック=星系へ進軍 or 回廊上で停止保持。\n" +
                      "・回廊で敵対艦隊が接触すると Battle シーンで『実会戦』になり、決着後に Strategy へ戻って結果が反映されます。\n" +
                      "※Battle シーンの BattleSetup に fleetPrefab が割り当たっていること、Build Settings に Strategy/Battle が入っていることを確認してください。");
        }

        private static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            AddIfMissing(scenes, StrategyScenePath);
            AddIfMissing(scenes, BattleScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddIfMissing(List<EditorBuildSettingsScene> scenes, string path)
        {
            if (!File.Exists(path)) return;
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}
