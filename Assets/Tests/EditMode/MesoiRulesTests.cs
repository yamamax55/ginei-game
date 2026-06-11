using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>中間層安定化係数の純ロジックの担保（#1495・アリストテレス『政治学』）。既定 MesoiParams 具体値で期待値を固定。</summary>
    public class MesoiRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>中間層シェア＝1−富者−貧者・クランプ（富者0.1+貧者0.5→中間層0.4）。</summary>
        [Test]
        public void 中間層シェア_富者貧者の残りで決まる()
        {
            Assert.AreEqual(0.4f, MesoiRules.MiddleClassShare(0.1f, 0.5f), Eps);
            // 富者貧者で1を超える＝中間層は0にクランプ。
            Assert.AreEqual(0f, MesoiRules.MiddleClassShare(0.6f, 0.6f), Eps);
        }

        /// <summary>政体安定倍率＝1.0+中間層シェア×stabilityGain(0.5)。中間層が分厚いほど高い。</summary>
        [Test]
        public void 政体安定倍率_中間層が分厚いほど高い()
        {
            // 中間層0.4 → 1.0+0.4×0.5=1.2。
            Assert.AreEqual(1.2f, MesoiRules.PolityStabilityFactor(0.4f), Eps);
            // 空洞化(0.0)は最小1.0、満杯(1.0)は1.5。
            Assert.AreEqual(1.0f, MesoiRules.PolityStabilityFactor(0f), Eps);
            Assert.AreEqual(1.5f, MesoiRules.PolityStabilityFactor(1f), Eps);
            // 分厚いほど単調増加。
            Assert.Greater(MesoiRules.PolityStabilityFactor(0.6f), MesoiRules.PolityStabilityFactor(0.2f));
        }

        /// <summary>階級の二極化＝富者貧者の合計−中間層・クランプ。砂時計ほど高い。</summary>
        [Test]
        public void 階級の二極化_中間層が薄いほど激化()
        {
            // 富者0.4+貧者0.4=0.8、中間層0.2 → 0.8−0.2=0.6。
            Assert.AreEqual(0.6f, MesoiRules.ClassPolarization(0.4f, 0.4f, 0.2f), Eps);
            // 中間層が分厚いと二極化は弱まる。
            Assert.Less(
                MesoiRules.ClassPolarization(0.25f, 0.25f, 0.5f),
                MesoiRules.ClassPolarization(0.45f, 0.45f, 0.1f));
        }

        /// <summary>穏健化作用＝中間層シェア×moderationMax(0.8)。極端政策を抑える。</summary>
        [Test]
        public void 穏健化作用_中間層が極端政策を抑える()
        {
            // 中間層0.5 → 0.5×0.8=0.4。
            Assert.AreEqual(0.4f, MesoiRules.ModerationEffect(0.5f), Eps);
            Assert.AreEqual(0f, MesoiRules.ModerationEffect(0f), Eps);
            Assert.AreEqual(0.8f, MesoiRules.ModerationEffect(1f), Eps);
        }

        /// <summary>緩衝能力＝中間層シェア×(1−対立)。中間層が分厚く対立が弱いほど吸収できる。</summary>
        [Test]
        public void 緩衝能力_中間層が対立を緩衝する()
        {
            // 中間層0.6・対立0.5 → 0.6×0.5=0.3。
            Assert.AreEqual(0.3f, MesoiRules.BufferingCapacity(0.6f, 0.5f), Eps);
            // 対立が強いほど能力を食い潰す。
            Assert.Greater(
                MesoiRules.BufferingCapacity(0.6f, 0.2f),
                MesoiRules.BufferingCapacity(0.6f, 0.9f));
            // 中間層ゼロは緩衝不能。
            Assert.AreEqual(0f, MesoiRules.BufferingCapacity(0f, 0.3f), Eps);
        }

        /// <summary>中間層の空洞化＝中間層シェア−格差×hollowingRate(0.5)×dt。格差が中間層を薄くする。</summary>
        [Test]
        public void 中間層の空洞化_格差が中間層を薄くする()
        {
            // 中間層0.5・格差0.4・dt1 → 0.5−0.4×0.5×1=0.3。
            Assert.AreEqual(0.3f, MesoiRules.HollowingOutTick(0.5f, 0.4f, 1f), Eps);
            // 格差ゼロは不変。
            Assert.AreEqual(0.5f, MesoiRules.HollowingOutTick(0.5f, 0f, 1f), Eps);
            // 格差が大きいほど薄くなる。
            Assert.Less(
                MesoiRules.HollowingOutTick(0.5f, 0.8f, 1f),
                MesoiRules.HollowingOutTick(0.5f, 0.2f, 1f));
        }

        /// <summary>ポリテイアの品質＝中間層×法の支配の積。どちらが欠けても良政体にならない。</summary>
        [Test]
        public void ポリテイアの品質_中間層と法の支配の積()
        {
            // 中間層0.6・法0.5 → 0.3。
            Assert.AreEqual(0.3f, MesoiRules.PoliteiaQuality(0.6f, 0.5f), Eps);
            // 法の支配ゼロは中間層が分厚くても品質ゼロ。
            Assert.AreEqual(0f, MesoiRules.PoliteiaQuality(1f, 0f), Eps);
            // 中間層ゼロも品質ゼロ。
            Assert.AreEqual(0f, MesoiRules.PoliteiaQuality(0f, 1f), Eps);
        }

        /// <summary>砂時計型社会＝中間層シェアが既定閾値(0.25)未満で true。</summary>
        [Test]
        public void 砂時計型社会_中間層が消えると判定()
        {
            // 0.2 < 0.25 → 砂時計。
            Assert.IsTrue(MesoiRules.IsHourglassSociety(0.2f));
            // 0.4 ≥ 0.25 → 健全。
            Assert.IsFalse(MesoiRules.IsHourglassSociety(0.4f));
        }
    }
}
