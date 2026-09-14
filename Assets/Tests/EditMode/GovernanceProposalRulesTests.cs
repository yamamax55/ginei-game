using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 統治政策の上申の受付判定（#67/#109）を固定する：次の政策の巡回、カーソル距離の拾い方（I キーと同じ）、
    /// 受け付けない理由の優先順、理由の文言。Alt+T と星系情報パネルのボタンが同じ判定を使う前提。
    /// </summary>
    public class GovernanceProposalRulesTests
    {
        [Test]
        public void NextPolicy_CyclesInDeclarationOrder()
        {
            Assert.AreEqual(GovernancePolicy.動員, GovernanceProposalRules.NextPolicy(GovernancePolicy.民生));
            Assert.AreEqual(GovernancePolicy.弾圧, GovernanceProposalRules.NextPolicy(GovernancePolicy.動員));
            Assert.AreEqual(GovernancePolicy.解放, GovernanceProposalRules.NextPolicy(GovernancePolicy.弾圧));
            Assert.AreEqual(GovernancePolicy.民生, GovernanceProposalRules.NextPolicy(GovernancePolicy.解放));
            Assert.AreEqual(GovernancePolicy.動員, GovernanceProposalRules.NextPolicy((GovernancePolicy)99), "範囲外は先頭扱い");
        }

        [Test]
        public void AcceptsPointerDistance_UsesLargerOfClickRadiusAndMinimum()
        {
            // 通常の星系（当たり判定が最小より小さい）＝最小 1.2 まで
            Assert.IsTrue(GovernanceProposalRules.AcceptsPointerDistance(1.2f, 0.65f));
            Assert.IsFalse(GovernanceProposalRules.AcceptsPointerDistance(1.21f, 0.65f));
            // 要塞星系（見た目が大きい）＝旧実装の固定 1.2 では拾えなかった距離も拾う
            Assert.IsTrue(GovernanceProposalRules.AcceptsPointerDistance(1.8f, 2.0f));
            Assert.IsFalse(GovernanceProposalRules.AcceptsPointerDistance(2.01f, 2.0f));
            // 不正値
            Assert.IsFalse(GovernanceProposalRules.AcceptsPointerDistance(-0.1f, 2.0f));
            Assert.IsFalse(GovernanceProposalRules.AcceptsPointerDistance(float.NaN, 2.0f));
        }

        private static GovernanceProposalRejection Eval(
            bool found = true, Faction owner = Faction.同盟, bool province = true, bool director = true,
            bool state = true, int pending = 0, int max = 3, bool duplicate = false)
            => GovernanceProposalRules.Evaluate(found, owner, Faction.同盟, province, director, state, pending, max, duplicate);

        [Test]
        public void Evaluate_AllConditionsMet_Accepts()
        {
            Assert.AreEqual(GovernanceProposalRejection.なし, Eval());
            Assert.AreEqual(GovernanceProposalRejection.なし, Eval(pending: 2, max: 3));
        }

        [Test]
        public void Evaluate_ReportsFirstFailingCondition_InOrder()
        {
            Assert.AreEqual(GovernanceProposalRejection.星系なし, Eval(found: false, owner: Faction.帝国, director: false));
            Assert.AreEqual(GovernanceProposalRejection.管轄外, Eval(owner: Faction.帝国, province: false));
            Assert.AreEqual(GovernanceProposalRejection.内政データなし, Eval(province: false, director: false));
            Assert.AreEqual(GovernanceProposalRejection.稟議機構なし, Eval(director: false, state: false));
            Assert.AreEqual(GovernanceProposalRejection.勢力状態なし, Eval(state: false, duplicate: true));
            Assert.AreEqual(GovernanceProposalRejection.重複上申, Eval(duplicate: true, pending: 3));
            Assert.AreEqual(GovernanceProposalRejection.決裁待ち上限, Eval(pending: 3, max: 3));
            Assert.AreEqual(GovernanceProposalRejection.決裁待ち上限, Eval(pending: 0, max: 0));
        }

        [Test]
        public void RejectionText_IsEmptyOnlyWhenAccepted_AndNamesTheSystem()
        {
            Assert.AreEqual("", GovernanceProposalRules.RejectionText(GovernanceProposalRejection.なし, "ハイネセン"));
            foreach (GovernanceProposalRejection r in System.Enum.GetValues(typeof(GovernanceProposalRejection)))
            {
                if (r == GovernanceProposalRejection.なし) continue;
                Assert.IsNotEmpty(GovernanceProposalRules.RejectionText(r, "ハイネセン"), r.ToString());
                Assert.IsNotEmpty(GovernanceProposalRules.RejectionText(r, null), r + "（名前なし）");
            }
            StringAssert.Contains("ハイネセン", GovernanceProposalRules.RejectionText(GovernanceProposalRejection.管轄外, "ハイネセン"));
            StringAssert.Contains("この星系", GovernanceProposalRules.RejectionText(GovernanceProposalRejection.管轄外, ""));
        }

        [Test]
        public void Preview_CanSubmit_OnlyWhenNoRejection()
        {
            var ok = new GovernanceProposalPreview(3, "星", GovernancePolicy.民生, GovernancePolicy.動員, GovernanceProposalRejection.なし);
            var ng = new GovernanceProposalPreview(3, null, GovernancePolicy.民生, GovernancePolicy.動員, GovernanceProposalRejection.管轄外);
            Assert.IsTrue(ok.CanSubmit);
            Assert.IsFalse(ng.CanSubmit);
            Assert.AreEqual("", ng.systemName, "null 名は空文字");
        }
    }
}
