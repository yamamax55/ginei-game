using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>近代化プログラム（SKUN-1 #1431・坂の上の雲型の後発国の富国強兵）の純ロジック検証。</summary>
    public class ModernizationProgramRulesTests
    {
        /// <summary>後発の利益＝先進国との差が大きいほど模倣で速く追いつける（差ゼロはボーナスなし）。</summary>
        [Test]
        public void BacknessAdvantage_差が大きいほど加速()
        {
            // 既定 backnessAdvantageScale=0.5。
            Assert.AreEqual(1f, ModernizationProgramRules.BacknessAdvantage(0f), 1e-4f);    // 追いついた国
            Assert.AreEqual(1.25f, ModernizationProgramRules.BacknessAdvantage(0.5f), 1e-4f);
            Assert.AreEqual(1.5f, ModernizationProgramRules.BacknessAdvantage(1f), 1e-4f);  // 最も後発
            // 単調増加。
            Assert.Greater(ModernizationProgramRules.BacknessAdvantage(0.8f),
                           ModernizationProgramRules.BacknessAdvantage(0.3f));
        }

        /// <summary>多面加速＝三分野の同時投資で加速（総合×バランス）。均等な満額が最大。</summary>
        [Test]
        public void MultiFrontAcceleration_均等満額が最大()
        {
            // 既定 multiFrontScale=1。均等満額：total=1, balance=1 → 1.0。
            Assert.AreEqual(1f, ModernizationProgramRules.MultiFrontAcceleration(1f, 1f, 1f), 1e-4f);
            // 一分野ゼロ：balance=0 → 加速ゼロ（足を引っ張る）。
            Assert.AreEqual(0f, ModernizationProgramRules.MultiFrontAcceleration(1f, 1f, 0f), 1e-4f);
            // 均等0.5：total=0.5, balance=1 → 0.5。
            Assert.AreEqual(0.5f, ModernizationProgramRules.MultiFrontAcceleration(0.5f, 0.5f, 0.5f), 1e-4f);
            // 偏った高投資は均等な中投資に劣りうる。
            Assert.Greater(ModernizationProgramRules.MultiFrontAcceleration(0.6f, 0.6f, 0.6f),
                           ModernizationProgramRules.MultiFrontAcceleration(1f, 1f, 0.2f));
        }

        /// <summary>バランス発展＝偏ると一分野が他の足を引っ張る（最小律）。</summary>
        [Test]
        public void BalancedDevelopment_最小律で偏りを罰する()
        {
            // 均等＝1.0。
            Assert.AreEqual(1f, ModernizationProgramRules.BalancedDevelopment(0.8f, 0.8f, 0.8f), 1e-4f);
            // 最弱/最強の比：min=0.2, max=1.0 → 0.2。
            Assert.AreEqual(0.2f, ModernizationProgramRules.BalancedDevelopment(1f, 0.6f, 0.2f), 1e-4f);
            // 全ゼロ＝0。
            Assert.AreEqual(0f, ModernizationProgramRules.BalancedDevelopment(0f, 0f, 0f), 1e-4f);
        }

        /// <summary>国家の後押し＝関与と財政動員の積（両輪・片方ゼロは効かない）。</summary>
        [Test]
        public void StatePushFactor_関与と財政の両輪()
        {
            // 既定 statePushScale=0.5。両方満額：1+0.5*1*1=1.5。
            Assert.AreEqual(1.5f, ModernizationProgramRules.StatePushFactor(1f, 1f), 1e-4f);
            // 財政ゼロ＝後押しなし（関与だけでは動かせない）。
            Assert.AreEqual(1f, ModernizationProgramRules.StatePushFactor(1f, 0f), 1e-4f);
            // 半々：1+0.5*0.5*0.5=1.125。
            Assert.AreEqual(1.125f, ModernizationProgramRules.StatePushFactor(0.5f, 0.5f), 1e-4f);
        }

        /// <summary>近代化の進行＝加速度に応じて上限1へ漸近（加速度ゼロは進まない）。</summary>
        [Test]
        public void ModernizationTick_漸近的に進む()
        {
            // 既定 rate=0.05。level=0, accel=1, dt=1 → 0 + 0.05*1*1*1 = 0.05。
            Assert.AreEqual(0.05f, ModernizationProgramRules.ModernizationTick(0f, 1f, 1f), 1e-4f);
            // 加速度ゼロ＝据え置き。
            Assert.AreEqual(0.3f, ModernizationProgramRules.ModernizationTick(0.3f, 0f, 1f), 1e-4f);
            // 上限近くは伸びが鈍る（残り少ない不足分のみ埋める）。
            float fromLow = ModernizationProgramRules.ModernizationTick(0.1f, 1f, 1f);
            float fromHigh = ModernizationProgramRules.ModernizationTick(0.9f, 1f, 1f);
            Assert.Greater(fromLow - 0.1f, fromHigh - 0.9f);
            // 1を超えない。
            Assert.LessOrEqual(ModernizationProgramRules.ModernizationTick(0.99f, 1f, 100f), 1f);
        }

        /// <summary>過伸張の歪み＝ペースが社会の吸収能力を超えた分だけ歪む。</summary>
        [Test]
        public void OverstretchStrain_吸収を超えた分だけ歪む()
        {
            // 既定 overstretchScale=0.6。ペース0.9・吸収0.4 → excess=0.5 → 0.6*0.5=0.3。
            Assert.AreEqual(0.3f, ModernizationProgramRules.OverstretchStrain(0.9f, 0.4f), 1e-4f);
            // 吸収できる範囲＝歪みゼロ。
            Assert.AreEqual(0f, ModernizationProgramRules.OverstretchStrain(0.4f, 0.9f), 1e-4f);
            // ペースが速いほど歪む。
            Assert.Greater(ModernizationProgramRules.OverstretchStrain(1f, 0.2f),
                           ModernizationProgramRules.OverstretchStrain(0.6f, 0.2f));
        }

        /// <summary>追いつき度＝先進国水準に対する相対位置（比較相手なしは追いついた扱い）。</summary>
        [Test]
        public void CatchUpProximity_相対位置を返す()
        {
            // level=0.4, leader=0.8 → 0.5。
            Assert.AreEqual(0.5f, ModernizationProgramRules.CatchUpProximity(0.4f, 0.8f), 1e-4f);
            // 追い越しても1でクランプ。
            Assert.AreEqual(1f, ModernizationProgramRules.CatchUpProximity(0.9f, 0.5f), 1e-4f);
            // 比較相手なし（leader=0）＝1。
            Assert.AreEqual(1f, ModernizationProgramRules.CatchUpProximity(0.3f, 0f), 1e-4f);
        }

        /// <summary>近代化成功＝水準とバランスがともに閾値以上（一分野偏重の見かけは成功でない）。</summary>
        [Test]
        public void IsSuccessfulModernization_バランスよく到達した時のみ成功()
        {
            // 既定 threshold=0.7。両方満たす。
            Assert.IsTrue(ModernizationProgramRules.IsSuccessfulModernization(0.8f, 0.75f));
            // 水準は高いがバランス不足＝偏重の見かけ＝成功でない。
            Assert.IsFalse(ModernizationProgramRules.IsSuccessfulModernization(0.9f, 0.3f));
            // バランスは良いが水準不足＝まだ届かない。
            Assert.IsFalse(ModernizationProgramRules.IsSuccessfulModernization(0.5f, 0.9f));
            // 境界（ちょうど閾値）＝成功。
            Assert.IsTrue(ModernizationProgramRules.IsSuccessfulModernization(0.7f, 0.7f));
        }
    }
}
