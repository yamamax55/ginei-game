using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 襲撃隊（快速部隊の一撃離脱）を固定する：到達深度＝速度×(1＋継戦)、目標打撃＝戦力×(1−防御軽減)、
    /// 成功度＝自速/(自速＋敵反応)、撹乱価値＝打撃×(1＋重要度)、帰還リスク＝深度×係数×(1＋追撃)、
    /// 後方拘束＝守備×脅威×縛り、正味価値＝撹乱×(1−リスク)、成功判定＝閾値以上。境界を担保。
    /// </summary>
    public class RaidingPartyRulesTests
    {
        private static readonly RaidingPartyParams P = RaidingPartyParams.Default;
        // 到達1.0/継戦重み0.5/深度リスク0.02/縛り0.5/成功閾値0.5

        [Test]
        public void DefaultParams_AreExpected()
        {
            Assert.AreEqual(1f, P.reachPerSpeed, 1e-4f);
            Assert.AreEqual(0.5f, P.enduranceWeight, 1e-4f);
            Assert.AreEqual(0.02f, P.riskPerDepth, 1e-4f);
            Assert.AreEqual(0.5f, P.tyingDownScale, 1e-4f);
            Assert.AreEqual(0.5f, P.successThreshold, 1e-4f);
        }

        [Test]
        public void RaidReach_SpeedAndEndurance()
        {
            // 12 * 1 * (1 + 1*0.5) = 18
            Assert.AreEqual(18f, RaidingPartyRules.RaidReach(12f, 1f), 1e-4f);
            // 継戦0なら素の到達 = 速度
            Assert.AreEqual(10f, RaidingPartyRules.RaidReach(10f, 0f), 1e-4f);
            // 負入力は0クランプ
            Assert.AreEqual(0f, RaidingPartyRules.RaidReach(-5f, 3f), 1e-4f);
        }

        [Test]
        public void TargetDamage_DefenseMitigation()
        {
            // def 50 -> 軽減0.25 -> 80*0.75 = 60
            Assert.AreEqual(60f, RaidingPartyRules.TargetDamage(80f, 50f), 1e-4f);
            // 無防備は満額
            Assert.AreEqual(100f, RaidingPartyRules.TargetDamage(100f, 0f), 1e-4f);
            // 過大防御でも最低1割は通る（軽減上限0.9）
            Assert.AreEqual(10f, RaidingPartyRules.TargetDamage(100f, 1000f), 1e-4f);
        }

        [Test]
        public void HitAndRunSuccess_SpeedRatio()
        {
            // 12/(12+4) = 0.75（敵より速い）
            Assert.AreEqual(0.75f, RaidingPartyRules.HitAndRunSuccess(12f, 4f), 1e-4f);
            // 互角は0.5
            Assert.AreEqual(0.5f, RaidingPartyRules.HitAndRunSuccess(10f, 10f), 1e-4f);
            // 追手なしは満点
            Assert.AreEqual(1f, RaidingPartyRules.HitAndRunSuccess(0f, 0f), 1e-4f);
        }

        [Test]
        public void DisruptionValue_ImportanceAmplifies()
        {
            // 60 * (1 + 1) = 120
            Assert.AreEqual(120f, RaidingPartyRules.DisruptionValue(60f, 1f), 1e-4f);
            // 重要度0なら打撃そのまま
            Assert.AreEqual(60f, RaidingPartyRules.DisruptionValue(60f, 0f), 1e-4f);
        }

        [Test]
        public void ExtractionRisk_DeeperIsRiskier()
        {
            // 10 * 0.02 * (1+1) = 0.4
            Assert.AreEqual(0.4f, RaidingPartyRules.ExtractionRisk(10f, 1f), 1e-4f);
            // 深入り＋激しい追撃は帰れない（クランプ1.0）
            Assert.AreEqual(1f, RaidingPartyRules.ExtractionRisk(24f, 2f), 1e-4f);
            // 浅い襲撃は低リスク
            Assert.AreEqual(0.1f, RaidingPartyRules.ExtractionRisk(5f, 0f), 1e-4f);
        }

        [Test]
        public void TyingDownEffect_ThreatPullsGarrison()
        {
            // 200 * 1 * 0.5 = 100
            Assert.AreEqual(100f, RaidingPartyRules.TyingDownEffect(1f, 200f), 1e-4f);
            // 脅威がなければ縛れない
            Assert.AreEqual(0f, RaidingPartyRules.TyingDownEffect(0f, 200f), 1e-4f);
        }

        [Test]
        public void RaidNetValue_DiscountedByRisk()
        {
            // 100 * (1 - 0.4) = 60
            Assert.AreEqual(60f, RaidingPartyRules.RaidNetValue(100f, 0.4f), 1e-4f);
            // 帰れない襲撃は戦果を持ち帰れない
            Assert.AreEqual(0f, RaidingPartyRules.RaidNetValue(100f, 1f), 1e-4f);
        }

        [Test]
        public void IsRaidSuccessful_Threshold()
        {
            Assert.IsTrue(RaidingPartyRules.IsRaidSuccessful(0.75f));
            Assert.IsTrue(RaidingPartyRules.IsRaidSuccessful(0.5f));   // 閾値ちょうど
            Assert.IsFalse(RaidingPartyRules.IsRaidSuccessful(0.49f));
        }

        [Test]
        public void Narrative_FastRaiderStrikesRearThenStrugglesToExtract()
        {
            // 快速の襲撃隊（速度12・継戦2）が敵後方へ深く侵入する
            float reach = RaidingPartyRules.RaidReach(12f, 2f, P);
            Assert.AreEqual(24f, reach, 1e-4f); // 12 * (1 + 2*0.5) = 24

            // 無防備に近い補給拠点（戦力80・防御50）を叩く
            float damage = RaidingPartyRules.TargetDamage(80f, 50f);
            Assert.AreEqual(60f, damage, 1e-4f);

            // 重要拠点ゆえ撹乱は大きく波及（重要度1.0）
            float disruption = RaidingPartyRules.DisruptionValue(damage, 1f);
            Assert.AreEqual(120f, disruption, 1e-4f);

            // 敵より速く一撃離脱に成功（12 対 反応4）
            float success = RaidingPartyRules.HitAndRunSuccess(12f, 4f);
            Assert.AreEqual(0.75f, success, 1e-4f);
            Assert.IsTrue(RaidingPartyRules.IsRaidSuccessful(success, P.successThreshold));

            // 襲撃の脅威で敵は後方守備に戦力を割く（守備200を半分拘束）
            float tied = RaidingPartyRules.TyingDownEffect(1f, 200f, P);
            Assert.AreEqual(100f, tied, 1e-4f);

            // だが深入りゆえ帰還は困難（追撃2でリスク飽和）
            float risk = RaidingPartyRules.ExtractionRisk(reach, 2f, P);
            Assert.AreEqual(1f, risk, 1e-4f);

            // 帰れなければ大きな撹乱も正味価値はゼロに（深入りのトレードオフ）
            float net = RaidingPartyRules.RaidNetValue(disruption, risk);
            Assert.AreEqual(0f, net, 1e-4f);

            // 浅い襲撃なら戦果を持ち帰れる（同じ撹乱・低リスク）
            float shallowRisk = RaidingPartyRules.ExtractionRisk(10f, 1f, P);
            Assert.AreEqual(0.4f, shallowRisk, 1e-4f);
            float shallowNet = RaidingPartyRules.RaidNetValue(disruption, shallowRisk);
            Assert.AreEqual(72f, shallowNet, 1e-4f); // 120 * 0.6
        }
    }
}
