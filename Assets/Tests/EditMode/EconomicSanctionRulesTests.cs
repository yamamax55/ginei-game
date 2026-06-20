using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 経済制裁を固定する：対象分野×深さで範囲を決め、貿易依存で打撃が出る。制裁包囲が固まるほど抜け穴が塞がり
    /// 実効効果が残る。制裁する側は返り血を浴び、対象は時間で適応して政治圧力が薄まる。境界とパイプライン物語を担保。
    /// </summary>
    public class EconomicSanctionRulesTests
    {
        private static readonly EconomicSanctionParams P = EconomicSanctionParams.Default;
        // 既定＝分野比重1.0/包囲1.0/抜け穴0.8/返り血1.0/適応上限0.8

        [Test]
        public void SanctionScope_SectorsTimesDepth()
        {
            // 0.6×0.5×1.0=0.3
            Assert.AreEqual(0.3f, EconomicSanctionRules.SanctionScope(0.6f, 0.5f, P), 1e-4f);
            // 全分野×全深＝1
            Assert.AreEqual(1f, EconomicSanctionRules.SanctionScope(1f, 1f, P), 1e-4f);
            // 分野ゼロ＝範囲ゼロ
            Assert.AreEqual(0f, EconomicSanctionRules.SanctionScope(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void TargetDamage_ScopeTimesDependency()
        {
            // 0.3×0.8=0.24
            Assert.AreEqual(0.24f, EconomicSanctionRules.TargetDamage(0.3f, 0.8f, P), 1e-4f);
            // 貿易非依存＝打撃なし
            Assert.AreEqual(0f, EconomicSanctionRules.TargetDamage(0.5f, 0f, P), 1e-4f);
        }

        [Test]
        public void CoalitionParticipation_StatesTimesSupport()
        {
            // 0.5×0.6×1.0=0.3
            Assert.AreEqual(0.3f, EconomicSanctionRules.CoalitionParticipation(0.5f, 0.6f, P), 1e-4f);
            // 全参加×全支持＝1
            Assert.AreEqual(1f, EconomicSanctionRules.CoalitionParticipation(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void Loopholes_GapTimesThirdParty()
        {
            // (1-0.3)×0.5×0.8=0.28
            Assert.AreEqual(0.28f, EconomicSanctionRules.Loopholes(0.3f, 0.5f, P), 1e-4f);
            // 完全包囲＝抜け穴ゼロ
            Assert.AreEqual(0f, EconomicSanctionRules.Loopholes(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void EffectiveDamage_LeaksThroughLoopholes()
        {
            // 0.24×(1-0.28)=0.1728
            Assert.AreEqual(0.1728f, EconomicSanctionRules.EffectiveDamage(0.24f, 0.28f, P), 1e-4f);
            // 抜け穴全開＝効果消滅
            Assert.AreEqual(0f, EconomicSanctionRules.EffectiveDamage(0.5f, 1f, P), 1e-4f);
        }

        [Test]
        public void SenderCost_OwnTradeTimesScope()
        {
            // 0.4×0.3×1.0=0.12
            Assert.AreEqual(0.12f, EconomicSanctionRules.SenderCost(0.4f, 0.3f, P), 1e-4f);
            // 対象国と商っていなければ返り血なし
            Assert.AreEqual(0f, EconomicSanctionRules.SenderCost(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void TargetAdaptation_GrowsWithTimeUnderCap()
        {
            // sqrt(0.25)=0.5 → 0.5×0.5×0.8=0.2（Sqrt箇所＝許容1e-3）
            Assert.AreEqual(0.2f, EconomicSanctionRules.TargetAdaptation(0.5f, 0.25f, P), 1e-3f);
            // 開始直後（時間0）は適応なし
            Assert.AreEqual(0f, EconomicSanctionRules.TargetAdaptation(1f, 0f, P), 1e-3f);
            // 最大耐性×全期間でも上限0.8で頭打ち（sqrt(1)=1 → 1×1×0.8）
            Assert.AreEqual(0.8f, EconomicSanctionRules.TargetAdaptation(1f, 1f, P), 1e-3f);
        }

        [Test]
        public void PoliticalEffect_FadesWithAdaptation()
        {
            // 0.1728×(1-0.2)=0.13824
            Assert.AreEqual(0.13824f, EconomicSanctionRules.PoliticalEffect(0.1728f, 0.2f, P), 1e-4f);
            // 完全適応＝政治圧力消滅
            Assert.AreEqual(0f, EconomicSanctionRules.PoliticalEffect(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void ClampsInputs()
        {
            // 範囲外入力でも 0..1 に収まる
            Assert.AreEqual(1f, EconomicSanctionRules.SanctionScope(5f, 5f, P), 1e-4f);
            Assert.AreEqual(0f, EconomicSanctionRules.Loopholes(2f, 2f, P), 1e-4f); // 包囲2→clamp1→gap0
            Assert.AreEqual(0f, EconomicSanctionRules.PoliticalEffect(-3f, -3f, P), 1e-4f);
        }

        [Test]
        public void Narrative_TightCoalitionDeliversPressureLooseOneLeaks()
        {
            // 物語：広く深い制裁を貿易依存の対象に課す。範囲0.8×0.75=0.6、打撃0.6×0.9=0.54。
            float scope = EconomicSanctionRules.SanctionScope(0.8f, 0.75f, P);
            Assert.AreEqual(0.6f, scope, 1e-4f);
            float damage = EconomicSanctionRules.TargetDamage(scope, 0.9f, P);
            Assert.AreEqual(0.54f, damage, 1e-4f);

            // 固い包囲（多国×高支持）→抜け穴が塞がり実効効果が高く残る
            float tight = EconomicSanctionRules.CoalitionParticipation(0.9f, 0.9f, P); // 0.81
            float tightLeak = EconomicSanctionRules.Loopholes(tight, 0.8f, P);          // (1-0.81)*0.8*0.8=0.1216
            float tightEff = EconomicSanctionRules.EffectiveDamage(damage, tightLeak, P);

            // 緩い包囲（少国×低支持）→抜け穴が広く効果が漏れる
            float loose = EconomicSanctionRules.CoalitionParticipation(0.3f, 0.3f, P);  // 0.09
            float looseLeak = EconomicSanctionRules.Loopholes(loose, 0.8f, P);          // (1-0.09)*0.8*0.8=0.5824
            float looseEff = EconomicSanctionRules.EffectiveDamage(damage, looseLeak, P);

            Assert.Greater(tightEff, looseEff); // 包囲が固いほど効く

            // 時間で対象が適応すると、同じ実効効果でも政治圧力は薄まる
            float early = EconomicSanctionRules.TargetAdaptation(0.6f, 0.1f, P);
            float late = EconomicSanctionRules.TargetAdaptation(0.6f, 0.9f, P);
            Assert.Greater(late, early); // 時間とともに適応が進む

            float pressureEarly = EconomicSanctionRules.PoliticalEffect(tightEff, early, P);
            float pressureLate = EconomicSanctionRules.PoliticalEffect(tightEff, late, P);
            Assert.Greater(pressureEarly, pressureLate); // 適応が進むほど屈服を迫る力は弱まる

            // 制裁する側も返り血を浴びる（対象国貿易が太いほど痛い）
            float cost = EconomicSanctionRules.SenderCost(0.7f, scope, P); // 0.7*0.6*1=0.42
            Assert.AreEqual(0.42f, cost, 1e-4f);
        }
    }
}
