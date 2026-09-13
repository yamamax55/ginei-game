namespace Ginei
{
    /// <summary>
    /// 承諾された支援要請を<b>実際に実行できたか</b>（GitHub #67・第5次）。
    ///
    /// 相手が応じたことと、その命令が盤面で実行されたことは<b>別の事実</b>。
    /// 目標がすでに失われていた・必要な機能が無い・その陣形を布けない、といった理由で
    /// 実行できないことがある。ここを区別しないと
    /// <b>「応じました」だけ出て何も起きない＝成功に見える失敗</b>になる。
    /// </summary>
    public enum SupportOrderExecution
    {
        /// <summary>まだ実行していない（初期値）。</summary>
        未実行,

        /// <summary>要請どおり実行した。</summary>
        実行,

        /// <summary>命令の対象（攻撃目標など）がすでに失われていた。</summary>
        対象消失,

        /// <summary>実行に必要な機能を持っていない（移動・砲・編制の欠落）。</summary>
        手段なし,

        /// <summary>実行を試みたが通らなかった（陣形の資格・状況など）。</summary>
        実行不可,
    }

    /// <summary>
    /// 支援要請の<b>実行結果</b>の扱い（純ロジック・test-first）。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>実行できたときだけ成功として知らせる（実行不能を承諾で塗りつぶさない）。</item>
    ///   <item>失敗は<b>理由ごとに違う文面</b>にする（どこで止まったのか実機で分かる）。</item>
    ///   <item>1件の要請につき実行は1回だけ（<see cref="CanExecute"/> が二重実行を防ぐ）。</item>
    /// </list>
    /// </summary>
    public static class SupportOrderExecutionRules
    {
        /// <summary>要請どおり実行できたか。</summary>
        public static bool IsCarriedOut(SupportOrderExecution execution)
            => execution == SupportOrderExecution.実行;

        /// <summary>応じたのに実行できなかったか（＝通知を成功にしてはいけない）。</summary>
        public static bool IsFailure(SupportOrderExecution execution)
            => execution == SupportOrderExecution.対象消失
            || execution == SupportOrderExecution.手段なし
            || execution == SupportOrderExecution.実行不可;

        /// <summary>
        /// いま実行してよいか（二重実行の防止）。
        /// すでに <see cref="SupportOrderExecution.未実行"/> でない＝一度手を付けているので、もう実行しない。
        /// </summary>
        public static bool CanExecute(SupportOrderExecution current, SupportRequestOutcome outcome)
            => current == SupportOrderExecution.未実行 && outcome == SupportRequestOutcome.承諾;

        /// <summary>
        /// 顛末と実行結果をまとめた1行。承諾でも実行できていなければ<b>そう書く</b>。
        /// 承諾以外（拒否・失効・検討中）は実行結果を見ない。
        /// </summary>
        public static string ResultText(SupportRequestOutcome outcome, SupportOrderExecution execution,
                                        string targetName, SupportOrderKind kind)
        {
            if (outcome != SupportRequestOutcome.承諾)
                return SupportRequestRules.OutcomeText(outcome, targetName, kind);

            string who = string.IsNullOrEmpty(targetName) ? "指揮系統外の部隊" : targetName;
            switch (execution)
            {
                case SupportOrderExecution.実行:
                    return SupportRequestRules.OutcomeText(outcome, targetName, kind);
                case SupportOrderExecution.対象消失:
                    return $"{who} は{kind}の要請に応じましたが、目標がすでに失われていました（実行できません）";
                case SupportOrderExecution.手段なし:
                    return $"{who} は{kind}の要請に応じましたが、実行する手段がありません（実行できません）";
                case SupportOrderExecution.実行不可:
                    return $"{who} は{kind}の要請に応じましたが、その命令を実行できませんでした";
                default:
                    return $"{who} は{kind}の要請に応じましたが、まだ実行されていません";
            }
        }

        /// <summary>
        /// その命令は<b>AI 操舵を止める必要があるか</b>（承諾後に AI が上書きしないように）。
        ///
        /// <list type="bullet">
        ///   <item><b>移動</b>＝行き先そのもの。止めないと AI が次のフレームで別の座標へ向け直す。</item>
        ///   <item><b>攻撃</b>＝砲は指定目標を狙うが、足を止めないと AI が自分の見つけた敵へ寄っていく
        ///         ＝狙いと動きがちぐはぐになる。</item>
        ///   <item><b>陣形変更</b>＝足に関わらない。<b>プレイヤーの直接命令でも止めていない</b>ので、
        ///         要請だけ特別扱いしない（AI の陣形自動切替との兼ね合いは直接命令と同じ土俵）。</item>
        /// </list>
        ///
        /// 解除は AI 側が自分で行う（移動が終わり手動標的も標準命令も無くなった時点で復帰）
        /// ＝ここは「止めるべきか」だけを答える。
        /// </summary>
        public static bool RequiresManualSteering(SupportOrderKind kind)
            => kind == SupportOrderKind.移動 || kind == SupportOrderKind.攻撃;

        /// <summary>
        /// 要請を出す前に対象がすでに失われていたときの1行（＝要請を受理しない）。
        /// まだ誰も応じていないので「応じましたが」とは書かない。
        /// </summary>
        public static string CannotRequestText(string targetName, SupportOrderKind kind)
        {
            string who = string.IsNullOrEmpty(targetName) ? "指揮系統外の部隊" : targetName;
            return $"{who} への{kind}の要請は出せません（目標がすでに失われています）";
        }

        /// <summary>
        /// その結果を<b>注意として知らせるべきか</b>（成功だけ情報・それ以外は注意）。
        /// 通知の重要度を呼び手ごとに書き分けないための単一窓口。
        /// </summary>
        public static bool IsNoteworthy(SupportRequestOutcome outcome, SupportOrderExecution execution)
            => outcome != SupportRequestOutcome.承諾 || !IsCarriedOut(execution);
    }
}
