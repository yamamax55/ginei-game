using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 採用仕様 #67 の第5次分（依頼B）：支援要請の<b>結果と実行の整合</b>を固定する。
    /// 「応じました」とだけ出て何も起きない＝<b>成功に見える失敗</b>を作らないことが要点。
    /// </summary>
    public class SupportOrderExecutionTests
    {
        private static SupportRequestParams P => SupportRequestParams.Default;

        // ===== 対象消失は承諾させずに失効させる =====

        /// <summary>★攻撃目標が消えた要請は、返事を待たずに失効する（承諾にしない）。</summary>
        [Test]
        public void Judge_OrderTargetGone_ExpiresInsteadOfAccepting()
        {
            // 受け手は健在・応じる気も十分・返事の時間も来ている＝本来なら承諾になる状況。
            Assert.AreEqual(SupportRequestOutcome.承諾,
                SupportRequestRules.Judge(true, true, P.replySeconds, 1f, P));

            // それでも命令の対象が失われていれば失効。
            Assert.AreEqual(SupportRequestOutcome.失効,
                SupportRequestRules.Judge(true, false, P.replySeconds, 1f, P));
        }

        /// <summary>対象消失は検討中の段階でも即座に失効させる（黙って待たせない）。</summary>
        [Test]
        public void Judge_OrderTargetGone_ExpiresEvenBeforeReply()
        {
            Assert.AreEqual(SupportRequestOutcome.検討中,
                SupportRequestRules.Judge(true, true, 0f, 1f, P));
            Assert.AreEqual(SupportRequestOutcome.失効,
                SupportRequestRules.Judge(true, false, 0f, 1f, P));
        }

        /// <summary>従来の4引数版は「対象は有効」として振る舞う（後方互換）。</summary>
        [Test]
        public void Judge_LegacyOverload_AssumesTargetValid()
        {
            for (float t = 0f; t <= P.expireSeconds + 1f; t += 1f)
            {
                Assert.AreEqual(SupportRequestRules.Judge(true, true, t, 1f, P),
                                SupportRequestRules.Judge(true, t, 1f, P),
                                "経過秒で挙動が変わっている: " + t);
            }
        }

        // ===== 実行できたかの区別 =====

        [Test]
        public void CarriedOut_OnlyForExecuted()
        {
            Assert.IsTrue(SupportOrderExecutionRules.IsCarriedOut(SupportOrderExecution.実行));
            Assert.IsFalse(SupportOrderExecutionRules.IsCarriedOut(SupportOrderExecution.未実行));
            Assert.IsFalse(SupportOrderExecutionRules.IsCarriedOut(SupportOrderExecution.対象消失));
            Assert.IsFalse(SupportOrderExecutionRules.IsCarriedOut(SupportOrderExecution.手段なし));
            Assert.IsFalse(SupportOrderExecutionRules.IsCarriedOut(SupportOrderExecution.実行不可));
        }

        /// <summary>未実行は「失敗」ではない（まだ手を付けていないだけ）。</summary>
        [Test]
        public void Failure_ExcludesNotYetExecuted()
        {
            Assert.IsFalse(SupportOrderExecutionRules.IsFailure(SupportOrderExecution.未実行));
            Assert.IsFalse(SupportOrderExecutionRules.IsFailure(SupportOrderExecution.実行));
            Assert.IsTrue(SupportOrderExecutionRules.IsFailure(SupportOrderExecution.対象消失));
            Assert.IsTrue(SupportOrderExecutionRules.IsFailure(SupportOrderExecution.手段なし));
            Assert.IsTrue(SupportOrderExecutionRules.IsFailure(SupportOrderExecution.実行不可));
        }

        // ===== 二重実行の防止 =====

        /// <summary>★一度でも手を付けた要請は、二度と実行しない。</summary>
        [Test]
        public void CanExecute_OnlyOnceAndOnlyWhenAccepted()
        {
            Assert.IsTrue(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.未実行, SupportRequestOutcome.承諾));

            // すでに実行済み／失敗済みなら通さない。
            Assert.IsFalse(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.実行, SupportRequestOutcome.承諾));
            Assert.IsFalse(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.対象消失, SupportRequestOutcome.承諾));
            Assert.IsFalse(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.手段なし, SupportRequestOutcome.承諾));
            Assert.IsFalse(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.実行不可, SupportRequestOutcome.承諾));
        }

        /// <summary>承諾以外では実行しない（拒否・失効・検討中で勝手に動かさない）。</summary>
        [Test]
        public void CanExecute_NeverWithoutAcceptance()
        {
            Assert.IsFalse(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.未実行, SupportRequestOutcome.拒否));
            Assert.IsFalse(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.未実行, SupportRequestOutcome.失効));
            Assert.IsFalse(SupportOrderExecutionRules.CanExecute(
                SupportOrderExecution.未実行, SupportRequestOutcome.検討中));
        }

        // ===== 通知の文面（成功に見える失敗を作らない） =====

        /// <summary>★承諾でも実行できていなければ、成功の文面にしない。</summary>
        [Test]
        public void ResultText_AcceptedButNotExecuted_IsNotSuccess()
        {
            string ok = SupportOrderExecutionRules.ResultText(
                SupportRequestOutcome.承諾, SupportOrderExecution.実行, "第7艦隊", SupportOrderKind.移動);
            StringAssert.Contains("応じました", ok);

            string gone = SupportOrderExecutionRules.ResultText(
                SupportRequestOutcome.承諾, SupportOrderExecution.対象消失, "第7艦隊", SupportOrderKind.攻撃);
            StringAssert.Contains("実行できません", gone);
            StringAssert.Contains("目標がすでに失われて", gone);
            Assert.AreNotEqual(ok, gone);

            string none = SupportOrderExecutionRules.ResultText(
                SupportRequestOutcome.承諾, SupportOrderExecution.手段なし, "第7艦隊", SupportOrderKind.移動);
            StringAssert.Contains("実行する手段がありません", none);

            string denied = SupportOrderExecutionRules.ResultText(
                SupportRequestOutcome.承諾, SupportOrderExecution.実行不可, "第7艦隊", SupportOrderKind.陣形変更);
            StringAssert.Contains("実行できませんでした", denied);

            // 3つの失敗はすべて別々の文面＝実機でどこで止まったか分かる。
            Assert.AreNotEqual(gone, none);
            Assert.AreNotEqual(none, denied);
            Assert.AreNotEqual(gone, denied);
        }

        /// <summary>承諾以外は従来どおりの文面（実行結果を混ぜない）。</summary>
        [Test]
        public void ResultText_NonAcceptance_MatchesOutcomeText()
        {
            Assert.AreEqual(SupportRequestRules.OutcomeText(
                                SupportRequestOutcome.拒否, "第7艦隊", SupportOrderKind.攻撃),
                            SupportOrderExecutionRules.ResultText(
                                SupportRequestOutcome.拒否, SupportOrderExecution.未実行,
                                "第7艦隊", SupportOrderKind.攻撃));
            Assert.AreEqual(SupportRequestRules.OutcomeText(
                                SupportRequestOutcome.失効, "第7艦隊", SupportOrderKind.移動),
                            SupportOrderExecutionRules.ResultText(
                                SupportRequestOutcome.失効, SupportOrderExecution.対象消失,
                                "第7艦隊", SupportOrderKind.移動));
        }

        /// <summary>★実行できたときだけ「情報」＝それ以外は必ず「注意」で目に入る。</summary>
        [Test]
        public void Noteworthy_OnlySuccessIsQuiet()
        {
            Assert.IsFalse(SupportOrderExecutionRules.IsNoteworthy(
                SupportRequestOutcome.承諾, SupportOrderExecution.実行));

            Assert.IsTrue(SupportOrderExecutionRules.IsNoteworthy(
                SupportRequestOutcome.承諾, SupportOrderExecution.対象消失));
            Assert.IsTrue(SupportOrderExecutionRules.IsNoteworthy(
                SupportRequestOutcome.承諾, SupportOrderExecution.手段なし));
            Assert.IsTrue(SupportOrderExecutionRules.IsNoteworthy(
                SupportRequestOutcome.承諾, SupportOrderExecution.実行不可));
            Assert.IsTrue(SupportOrderExecutionRules.IsNoteworthy(
                SupportRequestOutcome.拒否, SupportOrderExecution.未実行));
            Assert.IsTrue(SupportOrderExecutionRules.IsNoteworthy(
                SupportRequestOutcome.失効, SupportOrderExecution.未実行));
        }

        /// <summary>要請を出す前に対象が消えていたときは「応じましたが」と言わない。</summary>
        [Test]
        public void CannotRequestText_DoesNotClaimAcceptance()
        {
            string s = SupportOrderExecutionRules.CannotRequestText("第7艦隊", SupportOrderKind.攻撃);
            StringAssert.Contains("出せません", s);
            StringAssert.DoesNotContain("応じ", s);
            Assert.IsFalse(string.IsNullOrEmpty(
                SupportOrderExecutionRules.CannotRequestText("", SupportOrderKind.攻撃)));
        }

        // ===== 承諾後に AI が上書きしないこと（命令の維持） =====

        /// <summary>
        /// ★移動と攻撃は AI 操舵を止める必要がある。
        /// 止めないと <c>FleetAI.Update</c> が次のフレームで自分の行き先を <c>SetDestination</c> し、
        /// 要請した移動先が即座に上書きされる（実機で「承諾したのに指定先へ行かない」に見える）。
        /// </summary>
        [Test]
        public void ManualSteering_RequiredForMoveAndAttack()
        {
            Assert.IsTrue(SupportOrderExecutionRules.RequiresManualSteering(SupportOrderKind.移動),
                          "移動を止めないと AI に行き先を上書きされる");
            Assert.IsTrue(SupportOrderExecutionRules.RequiresManualSteering(SupportOrderKind.攻撃),
                          "攻撃も足を止めないと AI が別の敵へ寄っていく");
        }

        /// <summary>
        /// 陣形変更は AI 操舵を止めない
        /// ＝プレイヤーの直接命令（<c>FleetCommander.ChangeFormation</c>）も止めておらず、
        /// 要請だけ特別扱いしないため。
        /// </summary>
        [Test]
        public void ManualSteering_NotRequiredForFormation()
        {
            Assert.IsFalse(SupportOrderExecutionRules.RequiresManualSteering(SupportOrderKind.陣形変更));
        }

        /// <summary>★実行できていない要請で AI 操舵を止めない（動かないのに AI を黙らせない）。</summary>
        [Test]
        public void ManualSteering_OnlyPairedWithCarriedOut()
        {
            // 呼び手は「実行できた」かつ「操舵が要る」の両方でだけ止める。
            foreach (SupportOrderExecution e in new[]
            {
                SupportOrderExecution.未実行, SupportOrderExecution.対象消失,
                SupportOrderExecution.手段なし, SupportOrderExecution.実行不可,
            })
            {
                Assert.IsFalse(SupportOrderExecutionRules.IsCarriedOut(e)
                               && SupportOrderExecutionRules.RequiresManualSteering(SupportOrderKind.移動),
                               "実行できていないのに AI を黙らせている: " + e);
            }

            Assert.IsTrue(SupportOrderExecutionRules.IsCarriedOut(SupportOrderExecution.実行)
                          && SupportOrderExecutionRules.RequiresManualSteering(SupportOrderKind.移動));
        }

        /// <summary>名前が無くても文面が壊れない（null安全）。</summary>
        [Test]
        public void Texts_AreNullSafe()
        {
            Assert.IsFalse(string.IsNullOrEmpty(SupportOrderExecutionRules.ResultText(
                SupportRequestOutcome.承諾, SupportOrderExecution.対象消失, null, SupportOrderKind.攻撃)));
            Assert.IsFalse(string.IsNullOrEmpty(SupportOrderExecutionRules.ResultText(
                SupportRequestOutcome.承諾, SupportOrderExecution.未実行, "", SupportOrderKind.移動)));
        }
    }
}
