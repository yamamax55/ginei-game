namespace Ginei
{
    /// <summary>決裁を確定できない理由。<see cref="なし"/>＝確定してよい。</summary>
    public enum DecisionResolveRejection
    {
        なし,
        案件が無い,
        すでに決裁済み,
        選択肢が不正,
    }

    /// <summary>
    /// 決裁を確定する<b>唯一の検証</b>（作業票①）。
    ///
    /// <b>なぜ要るか</b>：決裁は4つの経路（右下カード・決裁ボード・期限切れの自動処理・AIの自動裁可）から
    /// 確定されるのに、<see cref="DecisionQueue.Resolve"/> には検査が無かった。
    /// そのため
    /// <list type="bullet">
    ///   <item>解決済みの案件をもう一度確定できる（外から <c>Resolve</c> を呼べば何度でも）</item>
    ///   <item>選択肢の範囲外の番号がそのまま入る</item>
    ///   <item>期限切れで自動解決した直後に手で押すと、同じ案件が二度効果を出す</item>
    /// </list>
    /// が成立していた。ここを通してからでないと確定できないようにする。
    ///
    /// <b>効果を一度だけにする鍵</b>は <see cref="PendingDecision.applied"/>。
    /// 「決裁済という状態」と「効果を適用したか」を分けて持ち、適用は
    /// <see cref="ClaimForApply"/> を勝ち取った1回だけに許す（どの経路から来ても同じ）。
    ///
    /// 乱数なし・決定論。
    /// </summary>
    public static class DecisionResolutionRules
    {
        /// <summary>その案件をその選択肢で確定してよいか。</summary>
        public static DecisionResolveRejection CanResolve(PendingDecision d, int choiceIndex)
        {
            if (d == null) return DecisionResolveRejection.案件が無い;
            if (IsSettled(d)) return DecisionResolveRejection.すでに決裁済み;
            if (d.choices == null || choiceIndex < 0 || choiceIndex >= d.choices.Count)
                return DecisionResolveRejection.選択肢が不正;
            return DecisionResolveRejection.なし;
        }

        /// <summary>すでに確定している（決裁済 or 自動解決）。</summary>
        public static bool IsSettled(PendingDecision d)
            => d != null && (d.status == DecisionStatus.決裁済 || d.status == DecisionStatus.自動解決);

        /// <summary>理由の日本語1行（<see cref="DecisionResolveRejection.なし"/>＝空文字）。</summary>
        public static string RejectionText(DecisionResolveRejection reason)
        {
            switch (reason)
            {
                case DecisionResolveRejection.なし: return "";
                case DecisionResolveRejection.案件が無い: return "その決裁はもうありません";
                case DecisionResolveRejection.すでに決裁済み: return "この決裁はすでに処理済みです";
                case DecisionResolveRejection.選択肢が不正: return "その選択肢は選べません";
                default: return "決裁できません";
            }
        }

        /// <summary>
        /// 効果を適用する権利を<b>1回だけ</b>取る。取れたら true（呼び手が効果を適用する義務を負う）。
        /// すでに誰かが取っていれば false＝二重適用にならない。
        ///
        /// 「確定（status）」と「適用（applied）」を分けているので、期限切れの自動解決が先に適用した
        /// 案件を手で押しても、2回目はここで false になる。
        /// </summary>
        public static bool ClaimForApply(PendingDecision d)
        {
            if (d == null || d.applied) return false;
            d.applied = true;
            return true;
        }

        /// <summary>
        /// 確定（選択の記録と状態の変更）だけを行う。効果の適用は <see cref="ClaimForApply"/> の側。
        /// <paramref name="auto"/>＝期限切れ/AI による自動処理（状態を <see cref="DecisionStatus.自動解決"/> にする）。
        /// 検証に落ちたら何もせず false。
        /// </summary>
        public static bool Settle(PendingDecision d, int choiceIndex, bool auto,
                                  out DecisionResolveRejection reason)
        {
            reason = CanResolve(d, choiceIndex);
            if (reason != DecisionResolveRejection.なし) return false;

            d.chosenIndex = choiceIndex;
            d.status = auto ? DecisionStatus.自動解決 : DecisionStatus.決裁済;
            return true;
        }

        /// <summary>執行の結果を案件へ記録する（決裁後の画面表示に使う）。</summary>
        public static void RecordResult(PendingDecision d, in PetitionActionResult result)
        {
            if (d == null) return;
            d.outcome = result.outcome;
            d.resultDetail = result.detail;
        }

        /// <summary>
        /// 決裁後に出す1行。<b>承認できたこと</b>と<b>実際に効いたこと</b>を区別して書く。
        /// まだ適用していなければ空文字。
        /// </summary>
        public static string ResultLine(PendingDecision d)
        {
            if (d == null || !d.applied) return "";
            if (string.IsNullOrEmpty(d.resultDetail))
                return d.outcome == PetitionActionOutcome.実行 ? "執行しました" : "執行されませんでした";
            return d.outcome == PetitionActionOutcome.実行
                ? "執行：" + d.resultDetail
                : "執行できず：" + d.resultDetail;
        }
    }
}
