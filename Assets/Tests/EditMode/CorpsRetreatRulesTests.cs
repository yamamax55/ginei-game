using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class CorpsRetreatRulesTests
    {
        const float Tol = 1e-4f;

        // ── 撤退判断 ──

        [Test]
        public void 凡将は基準どおりの撤退閾値()
        {
            // 統率50・功名心50 → 基準 0.35 のまま
            Assert.AreEqual(0.35f, CorpsRetreatRules.RetreatThreshold(50, 50), Tol);
        }

        [Test]
        public void 名将は早めに退き猪武者は粘る()
        {
            float able = CorpsRetreatRules.RetreatThreshold(100, 0);   // 統率高・功名心低＝早く退く
            float reckless = CorpsRetreatRules.RetreatThreshold(0, 100); // 統率低・功名心高＝粘る
            Assert.Greater(able, 0.35f);
            Assert.Less(reckless, 0.35f);
            Assert.Greater(able, reckless);
        }

        [Test]
        public void 兵力比が閾値を下回るか敗走で総退却を命じる()
        {
            // 凡将（閾値0.35）：兵力比0.3 → 退く
            Assert.IsTrue(CorpsRetreatRules.ShouldOrderRetreat(0.3f, false, 50, 50));
            // 兵力比0.5 → まだ退かない
            Assert.IsFalse(CorpsRetreatRules.ShouldOrderRetreat(0.5f, false, 50, 50));
            // 敗走していれば兵力比が高くても退く
            Assert.IsTrue(CorpsRetreatRules.ShouldOrderRetreat(0.9f, true, 50, 50));
        }

        // ── 敗北の度合い ──

        [Test]
        public void 完敗ほど敗北の度合いが大きい()
        {
            // 敗者元1000・残100（9割喪失）・勝者900 → 高 severity
            float crushing = CorpsRetreatRules.DefeatSeverity(100, 1000, 900);
            // 敗者元1000・残800（2割喪失）・勝者900 → 低 severity
            float narrow = CorpsRetreatRules.DefeatSeverity(800, 1000, 900);
            Assert.Greater(crushing, narrow);
            Assert.That(crushing, Is.InRange(0f, 1f));
            Assert.That(narrow, Is.InRange(0f, 1f));
        }

        // ── 士気・練度・兵力の代償 ──

        [Test]
        public void 完敗ほど士気を大きく失う()
        {
            // 最大士気100・severity1.0・既定割合0.35 → 35
            Assert.AreEqual(35f, CorpsRetreatRules.MoraleLossOnDefeat(100f, 1.0f), Tol);
            // severity0.5 → 17.5
            Assert.AreEqual(17.5f, CorpsRetreatRules.MoraleLossOnDefeat(100f, 0.5f), Tol);
        }

        [Test]
        public void 敗北で練度が希釈される()
        {
            // xp50・severity1.0・割合0.2 → 50*(1-0.2)=40
            Assert.AreEqual(40f, CorpsRetreatRules.VeterancyAfterDefeat(50f, 1.0f), Tol);
            // severity0 → 不変
            Assert.AreEqual(50f, CorpsRetreatRules.VeterancyAfterDefeat(50f, 0f), Tol);
        }

        [Test]
        public void 撤退で兵力が追加で目減りするが全滅はしない()
        {
            // 生存1000・severity1.0・割合0.1 → 900
            Assert.AreEqual(900, CorpsRetreatRules.SurvivorsAfterWithdrawal(1000, 1.0f));
            // 生存が極少でも最低1（原隊へ帰す）
            Assert.AreEqual(1, CorpsRetreatRules.SurvivorsAfterWithdrawal(0, 1.0f));
        }

        // ── 声望の代償 ──

        [Test]
        public void 完敗ほど声望が失墜する()
        {
            // 声望0.8・severity1.0 → ReputationDecay(0.8, 1.0) = 0.8*(1-1.0*0.4)=0.48
            Assert.AreEqual(0.48f, CorpsRetreatRules.ReputationAfterDefeat(0.8f, 1.0f), Tol);
            // severity0 なら不変
            Assert.AreEqual(0.8f, CorpsRetreatRules.ReputationAfterDefeat(0.8f, 0f), Tol);
        }
    }
}
