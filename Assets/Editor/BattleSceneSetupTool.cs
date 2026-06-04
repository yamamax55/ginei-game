using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// Battle シーンを「シナリオ生成方式」に切り替えるエディタ拡張。
    /// メニュー Ginei/Setup Battle Scene から実行する。
    /// - 手置きの艦隊 "Fleet" / "EnemyFleet" を削除
    /// - BattleSetup を持つ GameObject を生成し、FleetUnit プレハブを割り当て
    /// 実行後はシーンを保存（Ctrl+S）すること。
    /// 結果は Console に出力する（モーダルダイアログは使わない：背面に隠れて固まって見えるのを防ぐ）。
    /// </summary>
    public static class BattleSceneSetupTool
    {
        private const string PrefabPath = "Assets/Prefabs/FleetUnit.prefab";

        [MenuItem("Ginei/Setup Battle Scene")]
        public static void SetupBattleScene()
        {
            // Play 中は実行不可（戦闘中に艦隊を削除して誤った勝敗判定を招くため）。
            // EDIT モードで実行 → Ctrl+S で保存 → ▶ Play の順で使うこと。
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[BattleSetup] Play モード中は実行できません。▶ を停止（EDITモード）してから実行し、Ctrl+S で保存してください。");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[BattleSetup] 有効なシーンが開かれていません。");
                return;
            }

            // 1. FleetUnit プレハブを取得
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BattleSetup] プレハブが見つかりません: {PrefabPath} / 先に Ginei/Create Fleet Prefab を実行してください。");
                return;
            }

            // 2. 既存の BattleSetup（重複生成防止）
            BattleSetup existing = Object.FindAnyObjectByType<BattleSetup>();

            // 3. 手置き艦隊を削除（FleetStrength を持つルートのみ。配下艦は親ごと消える）
            int removed = 0;
            foreach (var fs in Object.FindObjectsByType<FleetStrength>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fs != null && fs.transform.parent == null)
                {
                    Undo.DestroyObjectImmediate(fs.gameObject);
                    removed++;
                }
            }

            // 4. BattleSetup を用意（無ければ新規作成）
            BattleSetup setup = existing;
            if (setup == null)
            {
                GameObject go = new GameObject("BattleSetup");
                Undo.RegisterCreatedObjectUndo(go, "Create BattleSetup");
                setup = go.AddComponent<BattleSetup>();
            }
            setup.fleetPrefab = prefab;
            EditorUtility.SetDirty(setup);

            // 5. シーンを変更済みにマーク（保存はユーザーが Ctrl+S）
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeObject = setup.gameObject;
            EditorGUIUtility.PingObject(setup.gameObject);

            Debug.Log($"[BattleSetup] 完了：手置き艦隊を {removed} 隻削除 / BattleSetup={(existing == null ? "新規作成" : "既存を更新")} / FleetPrefab 割り当て済み。⚠ Ctrl+S でシーンを保存してください。");
        }
    }
}
