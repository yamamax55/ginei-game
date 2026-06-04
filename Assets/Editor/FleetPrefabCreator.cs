using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// シーン上の艦隊 GameObject から艦隊プレハブを生成するエディタ拡張。
    /// メニュー Ginei/Create Fleet Prefab から実行する。
    /// 生成されるプレハブは FleetAI を「無効状態」で含む（敵だけ BattleSetup で有効化する想定）。
    /// </summary>
    public static class FleetPrefabCreator
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string PrefabPath = "Assets/Prefabs/FleetUnit.prefab";
        private const string DefaultSourceName = "Fleet"; // 既定のソース名（プレイヤー側艦隊）

        // 実行時に生成される子（プレハブに焼き込まれると二重表示等の原因になる）
        private static readonly string[] RuntimeChildNames =
            { "StrengthDisplay", "MoraleLabel", "WeaponArcLine", "BeamLine", "Explosion", "DamagePopup" };

        [MenuItem("Ginei/Create Fleet Prefab")]
        public static void CreateFleetPrefab()
        {
            // Play 中は実行不可（実行時生成物が焼き込まれた汚いプレハブができるため）
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Fleet Prefab",
                    "Play モード中は実行できません。▶ を停止（EDITモード）してから実行してください。", "OK");
                return;
            }

            // 1. ソース GameObject を決定（Hierarchy で選択中を優先、なければ "Fleet" を検索）
            GameObject source = Selection.activeGameObject;
            if (source == null)
            {
                source = GameObject.Find(DefaultSourceName);
            }
            if (source == null)
            {
                EditorUtility.DisplayDialog("Fleet Prefab",
                    $"艦隊 GameObject が見つかりません。\nHierarchy で艦隊を選択するか、\"{DefaultSourceName}\" という名前の艦隊を用意してください。",
                    "OK");
                return;
            }

            // 2. 必須コンポーネントの確認（艦隊以外を誤ってプレハブ化しないため）
            if (source.GetComponent<FleetStrength>() == null)
            {
                EditorUtility.DisplayDialog("Fleet Prefab",
                    $"選択された \"{source.name}\" は FleetStrength を持っていません。\n艦隊 GameObject を選択してください。",
                    "OK");
                return;
            }

            // 3. 出力先フォルダを用意
            if (!Directory.Exists(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
                AssetDatabase.Refresh();
            }

            // 4. 既存プレハブがあれば上書き確認
            if (File.Exists(PrefabPath))
            {
                bool overwrite = EditorUtility.DisplayDialog("Fleet Prefab",
                    $"既に {PrefabPath} が存在します。上書きしますか？", "上書き", "キャンセル");
                if (!overwrite) return;
            }

            // 5. ソースを複製してから FleetAI を無効化（基準シーンを汚さないため複製を加工）
            GameObject temp = Object.Instantiate(source);
            temp.name = "FleetUnit";

            FleetAI ai = temp.GetComponent<FleetAI>();
            if (ai == null) ai = temp.AddComponent<FleetAI>(); // RequireComponent 依存は艦隊に揃っている
            ai.enabled = false;

            // 5.5 実行時生成物を除去（焼き込み由来の二重表示・LineRenderer衝突を防ぐ）
            StripRuntimeArtifacts(temp);

            // 6. プレハブ保存
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath, out bool success);

            // 7. 一時オブジェクトを破棄
            Object.DestroyImmediate(temp);

            if (success)
            {
                EditorUtility.DisplayDialog("Fleet Prefab",
                    $"プレハブを作成しました:\n{PrefabPath}\n\n（FleetAI は無効状態で含まれています）",
                    "OK");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                EditorUtility.DisplayDialog("Fleet Prefab", "プレハブの作成に失敗しました。", "OK");
            }
        }

        /// <summary>
        /// 実行時に生成される子オブジェクトと、実行時に追加される LineRenderer を除去します。
        /// （Play中に作られた艦隊から作成しても、きれいなプレハブになるように）
        /// </summary>
        private static void StripRuntimeArtifacts(GameObject root)
        {
            // 実行時生成の子を削除
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child == root.transform) continue;
                foreach (var n in RuntimeChildNames)
                {
                    if (child.name == n)
                    {
                        Object.DestroyImmediate(child.gameObject);
                        break;
                    }
                }
            }

            // 実行時に追加される LineRenderer（ビーム用）を除去：起動時に作り直される
            foreach (var lr in root.GetComponentsInChildren<LineRenderer>(true))
            {
                Object.DestroyImmediate(lr);
            }
        }
    }
}
