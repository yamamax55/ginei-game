using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 恒星/要塞モデルの実体を点検する（#恒星モデル）。純ロジック側（<see cref="StarModelRules"/> のテスト）は
    /// 「名前→番号」の対応を保証するが、<b>FBX が実在して読める状態か</b>はプロジェクトを見ないと分からない。
    /// ここで 51 体すべてについて、読み込み・メッシュ・頂点カラー・マテリアルスロットを一括で確認する。
    /// </summary>
    public static class StarModelValidator
    {
        [MenuItem("Ginei/恒星モデルを検証", false, 200)]
        public static void Validate()
        {
            var sb = new StringBuilder();
            int missing = 0, noColor = 0, notReadable = 0, badSlots = 0, ok = 0;
            long totalTris = 0;

            string[] names = MountainSystemNames.Names;
            for (int i = 0; i < names.Length; i++)
            {
                string path = StarModelRules.ResourcePathFor(i);
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    missing++;
                    sb.AppendLine($"✗ [{i:00}] {names[i]}：{path} が読めない（FBX 未配置 or Resources 外）");
                    continue;
                }

                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    missing++;
                    sb.AppendLine($"✗ [{i:00}] {names[i]}：レンダラーが無い");
                    continue;
                }

                var mf = renderers[0].GetComponent<MeshFilter>();
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null)
                {
                    missing++;
                    sb.AppendLine($"✗ [{i:00}] {names[i]}：メッシュが無い");
                    continue;
                }

                bool problem = false;
                if (!mesh.isReadable)
                {
                    notReadable++; problem = true;
                    sb.AppendLine($"✗ [{i:00}] {names[i]}：Read/Write が無効（頂点カラーが読めずコロナの色が出ない）");
                }
                else if (mesh.colors == null || mesh.colors.Length == 0)
                {
                    noColor++; problem = true;
                    sb.AppendLine($"✗ [{i:00}] {names[i]}：頂点カラーが無い（表面色が白になる）");
                }

                int slots = renderers[0].sharedMaterials != null ? renderers[0].sharedMaterials.Length : 0;
                if (slots < 2)
                {
                    badSlots++; problem = true;
                    sb.AppendLine($"△ [{i:00}] {names[i]}：マテリアルスロットが {slots}（Photosphere/Prominence の2枠を想定）");
                }

                totalTris += mesh.triangles.Length / 3;
                if (!problem) ok++;
            }

            string head =
                $"恒星モデル検証：{names.Length} 体中 正常 {ok} / 欠落 {missing} / 読み取り不可 {notReadable} / " +
                $"頂点カラー無し {noColor} / スロット不足 {badSlots}　（総三角形 {totalTris:N0}）";

            if (missing + notReadable + noColor + badSlots == 0)
            {
                Debug.Log($"[StarModelValidator] {head}\nすべて正常です。");
                EditorUtility.DisplayDialog("恒星モデル検証", head + "\n\nすべて正常です。", "OK");
            }
            else
            {
                Debug.LogWarning($"[StarModelValidator] {head}\n{sb}");
                EditorUtility.DisplayDialog("恒星モデル検証", head + "\n\n詳細は Console を確認してください。", "OK");
            }
        }

        [MenuItem("Ginei/恒星モデルの Read/Write を再適用", false, 201)]
        public static void ReimportStarModels()
        {
            // 既にインポート済みのモデルへ設定を効かせ直す（AssetPostprocessor はインポート時にしか走らないため）。
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources/Models" });
            int n = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                n++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[StarModelValidator] {n} 件のモデルを再インポートしました（Read/Write 等の設定を再適用）。");
        }
    }
}
