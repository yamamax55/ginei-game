using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// エディタが非フォーカスでも、外部（CLI/Git/Claude Code 等）で変更したスクリプトを
    /// 定期的に取り込んでコンパイルさせるエディタ拡張。
    /// 既定の Unity はウィンドウへ再フォーカスしたときしか再インポート/コンパイルしないため、
    /// クラウド/外部エディタ運用で「保存したのにコンパイルが走らない」を解消する。
    /// 一定間隔で <see cref="AssetDatabase.Refresh"/> を呼ぶだけ（変更が無ければほぼ無コスト）。
    /// メニュー `Ginei/自動リフレッシュ（バックグラウンド）` で ON/OFF（EditorPrefs に保存）。
    /// 再生中・コンパイル中・インポート中・フォーカス時は触らない（標準の自動更新へ譲る）。
    /// </summary>
    [InitializeOnLoad]
    public static class BackgroundAutoRefresh
    {
        private const string PrefKey = "Ginei.BackgroundAutoRefresh.Enabled";
        private const string MenuPath = "Ginei/自動リフレッシュ（バックグラウンド）";

        // ポーリング間隔（秒）。短いほど反応が速いが、毎回ファイルのタイムスタンプ走査が走る。
        private const double IntervalSeconds = 2.0;

        private static double nextCheck;

        static BackgroundAutoRefresh()
        {
            EditorApplication.update += OnUpdate;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        private static void OnUpdate()
        {
            if (!Enabled) return;
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + IntervalSeconds;

            // 再生中・コンパイル中・アセットDB更新中は触らない（壊さない）。
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            // フォーカスがある間は Unity 標準の自動リフレッシュに任せる（二重実行を避ける）。
            if (UnityEditorInternal.InternalEditorUtility.isApplicationActive) return;

            // 変更が無ければ Refresh はほぼ何もしない（タイムスタンプ走査のみ）。
            AssetDatabase.Refresh();
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            nextCheck = 0;
            Debug.Log(Enabled
                ? "バックグラウンド自動リフレッシュ：ON（非フォーカスでも定期コンパイル）"
                : "バックグラウンド自動リフレッシュ：OFF");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
