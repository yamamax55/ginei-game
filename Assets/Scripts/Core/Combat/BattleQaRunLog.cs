using System.Collections.Generic;
using System.Text;

namespace Ginei
{
    /// <summary>観測判定の結論。★未判定は合格ではない（前提が崩れた・観測が足りない）。</summary>
    public enum BattleQaVerdict
    {
        合格,
        不合格,
        未判定,
    }

    /// <summary>1項目ぶんの観測判定（何を見て・どうだったか）。</summary>
    public readonly struct BattleQaCheck
    {
        public readonly string name;
        public readonly BattleQaVerdict verdict;
        public readonly string detail;
        public readonly float gameTime;

        public BattleQaCheck(string name, BattleQaVerdict verdict, string detail, float gameTime)
        {
            this.name = name ?? "";
            this.verdict = verdict;
            this.detail = detail ?? "";
            this.gameTime = gameTime;
        }
    }

    /// <summary>
    /// 固定会戦QAの結果ログ（1回の実行＝1 run_id・純ロジック）。
    ///
    /// 各行にプリセット名・seed・run_id・ゲーム内経過時間を必ず添える。
    /// 容量は <see cref="Capacity"/>（<see cref="MoraleAuditLog.Capacity"/>・陣形保持記録と同じ 400）で、
    /// あふれたら古い行を捨てて<b>捨てた件数を数える</b>（黙って消さない）。
    /// </summary>
    public sealed class BattleQaRunLog
    {
        /// <summary>残す行数の上限。</summary>
        public const int Capacity = 400;

        public readonly string runId;
        public readonly string presetName;
        public readonly int seed;

        private readonly List<string> lines = new List<string>();
        private readonly List<BattleQaCheck> checks = new List<BattleQaCheck>();
        private int dropped;

        public BattleQaRunLog(string runId, string presetName, int seed)
        {
            this.runId = runId ?? "";
            this.presetName = presetName ?? "";
            this.seed = seed;
        }

        public int Count => lines.Count;
        /// <summary>上限超過で捨てた行数（0 でなければ記録喪失あり）。</summary>
        public int Dropped => dropped;
        public IReadOnlyList<string> Lines => lines;
        /// <summary>判定は行と別に全件保持する（件数はプリセットごとに十数件で有界）。</summary>
        public IReadOnlyList<BattleQaCheck> Checks => checks;

        /// <summary>
        /// run_id を組み立てる（例 <c>退却-s20260913-n003-ab12cd34</c>）。
        /// 通番は同一 Play セッション内の実行回数、suffix は呼び出し側が渡す一意片（Core は乱数・時計を持たない）。
        /// </summary>
        public static string FormatRunId(string presetName, int seed, int serial, string suffix)
        {
            string s = (presetName ?? "") + "-s" + seed + "-n" + serial.ToString("000");
            return string.IsNullOrEmpty(suffix) ? s : s + "-" + suffix;
        }

        /// <summary>1行足す（ゲーム内経過時間つき）。</summary>
        public void Add(float gameTime, string message)
        {
            lines.Add("[" + runId + " " + presetName + " seed=" + seed + " t=" + gameTime.ToString("0.00") + "] " + (message ?? ""));
            while (lines.Count > Capacity) { lines.RemoveAt(0); dropped++; }
        }

        /// <summary>判定を記録する（ログにも1行出す）。</summary>
        public void AddCheck(BattleQaCheck check)
        {
            checks.Add(check);
            Add(check.gameTime, "判定［" + check.verdict + "］" + check.name + "：" + check.detail);
        }

        /// <summary>結論ごとの件数。</summary>
        public int CountVerdict(BattleQaVerdict v)
        {
            int n = 0;
            for (int i = 0; i < checks.Count; i++) if (checks[i].verdict == v) n++;
            return n;
        }

        /// <summary>
        /// 全体の結論：判定0件または未判定が1件でもあれば未判定、不合格があれば不合格、残りは合格。
        /// ★「見ていないものを合格にしない」ため、未判定を不合格より優先して表に出す。
        /// </summary>
        public BattleQaVerdict Overall
        {
            get
            {
                if (checks.Count == 0 || CountVerdict(BattleQaVerdict.未判定) > 0) return BattleQaVerdict.未判定;
                return CountVerdict(BattleQaVerdict.不合格) > 0 ? BattleQaVerdict.不合格 : BattleQaVerdict.合格;
            }
        }

        /// <summary>
        /// 記録喪失の注記（空＝喪失なし）。本ログと既存の2記録（士気の原因台帳・陣形保持記録）の分をまとめて書く。
        /// </summary>
        public static string LossNotice(int runLogDropped, int moraleAuditDropped, bool formationLogTruncated)
        {
            var sb = new StringBuilder();
            if (runLogDropped > 0) sb.Append("★結果ログが上限 ").Append(Capacity).Append(" 行を超え、古い ").Append(runLogDropped).Append(" 行を失った。");
            if (moraleAuditDropped > 0) sb.Append("★士気の原因台帳が上限 ").Append(MoraleAuditLog.Capacity).Append(" 件を超え、").Append(moraleAuditDropped).Append(" 件を失った。");
            if (formationLogTruncated) sb.Append("★陣形保持記録が上限 400 行を超え、古い行を失った。");
            return sb.ToString();
        }

        /// <summary>出力用の全文（見出し＋判定の要約＋全行）。</summary>
        public string Dump(string header)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(header)) sb.Append(header).Append('\n');
            sb.Append("run_id=").Append(runId).Append(" プリセット=").Append(presetName).Append(" seed=").Append(seed)
              .Append(" 行数=").Append(lines.Count).Append(" 捨てた行=").Append(dropped).Append('\n');
            sb.Append("全体の結論=").Append(Overall)
              .Append("（合格 ").Append(CountVerdict(BattleQaVerdict.合格))
              .Append(" / 不合格 ").Append(CountVerdict(BattleQaVerdict.不合格))
              .Append(" / 未判定 ").Append(CountVerdict(BattleQaVerdict.未判定)).Append("）\n");
            for (int i = 0; i < checks.Count; i++)
                sb.Append("  ［").Append(checks[i].verdict).Append("］").Append(checks[i].name)
                  .Append("：").Append(checks[i].detail).Append('\n');
            sb.Append('\n');
            for (int i = 0; i < lines.Count; i++) sb.Append(lines[i]).Append('\n');
            return sb.ToString();
        }
    }
}
