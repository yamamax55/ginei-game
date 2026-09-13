using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// QA：<b>士気を動かした原因</b>の記録（#士気原因の切り分け・Editor 限定・Play 中のみ）。
    ///
    /// <b>なぜ要るか</b>：実機の記録には
    /// 「敗走 解除（士気 0.0→6.0）」のように<b>結果だけ</b>が写り、
    /// その 6.0 を入れたのが自然回復なのか、会戦イベント（英雄的奮戦＝+6）なのか、
    /// 敵旗艦の撃墜による高揚なのかが残らない。
    /// どれも士気を上げるので、値と時刻だけで言い当てると<b>推測</b>になる。
    ///
    /// この記録は <see cref="MoraleAuditLog"/> を有効にするだけ＝
    /// <b>書き込んだ側が名乗った原因</b>をそのまま出す（こちらで推し量らない）。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item><b>状態を変えない</b>（読み取りと記録だけ。士気の計算にも製品の回復仕様にも触らない）。</item>
    ///   <item><b>オプトイン</b>＝メニューで開始するまで1件も積まない。Play を抜けたら自動で止める。</item>
    ///   <item>微小な毎フレームの自然回復は既定で積まない。
    ///         ただし<b>敗走が動いた変化は大きさに関係なく必ず残す</b>（原因を取りこぼさない）。</item>
    ///   <item>件数は有界（<see cref="MoraleAuditLog.Capacity"/>）。あふれたら件数を明示する。</item>
    /// </list>
    /// </summary>
    public static class MoraleSourceQaMenu
    {
        private const string MenuStart = "Ginei/QA: 士気の原因 記録を開始（Play中）";
        private const string MenuStartFine = "Ginei/QA: 士気の原因 記録を開始（細かく＝自然回復の刻みも残す）";
        private const string MenuStop = "Ginei/QA: 士気の原因 記録を停止";
        private const string MenuDump = "Ginei/QA: 士気の原因 記録を出力";
        private const string MenuClear = "Ginei/QA: 士気の原因 記録を消去";

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Play を抜けたら必ず止める＝通常プレイへ観測を持ち越さない。</summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                MoraleAuditLog.Enabled = false;
        }

        [MenuItem(MenuStart, false, 360)]
        private static void Start() => StartWith(MoraleAuditLog.DefaultMinAbsDelta, "既定");

        [MenuItem(MenuStartFine, false, 361)]
        private static void StartFine() => StartWith(0f, "細かく");

        private static void StartWith(float minAbsDelta, string label)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("［士気の原因］Play 中に開始してください（会戦が動いていないと何も起きません）。");
                return;
            }
            MoraleAuditLog.Clear();
            MoraleAuditLog.MinAbsDelta = minAbsDelta;
            MoraleAuditLog.Enabled = true;
            Debug.Log("［士気の原因］記録開始（" + label + "／しきい値 " + minAbsDelta.ToString("0.####") +
                      "）。敗走が動いた変化はしきい値に関係なく残します。");
        }

        [MenuItem(MenuStop, false, 362)]
        private static void Stop()
        {
            MoraleAuditLog.Enabled = false;
            Debug.Log("［士気の原因］記録停止（" + MoraleAuditLog.Count + " 件を保持）。出力メニューで確認できます。");
        }

        [MenuItem(MenuClear, false, 364)]
        private static void ClearLog()
        {
            // ★消すのは記録だけ＝しきい値（細かく見る設定）は保つ。
            //   途中で区切っても、そのあとの刻みを取りこぼさないため。
            MoraleAuditLog.Clear();
            Debug.Log("［士気の原因］記録を消去しました（しきい値 " +
                      MoraleAuditLog.MinAbsDelta.ToString("0.####") + " は保持。記録中か＝" +
                      MoraleAuditLog.Enabled + "）。");
        }

        [MenuItem(MenuDump, false, 363)]
        private static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[士気の原因・記録]");
            sb.AppendLine("士気を動かした側が名乗った原因をそのまま並べています（推測でラベルは付けていません）。");
            sb.AppendLine("★記録するだけで状態は変えていません。★記録が無いことは「起きなかった」証明にはなりません");
            sb.AppendLine("　（開始前・しきい値未満・容量あふれは写りません）。");
            sb.AppendLine("記録中か＝" + MoraleAuditLog.Enabled +
                          "　件数＝" + MoraleAuditLog.Count +
                          "　あふれて捨てた件数＝" + MoraleAuditLog.Dropped +
                          "　しきい値＝" + MoraleAuditLog.MinAbsDelta.ToString("0.####"));
            sb.AppendLine();

            System.Collections.Generic.IReadOnlyList<MoraleChangeRecord> all = MoraleAuditLog.All;
            if (all.Count == 0)
            {
                sb.AppendLine("（記録なし）開始メニューを押してから会戦を進めてください。");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine("t=ゲーム時刻 / 原因 / 艦隊 / 士気 前→後 / 敗走 前→後 / 最終交戦からの秒 / 不退転");
            for (int i = 0; i < all.Count; i++)
            {
                MoraleChangeRecord r = all[i];
                string mark = r.ClearedRout ? "★敗走 解除 " : (r.StartedRout ? "★敗走 開始 " : "");
                string detail = string.IsNullOrEmpty(r.detail) ? "" : "（" + r.detail + "）";
                sb.AppendLine(
                    "t=" + r.gameTime.ToString("0.00") +
                    "  " + mark + r.source + detail +
                    "  " + r.fleet +
                    "  士気 " + r.before.ToString("0.000") + "→" + r.after.ToString("0.000") +
                    "（" + (r.Delta >= 0f ? "+" : "") + r.Delta.ToString("0.000") + "）" +
                    "  敗走 " + r.routedBefore + "→" + r.routedAfter +
                    "  最終交戦から " + r.secondsSinceCombat.ToString("0.00") + " 秒" +
                    "  不退転=" + r.moraleLocked);
            }

            // 原因ごとの件数（切り分けの目安）。
            sb.AppendLine();
            sb.AppendLine("［原因ごとの件数］");
            System.Array kinds = System.Enum.GetValues(typeof(MoraleChangeSource));
            for (int i = 0; i < kinds.Length; i++)
            {
                var k = (MoraleChangeSource)kinds.GetValue(i);
                int n = MoraleAuditLog.CountBySource(k);
                if (n > 0) sb.AppendLine("  " + k + " ＝ " + n + " 件");
            }

            // 「何が敗走を解いたか」を先頭で答えられるように最後の解除を添える。
            if (MoraleAuditLog.TryGetLastRoutClear(null, out MoraleChangeRecord last))
            {
                sb.AppendLine();
                sb.AppendLine("［最後に敗走を解いた変化］ t=" + last.gameTime.ToString("0.00") +
                              " / 原因＝" + last.source +
                              (string.IsNullOrEmpty(last.detail) ? "" : "（" + last.detail + "）") +
                              " / " + last.fleet +
                              " / 士気 " + last.before.ToString("0.000") + "→" + last.after.ToString("0.000") +
                              " / 最終交戦から " + last.secondsSinceCombat.ToString("0.00") + " 秒");
            }

            Debug.Log(sb.ToString());
        }
    }
}
