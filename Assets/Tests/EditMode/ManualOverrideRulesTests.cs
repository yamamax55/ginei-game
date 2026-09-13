using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 手動上書きの持続と中断（支援要請の承諾後）。
    ///
    /// <b>この回帰を捕まえるのが目的</b>：支援要請を承諾した艦に上書きを立てると、
    /// 立てなかったとき（＝承諾前）にはできていた<b>敗走・総退却が塞がれる</b>。
    /// 「承諾したせいで退がれなくなる」を作らないことを、承諾前と承諾後の比較で固定する。
    /// 一方で<b>直接命令の扱いは従来どおり</b>であることも同時に固定する。
    /// </summary>
    public class ManualOverrideRulesTests
    {
        /// <summary>
        /// <see cref="FleetAI"/> の判断をそのまま写した縮小版
        /// ＝「AI が操舵から手を引くか」。false なら AI が動かせる（＝退却できる）。
        /// </summary>
        private static bool AiStandsDown(ManualOverrideKind kind, bool moving, bool hasManualTarget,
                                         bool hasStandingOrder, bool routed)
        {
            if (!ManualOverrideRules.IsOverriding(kind)) return false;
            if (ManualOverrideRules.ShouldReleaseForEmergency(kind, routed, false)) return false;
            return !ManualOverrideRules.IsOrderComplete(moving, hasManualTarget, hasStandingOrder);
        }

        // ===== 回帰：承諾したせいで退がれなくなっていないか =====

        /// <summary>
        /// ★本命の回帰テスト。総退却の対象になれるかが、
        /// <b>承諾前（上書きなし）と承諾後（支援要請）で同じ</b>であること。
        /// ここが食い違うと「支援を引き受けた艦だけ総退却から取り残される」。
        /// </summary>
        [Test]
        public void SupportOrder_DoesNotChangeCorpsRetreatEligibility()
        {
            bool beforeAccepting = ManualOverrideRules.CanOrderRetreat(ManualOverrideKind.なし);
            bool afterAccepting = ManualOverrideRules.CanOrderRetreat(ManualOverrideKind.支援要請);

            Assert.IsTrue(beforeAccepting, "AI 操舵中は従来どおり総退却の対象");
            Assert.AreEqual(beforeAccepting, afterAccepting,
                "支援要請を承諾したせいで総退却から外れている（回帰）");
        }

        /// <summary>
        /// ★敗走中は、支援の命令を実行中でも AI が操舵を取り戻せること
        /// （＝退がれる）。承諾前と同じ結果になる。
        /// </summary>
        [Test]
        public void SupportOrder_RoutedFleetCanStillRetreat()
        {
            // 承諾前：AI が操舵している＝当然退がれる。
            Assert.IsFalse(AiStandsDown(ManualOverrideKind.なし, true, false, false, routed: true));

            // 承諾後：移動命令の実行中でも、敗走したら AI が操舵を取り戻す。
            Assert.IsFalse(AiStandsDown(ManualOverrideKind.支援要請, true, false, false, routed: true),
                "敗走しても支援の移動命令が優先されている（回帰）");

            // 攻撃を引き受けている最中でも同じ。
            Assert.IsFalse(AiStandsDown(ManualOverrideKind.支援要請, true, true, false, routed: true),
                "敗走しても支援の攻撃命令が優先されている（回帰）");
        }

        /// <summary>敗走していない平時は、引き受けた命令が守られる（本来の目的）。</summary>
        [Test]
        public void SupportOrder_IsRespectedWhileNotInEmergency()
        {
            Assert.IsTrue(AiStandsDown(ManualOverrideKind.支援要請, true, false, false, routed: false),
                "平時に AI が行き先を上書きしてしまう");
            Assert.IsTrue(AiStandsDown(ManualOverrideKind.支援要請, false, true, false, routed: false),
                "攻撃目標を追尾している最中に AI が割り込んでいる");
        }

        // ===== 直接命令の扱いは変えていないこと =====

        /// <summary>★直接命令は従来どおり＝緊急でも中断しない・総退却の対象外。</summary>
        [Test]
        public void DirectOrder_KeepsLegacyBehavior()
        {
            Assert.IsFalse(ManualOverrideRules.CanOrderRetreat(ManualOverrideKind.直接命令),
                "直接命令が総退却に巻き込まれるようになっている（既存仕様の変更）");
            Assert.IsFalse(ManualOverrideRules.ShouldReleaseForEmergency(
                ManualOverrideKind.直接命令, routed: true, corpsRetreatOrdered: true),
                "直接命令が緊急で中断されるようになっている（既存仕様の変更）");
            Assert.IsTrue(AiStandsDown(ManualOverrideKind.直接命令, true, false, false, routed: true),
                "敗走で直接命令が破棄されるようになっている（既存仕様の変更）");
        }

        // ===== 中断の条件 =====

        [Test]
        public void Release_OnlyForSupportOrderAndOnlyInEmergency()
        {
            // 支援要請：敗走でも総退却でも中断する。
            Assert.IsTrue(ManualOverrideRules.ShouldReleaseForEmergency(
                ManualOverrideKind.支援要請, routed: true, corpsRetreatOrdered: false));
            Assert.IsTrue(ManualOverrideRules.ShouldReleaseForEmergency(
                ManualOverrideKind.支援要請, routed: false, corpsRetreatOrdered: true));

            // 緊急でなければ中断しない（勝手に投げ出さない）。
            Assert.IsFalse(ManualOverrideRules.ShouldReleaseForEmergency(
                ManualOverrideKind.支援要請, routed: false, corpsRetreatOrdered: false));

            // 上書きしていない艦には関係ない。
            Assert.IsFalse(ManualOverrideRules.ShouldReleaseForEmergency(
                ManualOverrideKind.なし, routed: true, corpsRetreatOrdered: true));
        }

        // ===== 完了で AI へ復帰すること =====

        /// <summary>
        /// ★攻撃の対象が消えたら（<c>HasManualTarget</c> が落ちたら）命令は完了扱いになり、
        /// AI へ復帰すること＝要請を引き受けた艦が固まらない。
        /// </summary>
        [Test]
        public void AttackOrder_CompletesWhenTargetIsGone()
        {
            // 追尾中：まだ終わっていない。
            Assert.IsFalse(ManualOverrideRules.IsOrderComplete(
                isMoving: true, hasManualTarget: true, hasStandingOrder: false));

            // 目標が沈んで手動標的が落ち、移動も止まった＝完了。
            Assert.IsTrue(ManualOverrideRules.IsOrderComplete(
                isMoving: false, hasManualTarget: false, hasStandingOrder: false));

            // 完了すれば AI が操舵を取り戻す。
            Assert.IsFalse(AiStandsDown(ManualOverrideKind.支援要請, false, false, false, routed: false),
                "命令が終わったのに AI へ戻っていない");
        }

        /// <summary>移動が終わっても、標準命令が残っていれば完了ではない。</summary>
        [Test]
        public void OrderComplete_RequiresEverythingFinished()
        {
            Assert.IsFalse(ManualOverrideRules.IsOrderComplete(true, false, false), "移動中");
            Assert.IsFalse(ManualOverrideRules.IsOrderComplete(false, true, false), "手動標的あり");
            Assert.IsFalse(ManualOverrideRules.IsOrderComplete(false, false, true), "標準命令あり");
            Assert.IsTrue(ManualOverrideRules.IsOrderComplete(false, false, false));
        }

        // ===== 種別の基本 =====

        [Test]
        public void Overriding_IsFalseOnlyForNone()
        {
            Assert.IsFalse(ManualOverrideRules.IsOverriding(ManualOverrideKind.なし));
            Assert.IsTrue(ManualOverrideRules.IsOverriding(ManualOverrideKind.直接命令));
            Assert.IsTrue(ManualOverrideRules.IsOverriding(ManualOverrideKind.支援要請));
        }

        [Test]
        public void InterruptedText_SaysWhoAndWhy()
        {
            string s = ManualOverrideRules.InterruptedText("第7艦隊", "敗走");
            StringAssert.Contains("第7艦隊", s);
            StringAssert.Contains("敗走", s);
            StringAssert.Contains("中断", s);
            Assert.IsFalse(string.IsNullOrEmpty(ManualOverrideRules.InterruptedText("", "")));
        }
    }
}
