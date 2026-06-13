using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 農奴制と解放を固定する：身分制労働は安定だが上限が低い、解放はJ字カーブ
    /// （直後は農奴制を下回り→数年で追い越し→長期は大きく上回る）、ショックは身分制の深さに比例、
    /// 領主反発は補償で和らぐ、恩義は日常化で薄れる、流動性は解放規模に比例。境界を担保。
    /// </summary>
    public class SerfdomRulesTests
    {
        private static readonly SerfdomParams P = SerfdomParams.Default;
        // 農奴産出0.6/自由上限1.2/ショック0.5/回復10年/追従遅れ2年/反発0.8/恩義0.5/風化30年/流動性0.4

        [Test]
        public void SerfProductivity_StableButLowCeiling()
        {
            // 安定＝常に同じ値・上限低い＝自由労働の長期上限を下回る
            Assert.AreEqual(0.6f, SerfdomRules.SerfProductivity(P), 1e-5f);
            Assert.Less(SerfdomRules.SerfProductivity(P), P.freeProductivityCap);
        }

        [Test]
        public void EmancipationTarget_JCurve_DipsThenOvertakes()
        {
            // 直後＝谷：0.6×(1−0.5)=0.3 ＜ 農奴水準0.6（短期の損）
            Assert.AreEqual(0.3f, SerfdomRules.EmancipationTarget(0f, P), 1e-5f);
            Assert.Less(SerfdomRules.EmancipationTarget(0f, P), SerfdomRules.SerfProductivity(P));
            // 中間：Lerp(0.3, 1.2, 0.5)=0.75
            Assert.AreEqual(0.75f, SerfdomRules.EmancipationTarget(5f, P), 1e-5f);
            // 回復後＝上限1.2 ＞ 農奴水準（長期の得）。以後は頭打ち
            Assert.AreEqual(1.2f, SerfdomRules.EmancipationTarget(10f, P), 1e-5f);
            Assert.AreEqual(1.2f, SerfdomRules.EmancipationTarget(50f, P), 1e-5f);
        }

        [Test]
        public void FreeLaborProductivityTick_FollowsJCurveWithLag()
        {
            // 解放直後：農奴水準0.6から谷0.3へ、遅れ2年×dt1＝半分寄る → 0.45（農奴制を下回る混乱）
            float shocked = SerfdomRules.FreeLaborProductivityTick(0.6f, 0f, 1f, P);
            Assert.AreEqual(0.45f, shocked, 1e-5f);
            Assert.Less(shocked, SerfdomRules.SerfProductivity(P));
            // 長期：dt≥遅れで目標へ到達＝上限1.2（農奴制を大きく上回る）
            float matured = SerfdomRules.FreeLaborProductivityTick(shocked, 10f, 2f, P);
            Assert.AreEqual(1.2f, matured, 1e-5f);
            Assert.Greater(matured, SerfdomRules.SerfProductivity(P));
        }

        [Test]
        public void CrossoverYears_ReformerSowsBeforeHarvest()
        {
            // 目標が農奴水準0.6を超えるのは 10×(0.6−0.3)/(1.2−0.3)=10/3年後＝果実は数年待ち
            Assert.AreEqual(10f / 3f, SerfdomRules.CrossoverYears(P), 1e-4f);
            // ショックなし＝谷がない社会は即時に上回る
            var noShock = new SerfdomParams(0.6f, 1.2f, 0f, 10f, 2f, 0.8f, 0.5f, 30f, 0.4f);
            Assert.AreEqual(0f, SerfdomRules.CrossoverYears(noShock), 1e-5f);
        }

        [Test]
        public void EmancipationShock_DeeperSerfdomBiggerShock()
        {
            Assert.AreEqual(0.5f, SerfdomRules.EmancipationShock(1f, P), 1e-5f);   // 全人口農奴＝最大
            Assert.AreEqual(0.2f, SerfdomRules.EmancipationShock(0.4f, P), 1e-5f); // 比例
            Assert.AreEqual(0f, SerfdomRules.EmancipationShock(0f, P), 1e-5f);     // 農奴なし＝無風
            Assert.AreEqual(0.5f, SerfdomRules.EmancipationShock(2f, P), 1e-5f);   // 入力クランプ
        }

        [Test]
        public void LandlordBacklash_SoftenedByCompensation()
        {
            Assert.AreEqual(0.8f, SerfdomRules.LandlordBacklash(1f, 0f, P), 1e-5f); // 無補償＝最大反発
            Assert.AreEqual(0f, SerfdomRules.LandlordBacklash(1f, 1f, P), 1e-5f);   // 全額補償＝牙を抜く
            Assert.AreEqual(0.2f, SerfdomRules.LandlordBacklash(0.5f, 0.5f, P), 1e-5f); // 0.8×0.5×0.5
        }

        [Test]
        public void FreedomLoyalty_FadesAsFreedomBecomesNormal()
        {
            Assert.AreEqual(0.5f, SerfdomRules.FreedomLoyalty(0f, P), 1e-5f);   // 解放直後＝恩義最大
            Assert.AreEqual(0.25f, SerfdomRules.FreedomLoyalty(15f, P), 1e-5f); // 半世代で半減
            Assert.AreEqual(0f, SerfdomRules.FreedomLoyalty(30f, P), 1e-5f);    // 日常化＝消える
            Assert.AreEqual(0f, SerfdomRules.FreedomLoyalty(60f, P), 1e-5f);    // 以後もゼロのまま
        }

        [Test]
        public void MobilityGain_ScalesWithFreedShare()
        {
            Assert.AreEqual(0.4f, SerfdomRules.MobilityGain(1f, P), 1e-5f);   // 全解放＝最大流動性
            Assert.AreEqual(0.2f, SerfdomRules.MobilityGain(0.5f, P), 1e-5f); // 比例
            Assert.AreEqual(0f, SerfdomRules.MobilityGain(0f, P), 1e-5f);     // 解放なし＝得られない
        }
    }
}
