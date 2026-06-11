using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>招安ジレンマ（SHZ-3 #1359・水滸伝＝梁山泊の招安と解体）の純ロジック検証。</summary>
    public class CooptionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>招安の魅力＝官位・恩赦・正統性を既定重み(0.4/0.35/0.25)で合成して正規化する。</summary>
        [Test]
        public void CooptionOffer_既定重みで合成される()
        {
            // 全付与なら合計1.0で最大魅力。
            Assert.AreEqual(1f, CooptionRules.CooptionOffer(1f, 1f, 1f), Eps);
            // 官位のみ＝0.4。
            Assert.AreEqual(0.4f, CooptionRules.CooptionOffer(1f, 0f, 0f), Eps);
            // 正統性のみ＝0.25（賊から官への大義）。
            Assert.AreEqual(0.25f, CooptionRules.CooptionOffer(0f, 0f, 1f), Eps);
        }

        /// <summary>受諾閾値＝疲弊と頭領の出世欲が閾値を下げ、思想の純度が閾値を上げる。</summary>
        [Test]
        public void AcceptanceThreshold_純度が高く疲弊なしなら高い()
        {
            // 純度1・疲弊0・出世欲0＝閾値1.0（純粋な反体制は容易に転ばない）。
            Assert.AreEqual(1f, CooptionRules.AcceptanceThreshold(0f, 0f, 1f), Eps);
            // 純度1だが疲弊1・出世欲1＝1 - 0.5 - 0.5 = 0（疲弊し出世を望めば容易に転ぶ）。
            Assert.AreEqual(0f, CooptionRules.AcceptanceThreshold(1f, 1f, 1f), Eps);
            // 純度0.8・疲弊0.4・出世欲0.6＝0.8 - 0.2 - 0.3 = 0.3。
            Assert.AreEqual(0.3f, CooptionRules.AcceptanceThreshold(0.4f, 0.6f, 0.8f), Eps);
        }

        /// <summary>受諾判定＝魅力が閾値を超えれば受諾（宋江の選択）、純粋な反体制には高い魅力が要る。</summary>
        [Test]
        public void AcceptCooption_魅力が閾値超で受諾()
        {
            // 魅力0.7 > 閾値0.3 ＝受諾。
            Assert.IsTrue(CooptionRules.AcceptCooption(0.7f, 0.3f));
            // 魅力0.5 < 閾値0.8（純粋な反体制）＝拒否。
            Assert.IsFalse(CooptionRules.AcceptCooption(0.5f, 0.8f));
            // 同値は受諾しない（厳密超）。
            Assert.IsFalse(CooptionRules.AcceptCooption(0.5f, 0.5f));
        }

        /// <summary>結束ドリフト＝体制に吸収されるほど元の結束(梁山泊の義)が時間で薄れる。</summary>
        [Test]
        public void CohesionDriftTick_吸収で結束が薄れる()
        {
            // cohesion0.8・吸収0.5・dt1：減衰 = 0.5*0.3*1 = 0.15 → 0.65。
            Assert.AreEqual(0.65f, CooptionRules.CohesionDriftTick(0.8f, 0.5f, 1f), Eps);
            // 吸収0なら結束は減らない（取り込まれなければ義は保たれる）。
            Assert.AreEqual(0.8f, CooptionRules.CohesionDriftTick(0.8f, 0f, 1f), Eps);
            // 下限0でクランプ。
            Assert.AreEqual(0f, CooptionRules.CohesionDriftTick(0.1f, 1f, 1f), Eps);
        }

        /// <summary>体制への吸収＝結束の喪失と体制の統制の積（独立性の喪失）。</summary>
        [Test]
        public void AbsorptionIntoSystem_結束喪失と統制の積()
        {
            // 結束喪失0.8・統制0.5＝0.4。
            Assert.AreEqual(0.4f, CooptionRules.AbsorptionIntoSystem(0.8f, 0.5f), Eps);
            // 統制0なら吸収されない（体制が統制しなければ手駒にならない）。
            Assert.AreEqual(0f, CooptionRules.AbsorptionIntoSystem(0.8f, 0f), Eps);
        }

        /// <summary>使い捨てリスク＝吸収が深く体制の信義が低いほど高い（水滸伝の悲劇）。</summary>
        [Test]
        public void DisposableAfterUse_信用なき体制ほど高い()
        {
            // 吸収1・信義0（不信1）・既定disposalScale0.8 ＝ 0.8。
            Assert.AreEqual(0.8f, CooptionRules.DisposableAfterUse(1f, 0f), Eps);
            // 体制が信義に厚い(1.0)なら使い捨てリスクは0。
            Assert.AreEqual(0f, CooptionRules.DisposableAfterUse(1f, 1f), Eps);
            // 吸収0.5・信義0.5（不信0.5）＝0.5*0.5*0.8 = 0.2。
            Assert.AreEqual(0.2f, CooptionRules.DisposableAfterUse(0.5f, 0.5f), Eps);
        }

        /// <summary>二重忠誠＝吸収が半ばで元の義が残るほど板挟みが最大になる山型。</summary>
        [Test]
        public void LoyaltyAmbiguity_中間で板挟み最大()
        {
            // 吸収0.5・元の義1＝4*0.5*0.5*1 = 1.0（最大の揺れ）。
            Assert.AreEqual(1f, CooptionRules.LoyaltyAmbiguity(0.5f, 1f), Eps);
            // 完全に吸収(1.0)なら迷いなし＝0。
            Assert.AreEqual(0f, CooptionRules.LoyaltyAmbiguity(1f, 1f), Eps);
            // 完全に賊のまま(0)でも迷いなし＝0。
            Assert.AreEqual(0f, CooptionRules.LoyaltyAmbiguity(0f, 1f), Eps);
            // 元の義が無ければ板挟みも無い。
            Assert.AreEqual(0f, CooptionRules.LoyaltyAmbiguity(0.5f, 0f), Eps);
        }

        /// <summary>取り込み判定＝吸収度が既定閾値0.6を超えたら招安成立（官の一部隊）。</summary>
        [Test]
        public void IsCoopted_閾値超で取り込み成立()
        {
            Assert.IsTrue(CooptionRules.IsCoopted(0.7f));   // 0.7 > 0.6 ＝取り込まれた
            Assert.IsFalse(CooptionRules.IsCoopted(0.5f));  // 0.5 < 0.6 ＝まだ独立勢力
            Assert.IsFalse(CooptionRules.IsCoopted(0.6f));  // 同値は不成立（厳密超）
        }
    }
}
