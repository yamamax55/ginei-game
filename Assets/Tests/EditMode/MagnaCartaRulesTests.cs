using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// マグナカルタ（#624 王権制約）を固定する：契約で実効王権が削られ、課税は同意を要し、
    /// 王の侵害で抵抗権が発動し、慣習法化度は再確認/破棄で揺れる。境界・クランプ・各分岐・決定論。
    /// </summary>
    public class MagnaCartaRulesTests
    {
        private static MagnaCartaParams P => MagnaCartaParams.Default;

        // --- EffectiveRoyalAuthority ---

        [Test]
        public void EffectiveRoyalAuthority_NoCharter_ReturnsBase()
        {
            // 契約が無ければ王権は無制約
            Assert.AreEqual(100f, MagnaCartaRules.EffectiveRoyalAuthority(100f, null, P), 1e-4f);
        }

        [Test]
        public void EffectiveRoyalAuthority_StrengthZero_NoReduction()
        {
            // 条項が全部立っても紙の上だけ（strength=0）なら王権は削れない
            var c = new Charter(taxConsent: true, dueProcess: true, resistanceRight: true, strength: 0f);
            Assert.AreEqual(100f, MagnaCartaRules.EffectiveRoyalAuthority(100f, c, P), 1e-4f);
        }

        [Test]
        public void EffectiveRoyalAuthority_FullyHabituated_ReducedByConstraints()
        {
            // 全条項×定着＝制約 0.75 ぶん削れる → 100*(1-0.75)=25
            var c = new Charter(taxConsent: true, dueProcess: true, resistanceRight: true, strength: 1f);
            Assert.AreEqual(25f, MagnaCartaRules.EffectiveRoyalAuthority(100f, c, P), 1e-4f);
        }

        [Test]
        public void EffectiveRoyalAuthority_ClampedToBaseRange()
        {
            // 制約合計が 1 を超えても王権は 0 未満にならない（クランプ）
            var big = new MagnaCartaParams(0.6f, 0.6f, 0.6f, 0.5f, 0.05f, 0.08f);
            var c = new Charter(true, true, true, strength: 1f);
            float v = MagnaCartaRules.EffectiveRoyalAuthority(100f, c, big);
            Assert.AreEqual(0f, v, 1e-4f);
        }

        // --- TaxRequiresConsent ---

        [Test]
        public void TaxRequiresConsent_ClauseAndHabituated_True()
        {
            var c = new Charter(taxConsent: true, dueProcess: false, resistanceRight: false, strength: 0.5f);
            Assert.IsTrue(MagnaCartaRules.TaxRequiresConsent(c, P)); // 閾値ちょうどで成立
        }

        [Test]
        public void TaxRequiresConsent_ClauseButUnhabituated_False()
        {
            // 条項はあるが定着不足（閾値未満）＝王は同意なしに課税を強行できる
            var c = new Charter(taxConsent: true, dueProcess: false, resistanceRight: false, strength: 0.4f);
            Assert.IsFalse(MagnaCartaRules.TaxRequiresConsent(c, P));
        }

        [Test]
        public void TaxRequiresConsent_NoClause_False()
        {
            var c = new Charter(taxConsent: false, dueProcess: true, resistanceRight: true, strength: 1f);
            Assert.IsFalse(MagnaCartaRules.TaxRequiresConsent(c, P));
            Assert.IsFalse(MagnaCartaRules.TaxRequiresConsent(null)); // null も false
        }

        // --- ResistanceTriggered ---

        [Test]
        public void ResistanceTriggered_ViolationAndHabituatedClause_True()
        {
            var c = new Charter(taxConsent: false, dueProcess: false, resistanceRight: true, strength: 0.8f);
            Assert.IsTrue(MagnaCartaRules.ResistanceTriggered(c, kingViolation: true, P));
        }

        [Test]
        public void ResistanceTriggered_NoViolation_False()
        {
            // 王が契約を守っている間は発動しない
            var c = new Charter(false, false, resistanceRight: true, strength: 1f);
            Assert.IsFalse(MagnaCartaRules.ResistanceTriggered(c, kingViolation: false, P));
        }

        [Test]
        public void ResistanceTriggered_NoClauseOrUnhabituated_False()
        {
            var noClause = new Charter(true, true, resistanceRight: false, strength: 1f);
            Assert.IsFalse(MagnaCartaRules.ResistanceTriggered(noClause, true, P));
            var weak = new Charter(false, false, resistanceRight: true, strength: 0.3f);
            Assert.IsFalse(MagnaCartaRules.ResistanceTriggered(weak, true, P)); // 定着不足
        }

        // --- HabituationDrift ---

        [Test]
        public void HabituationDrift_Upheld_Increases()
        {
            float v = MagnaCartaRules.HabituationDrift(0.5f, upheld: true, deltaTime: 1f, P);
            Assert.AreEqual(0.55f, v, 1e-4f);
        }

        [Test]
        public void HabituationDrift_Broken_Decreases()
        {
            float v = MagnaCartaRules.HabituationDrift(0.5f, upheld: false, deltaTime: 1f, P);
            Assert.AreEqual(0.42f, v, 1e-4f); // 破棄は剥がれが速い
        }

        [Test]
        public void HabituationDrift_ClampedAndZeroDt()
        {
            // 上限/下限へクランプ
            Assert.AreEqual(1f, MagnaCartaRules.HabituationDrift(0.99f, true, 5f, P), 1e-4f);
            Assert.AreEqual(0f, MagnaCartaRules.HabituationDrift(0.01f, false, 5f, P), 1e-4f);
            // dt<=0 は変化なし（決定論・既存値クランプのみ）
            Assert.AreEqual(0.5f, MagnaCartaRules.HabituationDrift(0.5f, true, 0f, P), 1e-4f);
        }
    }
}
