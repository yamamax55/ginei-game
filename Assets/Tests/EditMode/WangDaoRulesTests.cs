using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 王道/覇道＝統治スタイルの評判メタ層（孟子・#1059）を固定する：仁政/武断の積分で道がドリフトし、
    /// 王道は心服＝低コストで安定・帰順を集め徳が正統性を生む／覇道は力服＝高コストで離反含み・時間で
    /// 恨みが積もる。即効だが持続しない武力（CoerciveEfficiency）も担保。
    /// </summary>
    public class WangDaoRulesTests
    {
        private static readonly WangDaoParams P = WangDaoParams.Default; // drift0.1 / 王道0.2 / 覇道0.8 / 恨み0.1 / 帰順1.0

        [Test]
        public void DriftTick_BenevolenceTowardKingly_CoercionTowardHegemon()
        {
            // 仁政は王道(+)へ寄る。
            float towardKingly = WangDaoRules.DriftTick(0f, benevolentActs: 1f, coerciveActs: 0f, dt: 1f, P);
            Assert.AreEqual(0.1f, towardKingly, 1e-4f); // (1-0)*0.1*1

            // 武断は覇道(−)へ寄る。
            float towardHegemon = WangDaoRules.DriftTick(0f, benevolentActs: 0f, coerciveActs: 1f, dt: 1f, P);
            Assert.AreEqual(-0.1f, towardHegemon, 1e-4f);

            // クランプ：王道の天井+1を超えない。
            float capped = WangDaoRules.DriftTick(0.95f, 1f, 0f, 1f, P);
            Assert.AreEqual(1f, capped, 1e-4f);
        }

        [Test]
        public void SubmissionQuality_Kingly_Cheaper_Than_Hegemon()
        {
            // 王道(+1)：兵力ありでも低コストで安定（心服）。
            float kinglyCost = WangDaoRules.SubmissionQuality(1f, militaryPower: 1f, P);
            Assert.AreEqual(0.2f, kinglyCost, 1e-4f); // kingly=1 → baseCost=0.2, forceCost*0=0

            // 覇道(−1)＋全力の兵力：高コスト（力服は高くつく）。
            float hegemonCost = WangDaoRules.SubmissionQuality(-1f, militaryPower: 1f, P);
            // baseCost=0.8, forceCost=1*1*0.8=0.8, *(1-kingly)=*1 → 0.8+0.8=1.6 → clamp 1
            Assert.AreEqual(1f, hegemonCost, 1e-4f);
            Assert.Greater(hegemonCost, kinglyCost); // 覇道のほうが維持コストが高い
        }

        [Test]
        public void RebellionPressure_Hegemon_Grows_Kingly_Pacifies()
        {
            // 覇道支配は占領継続で恨みが積もる。
            float hegemonPressure = WangDaoRules.RebellionPressureFromDao(-1f, occupationDuration: 1f, P);
            Assert.AreEqual(0.1f, hegemonPressure, 1e-4f); // 1*1*0.1

            // 王道支配はむしろ反乱圧ゼロ（懐く）。
            float kinglyPressure = WangDaoRules.RebellionPressureFromDao(1f, occupationDuration: 1f, P);
            Assert.AreEqual(0f, kinglyPressure, 1e-4f);
        }

        [Test]
        public void AllyAttraction_OnlyKingly_DrawsAllies()
        {
            // 王道は戦わずして諸侯を集める。
            Assert.AreEqual(0.6f, WangDaoRules.AllyAttraction(0.6f, P), 1e-4f);
            // 覇道は帰順を生まない。
            Assert.AreEqual(0f, WangDaoRules.AllyAttraction(-0.6f, P), 1e-4f);
        }

        [Test]
        public void CoerciveEfficiency_Hegemon_Immediate_KinglyZero()
        {
            // 覇道は武力比例で即効（短期効率）。
            Assert.AreEqual(0.8f, WangDaoRules.CoerciveEfficiency(-1f, militaryPower: 0.8f, P), 1e-4f);
            // 王道は力で押さえる効率を持たない。
            Assert.AreEqual(0f, WangDaoRules.CoerciveEfficiency(1f, militaryPower: 1f, P), 1e-4f);
        }

        [Test]
        public void LegitimacyFromVirtue_Kingly_HasMandate_Hegemon_None()
        {
            // 王道の徳は天命に適い正統性を生む（DynastyRulesへ）。
            Assert.AreEqual(0.7f, WangDaoRules.LegitimacyFromVirtue(0.7f), 1e-4f);
            // 覇道は正統性を生まない。
            Assert.AreEqual(0f, WangDaoRules.LegitimacyFromVirtue(-0.5f), 1e-4f);
        }

        [Test]
        public void Submission_And_Coercion_Are_TradeOff_Across_Dao()
        {
            // 同じ勢力でも：覇道は即効(高 CoerciveEfficiency)・しかし維持は高コスト＝持続しない。
            float hegemonNow = WangDaoRules.CoerciveEfficiency(-1f, 1f, P);   // 1.0
            float hegemonCost = WangDaoRules.SubmissionQuality(-1f, 1f, P);   // 1.0
            // 王道は即効を持たない・しかし維持は安い＝持続する。
            float kinglyNow = WangDaoRules.CoerciveEfficiency(1f, 1f, P);     // 0
            float kinglyCost = WangDaoRules.SubmissionQuality(1f, 1f, P);     // 0.2
            Assert.Greater(hegemonNow, kinglyNow);     // 覇道は短期で勝る
            Assert.Greater(hegemonCost, kinglyCost);   // が長期コストで劣る
        }
    }
}
