using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// Windows 実行ファイルをメニューから作る（#配布ビルド）。Editor を開いたまま使えるので、
    /// 検証のたびに Editor を閉じてバッチモードを回す必要がない。
    ///
    /// <b>既存の配布物を壊さない</b>のが要件：出力先は既定で <c>Ginei-Strategy-Map-v2</c>（既存の
    /// <c>Ginei-Windows</c> とは別フォルダ）で、書き込む前に「その中に .exe があるか」を確かめて確認を取る。
    /// シーンは Build Settings の有効なものをそのまま使う（順序＝Title が先頭）。
    /// </summary>
    public static class WindowsBuildMenu
    {
        /// <summary>既定の出力ルート（配布物を並べて比較できる場所）。</summary>
        private const string DefaultRoot = @"C:\Users\htccj\Documents\Codex\2026-09-09\new-chat\outputs";
        /// <summary>戦略MAP刷新版（第1弾）の出力フォルダ名。比較用に残す＝上書きしない。</summary>
        private const string FolderV2 = "Ginei-Strategy-Map-v2";
        /// <summary>航路の非交差化＋要所の3D要塞（第2弾）の出力フォルダ名。比較用に残す。</summary>
        private const string FolderV3 = "Ginei-Strategy-Map-v3";
        /// <summary>星系名ごとの3D恒星（第3弾）の出力フォルダ名。比較用に残す。</summary>
        private const string FolderV4 = "Ginei-Strategy-Map-v4";
        /// <summary>艦隊集約UI・窓操作・入力・画面比率対応（第4弾）の出力フォルダ名。</summary>
        private const string FolderV5 = "Ginei-Strategy-Map-v5";
        /// <summary>回廊要塞（#40）と援軍ワープイン（#38）の出力フォルダ名。<b>v5 は上書きしない</b>。</summary>
        private const string FolderV6 = "Ginei-Strategy-Map-v6";
        private const string ExeName = "Ginei.exe";
        private const string DefaultFolder = FolderV6;

        [MenuItem("Ginei/Windows ビルド（v6 出力・最新）", false, 100)]
        public static void BuildV6() => Build(Path.Combine(DefaultRoot, FolderV6));

        [MenuItem("Ginei/Windows ビルド（v5 出力・比較用）", false, 101)]
        public static void BuildV5() => Build(Path.Combine(DefaultRoot, FolderV5));

        [MenuItem("Ginei/Windows ビルド（v4 出力・比較用）", false, 102)]
        public static void BuildV4() => Build(Path.Combine(DefaultRoot, FolderV4));

        [MenuItem("Ginei/Windows ビルド（v3 出力・比較用）", false, 103)]
        public static void BuildV3() => Build(Path.Combine(DefaultRoot, FolderV3));

        [MenuItem("Ginei/Windows ビルド（v2 出力・比較用）", false, 104)]
        public static void BuildV2() => Build(Path.Combine(DefaultRoot, FolderV2));

        [MenuItem("Ginei/Windows ビルド（出力先を選ぶ）…", false, 105)]
        public static void BuildToChosenFolder()
        {
            string dir = EditorUtility.SaveFolderPanel("Windows ビルドの出力先", DefaultRoot, DefaultFolder);
            if (string.IsNullOrEmpty(dir)) return;
            Build(dir);
        }

        private static void Build(string outputDir)
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Ginei ビルド",
                    "Build Settings に有効なシーンがありません。File > Build Settings で追加してください。", "OK");
                return;
            }

            string exePath = Path.Combine(outputDir, ExeName);

            // 既存の配布物を黙って上書きしない（別バージョンを潰すと比較検証ができなくなる）。
            if (Directory.Exists(outputDir) && Directory.GetFiles(outputDir, "*.exe").Length > 0)
            {
                bool ok = EditorUtility.DisplayDialog("Ginei ビルド",
                    $"出力先に既存のビルドがあります。上書きしますか？\n\n{outputDir}", "上書きする", "やめる");
                if (!ok) return;
            }
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Ginei] Windows ビルド成功: {exePath}（{s.totalSize / (1024 * 1024)} MB / {s.totalTime}）");
                if (EditorUtility.DisplayDialog("Ginei ビルド", $"成功しました。\n\n{exePath}", "フォルダを開く", "閉じる"))
                    EditorUtility.RevealInFinder(exePath);
            }
            else
            {
                Debug.LogError($"[Ginei] Windows ビルド失敗: {s.result}（エラー {s.totalErrors} 件）");
                EditorUtility.DisplayDialog("Ginei ビルド",
                    $"失敗しました（{s.result}・エラー {s.totalErrors} 件）。Console を確認してください。", "OK");
            }
        }

        /// <summary>Build Settings で有効なシーンのパス一覧（順序はそのまま＝先頭が起動シーン）。</summary>
        private static string[] EnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].enabled) list.Add(all[i].path);
            return list.ToArray();
        }
    }
}
