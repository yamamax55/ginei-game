using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 長距離打撃（アウトレンジ戦法）：StandoffStrikeRules の純ロジックテスト。
    /// 射程優位帯・遠距離命中減衰・一方的損害・接近猶予・カイティング・正味価値・可否を既定 Params で固定。
    /// </summary>
    public class StandoffStrikeRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void RangeAdvantage_OwnLonger_ReturnsPositiveDiff()
        {
            // 自120−敵80=40（こちらが長射程）
            Assert.AreEqual(40f, StandoffStrikeRules.RangeAdvantage(120f, 80f), Eps);
            // 自60−敵100=-40（相手が長射程＝不利）
            Assert.AreEqual(-40f, StandoffStrikeRules.RangeAdvantage(60f, 100f), Eps);
        }

        [Test]
        public void StandoffZone_ScalesAdvantageByFraction()
        {
            // 優位40×0.8=32（撃てる帯）。優位が無ければ0。
            Assert.AreEqual(32f, StandoffStrikeRules.StandoffZone(40f), Eps);
            Assert.AreEqual(0f, StandoffStrikeRules.StandoffZone(-10f), Eps);
        }

        [Test]
        public void LongRangeAccuracy_FallsOffWithRangeAndFireControl()
        {
            // 距離100：base=1-0.005*100=0.5。FC=100→倍率1.0→0.5。
            Assert.AreEqual(0.5f, StandoffStrikeRules.LongRangeAccuracy(100f, 100f), Eps);
            // FC=0→倍率0.5→0.25（低い射撃管制で命中半減）。
            Assert.AreEqual(0.25f, StandoffStrikeRules.LongRangeAccuracy(100f, 0f), Eps);
        }

        [Test]
        public void OneSidedDamage_ZeroWhenNoZone()
        {
            // 撃てる帯=32：100×0.5×(32/33)=48.48485。
            Assert.AreEqual(48.48485f, StandoffStrikeRules.OneSidedDamage(32f, 0.5f, 100f), 1e-3f);
            // 帯が無い（=0）なら一方的に撃てない＝0。
            Assert.AreEqual(0f, StandoffStrikeRules.OneSidedDamage(0f, 0.5f, 100f), Eps);
        }

        [Test]
        public void ClosingThreat_GraceIsZoneOverSpeed()
        {
            // 帯32／接近速度8=4（猶予4）。
            Assert.AreEqual(4f, StandoffStrikeRules.ClosingThreat(8f, 32f), Eps);
            // 接近速度0なら詰められない＝極大の猶予。
            Assert.IsTrue(StandoffStrikeRules.ClosingThreat(0f, 32f) >= float.MaxValue);
        }

        [Test]
        public void KitingEffectiveness_HalfWhenEqual_FullWhenFaster()
        {
            Assert.AreEqual(0.5f, StandoffStrikeRules.KitingEffectiveness(50f, 50f), Eps);   // 拮抗
            Assert.AreEqual(1f, StandoffStrikeRules.KitingEffectiveness(100f, 0f), Eps);     // 自分が速い→保てる
            Assert.AreEqual(0f, StandoffStrikeRules.KitingEffectiveness(0f, 100f), Eps);     // 自分が遅い→詰められる
        }

        [Test]
        public void RangeSupremacyValue_DiscountsByClosingGrace()
        {
            // 損害48.48485×(猶予4/(4+1))=48.48485×0.8=38.78788。
            Assert.AreEqual(38.78788f, StandoffStrikeRules.RangeSupremacyValue(48.48485f, 4f), 1e-3f);
            // 詰められない（極大猶予）なら割引なし＝損害そのまま。
            Assert.AreEqual(48.48485f, StandoffStrikeRules.RangeSupremacyValue(48.48485f, float.MaxValue), 1e-3f);
        }

        [Test]
        public void CanOutrange_TrueAboveThreshold()
        {
            Assert.IsTrue(StandoffStrikeRules.CanOutrange(40f, 5f));   // 優位40>5
            Assert.IsFalse(StandoffStrikeRules.CanOutrange(3f, 5f));   // 優位3<=5
        }

        [Test]
        public void Story_OutrangeAdvantageEvaporatesWhenEnemyCloses()
        {
            // 物語：自射程120・敵80でアウトレンジ成立、敵の射程外から一方的に叩く。
            float adv = StandoffStrikeRules.RangeAdvantage(120f, 80f); // 40
            Assert.IsTrue(StandoffStrikeRules.CanOutrange(adv, StandoffStrikeParams.Default.outrangeThreshold));

            float zone = StandoffStrikeRules.StandoffZone(adv); // 32
            float acc = StandoffStrikeRules.LongRangeAccuracy(88f, 80f); // 0.56*0.9=0.504
            Assert.AreEqual(0.504f, acc, Eps);

            float dmg = StandoffStrikeRules.OneSidedDamage(zone, acc, 100f); // 100*0.504*(32/33)=48.872727
            Assert.AreEqual(48.872727f, dmg, 1e-3f);
            Assert.Greater(dmg, 0f); // 一方的に損害を与えられている

            // 機動が速ければ撃ちながら下がって距離を保てる（カイティング有効）。
            float kite = StandoffStrikeRules.KitingEffectiveness(80f, 50f);
            Assert.Greater(kite, 0.5f);

            // 接近脅威があると優位価値は割り引かれる（速い敵ほど価値が削られる）。
            float graceFast = StandoffStrikeRules.ClosingThreat(16f, zone); // 2
            float graceSlow = StandoffStrikeRules.ClosingThreat(4f, zone);  // 8
            float valFast = StandoffStrikeRules.RangeSupremacyValue(dmg, graceFast);
            float valSlow = StandoffStrikeRules.RangeSupremacyValue(dmg, graceSlow);
            Assert.Less(valFast, valSlow); // すぐ詰められるほど正味価値は低い
            Assert.Less(valFast, dmg);     // 接近リスクぶん損害より目減り

            // 敵が距離を詰めて射程が並ぶと優位は消える＝一方的に撃てない。
            float advClosed = StandoffStrikeRules.RangeAdvantage(80f, 80f); // 0
            Assert.IsFalse(StandoffStrikeRules.CanOutrange(advClosed, StandoffStrikeParams.Default.outrangeThreshold));
            float zoneClosed = StandoffStrikeRules.StandoffZone(advClosed); // 0
            float dmgClosed = StandoffStrikeRules.OneSidedDamage(zoneClosed, acc, 100f); // 0
            Assert.AreEqual(0f, dmgClosed, Eps); // 優位消失で一方的損害ゼロ
        }
    }
}
