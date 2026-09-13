using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 「窓のドラッグ／リサイズが効かない」を実機で切り分けるための QA メニュー。
    /// <b>Play 中のみ動作し、何も保存しない</b>（診断用の GameObject は DontSave・停止で消える）。
    ///
    /// 推測で修正を重ねないために、押した瞬間に<b>実際に誰が入力を受け取ったか</b>を Console へ出す。
    /// 詳細は <see cref="WindowInputDiagnostics"/>。
    /// メニューのパスに「半角スペース＋#」を入れないこと（Unity がショートカット構文と解釈して登録に失敗する）。
    /// </summary>
    public static class WindowInputQaMenu
    {
        private const string Root = "Ginei/QA（Play中のみ・保存しない）/窓の入力診断/";

        [MenuItem(Root + "診断を開始（押下・ドラッグ・離しを記録）", false, 400)]
        public static void Start()
        {
            if (!RequirePlay()) return;
            WindowInputDiagnostics.Enable();
            EditorUtility.DisplayDialog("窓の入力診断",
                "診断を開始しました。\n\n" +
                "1. 会戦ウィンドウを開く\n" +
                "2. タイトルバーを掴んで動かす\n" +
                "3. 右下の金色グリップを掴んで動かす\n" +
                "4. × を押す\n\n" +
                "それぞれの操作のあと Console を確認してください。\n" +
                "「押下」の行に出る一覧の先頭が、実際に入力を受け取った UI です。\n\n" +
                "終わったら「診断を停止」を実行してください。", "OK");
        }

        [MenuItem(Root + "診断を停止", false, 401)]
        public static void Stop()
        {
            if (!RequirePlay()) return;
            WindowInputDiagnostics.Disable();
        }

        [MenuItem(Root + "いまの会戦ウィンドウの矩形を出す", false, 402)]
        public static void DumpRects()
        {
            if (!RequirePlay()) return;
            // 診断が動いていなくても1回だけ出せるように、一時的に起動して即停止する。
            bool wasRunning = WindowInputDiagnostics.IsRunning;
            if (!wasRunning) WindowInputDiagnostics.Enable();
            if (!wasRunning) WindowInputDiagnostics.Disable();
        }

        [MenuItem(Root + "会戦HUDの実測を出す（極小の原因切り分け）", false, 403)]
        public static void DumpHud()
        {
            if (!RequirePlay()) return;
            WindowInputDiagnostics.DumpBattleHud();
        }

        private static bool RequirePlay()
        {
            if (Application.isPlaying) return true;
            EditorUtility.DisplayDialog("窓の入力診断", "Play 中のみ使えます（保存はしません）。", "OK");
            return false;
        }
    }
}
