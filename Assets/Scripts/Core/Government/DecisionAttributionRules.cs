using System.Text;

namespace Ginei
{
    /// <summary>
    /// 決裁カードに<b>誰が出して誰が決めるか</b>を出す文言（GitHub #67／作業票⑤）。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>提案者・決裁者は<b>実在の人物名だけ</b>を出す。分からなければ「―」＝架空の名前を作らない。</item>
    ///   <item>権限の根拠（役職・所掌・範囲、または権限外の理由）を<b>文章で</b>出す＝色だけに頼らない。</item>
    ///   <item>提案の対象を明示する＝承認したあとに別の対象へ振り替わらないことが読んで分かる。</item>
    ///   <item>決裁後は<b>承認できたこと</b>と<b>実際に効いたこと</b>を分けて出す。</item>
    /// </list>
    /// </summary>
    public static class DecisionAttributionRules
    {
        /// <summary>名前が空のときの表示（架空の名前を入れない）。</summary>
        public const string Unknown = "―";

        /// <summary>「提案：〜／決裁：〜」の1行。</summary>
        public static string WhoLine(PendingDecision d)
        {
            if (d == null) return "";
            string proposer = string.IsNullOrEmpty(d.proposerName) ? Unknown : d.proposerName;
            string decider = string.IsNullOrEmpty(d.deciderName) ? Unknown : d.deciderName;
            return $"提案：{proposer}　決裁：{decider}";
        }

        /// <summary>「対象：〜」の1行（名指ししていなければ勢力全体）。</summary>
        public static string TargetLine(PendingDecision d)
            => d == null ? "" : "対象：" + d.Target.Label;

        /// <summary>「権限：〜」の1行（根拠が無ければ空文字）。</summary>
        public static string AuthorityLine(PendingDecision d)
            => d == null || string.IsNullOrEmpty(d.authorityBasis) ? "" : "権限：" + d.authorityBasis;

        /// <summary>いまの状態を短く（上申中／決裁済／執行の結果）。</summary>
        public static string StatusLine(PendingDecision d)
        {
            if (d == null) return "";
            if (d.escalated) return "状態：上申中（決裁権者の返事待ち）";
            if (d.applied) return "状態：" + DecisionResolutionRules.ResultLine(d);
            if (DecisionResolutionRules.IsSettled(d)) return "状態：決裁済（執行待ち）";
            return "状態：未決";
        }

        /// <summary>
        /// カード詳細の本文（本文＋提案者/決裁者＋対象＋権限＋状態）。
        /// 本文が空でも属性だけは出す＝「誰の案件か分からない札」を作らない。
        /// </summary>
        public static string DetailText(PendingDecision d)
            => DetailText(d, d != null ? d.body : "");

        /// <summary>表示本文だけを差し替える。Core は外部生成器を参照せず、保存対象の body も変更しない。</summary>
        public static string DetailText(PendingDecision d, string displayBody)
        {
            if (d == null) return "";
            var sb = new StringBuilder(320);
            sb.Append(string.IsNullOrEmpty(displayBody) ? "（詳細なし）" : displayBody);
            sb.Append('\n').Append('\n');
            sb.Append(WhoLine(d)).Append('\n');
            sb.Append(TargetLine(d));

            string auth = AuthorityLine(d);
            if (!string.IsNullOrEmpty(auth)) sb.Append('\n').Append(auth);

            sb.Append('\n').Append(StatusLine(d));
            return sb.ToString();
        }

        /// <summary>右下カードの見出しに添える短い1行（提案者と、上申中かどうかだけ）。</summary>
        public static string HeadlineNote(PendingDecision d)
        {
            if (d == null) return "";
            if (d.escalated)
                return string.IsNullOrEmpty(d.deciderName) ? "（上申中）" : $"（{d.deciderName} へ上申中）";
            return string.IsNullOrEmpty(d.proposerName) ? "" : $"（提案：{d.proposerName}）";
        }
    }
}
