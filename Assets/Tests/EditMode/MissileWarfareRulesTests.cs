using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>ミサイル戦：斉射・迎撃・飽和・誘導妨害・有効弾・命中効果・弾薬消耗（既定Params期待値固定）。</summary>
    public class MissileWarfareRulesTests
    {
        [Test]
        public void SalvoSize_LaunchersTimesRate()
        {
            Assert.AreEqual(20f, MissileWarfareRules.SalvoSize(10f, 2f), 1e-4f);
            Assert.AreEqual(0f, MissileWarfareRules.SalvoSize(-5f, 2f), 1e-4f); // 負はClampで0
            Assert.AreEqual(0f, MissileWarfareRules.SalvoSize(10f, 0f), 1e-4f);
        }

        [Test]
        public void InterceptCapacity_GunsAndChannelsLimit()
        {
            // 既定 interceptPerChannel=4。砲12 vs チャンネル4*4=16 → 律速は砲の12。
            Assert.AreEqual(12f, MissileWarfareRules.InterceptCapacity(12f, 4f), 1e-4f);
            // 砲20 vs 16 → 律速はチャンネルの16。
            Assert.AreEqual(16f, MissileWarfareRules.InterceptCapacity(20f, 4f), 1e-4f);
            Assert.AreEqual(0f, MissileWarfareRules.InterceptCapacity(20f, 0f), 1e-4f); // チャンネル0
        }

        [Test]
        public void SaturationFactor_RatioAndZeroCapacity()
        {
            Assert.AreEqual(20f / 12f, MissileWarfareRules.SaturationFactor(20f, 12f), 1e-4f); // 1.6667
            Assert.AreEqual(MissileWarfareParams.DefaultMaxSaturation,
                MissileWarfareRules.SaturationFactor(20f, 0f), 1e-4f); // 迎撃能力0＝完全飽和
            Assert.AreEqual(0.5f, MissileWarfareRules.SaturationFactor(10f, 20f), 1e-4f); // 余裕あり
        }

        [Test]
        public void InterceptionRate_DropsWhenSaturated()
        {
            // 飽和（弾20>迎撃12）：coverage=0.6 → 0.9*0.6=0.54、decay=1+(1.6667-1)=1.6667 → 0.54/1.6667=0.324
            Assert.AreEqual(0.324f, MissileWarfareRules.InterceptionRate(12f, 20f, 20f / 12f), 1e-4f);
            // 非飽和（弾10<迎撃20）：coverage=1 → 0.9、飽和度0.5は減衰なし
            Assert.AreEqual(0.9f, MissileWarfareRules.InterceptionRate(20f, 10f, 0.5f), 1e-4f);
            // 撃って来なければ迎撃ゼロ
            Assert.AreEqual(0f, MissileWarfareRules.InterceptionRate(20f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void GuidanceJamming_EcmFraction()
        {
            Assert.AreEqual(0.6f, MissileWarfareRules.GuidanceJamming(60f, 40f), 1e-4f); // 60/100
            Assert.AreEqual(0f, MissileWarfareRules.GuidanceJamming(0f, 0f), 1e-4f);     // 両ゼロ＝振り切れず
            Assert.AreEqual(0.5f, MissileWarfareRules.GuidanceJamming(50f, 50f), 1e-4f); // 拮抗
        }

        [Test]
        public void Leakers_PassThroughInterceptAndJamming()
        {
            // 弾20、迎撃0.324、妨害0.6 → 20*(0.676)*(0.4)=5.408
            Assert.AreEqual(5.408f, MissileWarfareRules.Leakers(20f, 0.324f, 0.6f), 1e-4f);
            // 完全迎撃＝到達ゼロ
            Assert.AreEqual(0f, MissileWarfareRules.Leakers(20f, 1f, 0f), 1e-4f);
            // 無防備＝全弾到達
            Assert.AreEqual(20f, MissileWarfareRules.Leakers(20f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void MissileEffectiveness_YieldOverArmorClamped()
        {
            // 到達5.408、威力0.1、装甲1 → 5.408*0.1/2=0.2704
            Assert.AreEqual(0.2704f, MissileWarfareRules.MissileEffectiveness(5.408f, 0.1f, 1f), 1e-4f);
            // 過大入力は0..1にClamp
            Assert.AreEqual(1f, MissileWarfareRules.MissileEffectiveness(100f, 1f, 0f), 1e-4f);
            Assert.AreEqual(0f, MissileWarfareRules.MissileEffectiveness(0f, 1f, 0f), 1e-4f); // 到達なし
        }

        [Test]
        public void AmmoDepletion_SalvoOverMagazine()
        {
            Assert.AreEqual(0.2f, MissileWarfareRules.AmmoDepletion(20f, 100f), 1e-4f);
            Assert.AreEqual(1f, MissileWarfareRules.AmmoDepletion(200f, 100f), 1e-4f); // 弾薬庫超過はClamp1
            Assert.AreEqual(1f, MissileWarfareRules.AmmoDepletion(20f, 0f), 1e-4f);    // 弾薬庫なし＝即枯渇
        }

        [Test]
        public void Narrative_SaturationStrikeOverwhelmsPointDefense()
        {
            // 物語：飽和攻撃。少数の発射機での通常斉射は迎撃に弾かれるが、
            // 発射機を増やした大量斉射は迎撃能力を飽和させ、有効弾の到達が跳ね上がる。
            var p = MissileWarfareParams.Default;

            float capacity = MissileWarfareRules.InterceptCapacity(12f, 4f, p); // 12
            float jam = MissileWarfareRules.GuidanceJamming(20f, 80f, p);        // 0.2

            // 小規模斉射（発射機5×レート2＝10発）＝迎撃能力以下。
            float smallSalvo = MissileWarfareRules.SalvoSize(5f, 2f, p);         // 10
            float smallSat = MissileWarfareRules.SaturationFactor(smallSalvo, capacity, p);
            float smallRate = MissileWarfareRules.InterceptionRate(capacity, smallSalvo, smallSat, p);
            float smallLeak = MissileWarfareRules.Leakers(smallSalvo, smallRate, jam, p);

            // 大規模飽和斉射（発射機20×レート2＝40発）＝迎撃能力を大きく超過。
            float bigSalvo = MissileWarfareRules.SalvoSize(20f, 2f, p);          // 40
            float bigSat = MissileWarfareRules.SaturationFactor(bigSalvo, capacity, p);
            float bigRate = MissileWarfareRules.InterceptionRate(capacity, bigSalvo, bigSat, p);
            float bigLeak = MissileWarfareRules.Leakers(bigSalvo, bigRate, jam, p);

            // 飽和した側は迎撃率が低く、有効弾も格段に多い＝飽和突破。
            Assert.Less(bigRate, smallRate, "飽和した大規模斉射の方が迎撃率は低い");
            Assert.Greater(bigLeak, smallLeak * 2f, "飽和突破で有効弾の到達が跳ね上がる");

            // 飽和攻撃は弾薬の消耗も早い。
            float smallDep = MissileWarfareRules.AmmoDepletion(smallSalvo, 100f, p);
            float bigDep = MissileWarfareRules.AmmoDepletion(bigSalvo, 100f, p);
            Assert.Greater(bigDep, smallDep, "大規模斉射ほど弾切れが早い");
        }
    }
}
