using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// IntellectualCritiqueRules（SCHU-5 #1595・知識人階級と正統性侵食）の純ロジックテスト。
    /// 繁栄が職にあぶれた知識人を生み、彼らの批判が体制の正統性を内側から侵食する逆説を担保。
    /// </summary>
    public class IntellectualCritiqueRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>知識人余剰＝繁栄×高等教育×上限。両方高いほど大きく、どちらかゼロなら知識人は生まれない。</summary>
        [Test]
        public void IntellectualSurplus_繁栄と教育の積で余剰が生まれる()
        {
            // 0.9(上限) × 0.8(繁栄) × 0.5(教育) = 0.36
            Assert.AreEqual(0.36f, IntellectualCritiqueRules.IntellectualSurplus(0.8f, 0.5f), Eps);
            // 繁栄ゼロ＝糧で手一杯＝知識人ゼロ
            Assert.AreEqual(0f, IntellectualCritiqueRules.IntellectualSurplus(0f, 1f), Eps);
            // 教育ゼロ＝知識人ゼロ
            Assert.AreEqual(0f, IntellectualCritiqueRules.IntellectualSurplus(1f, 0f), Eps);
        }

        /// <summary>不満＝余剰のうち職で吸収しきれなかった供給超過。全員吸収すれば不満ゼロ。</summary>
        [Test]
        public void UnderemployedDiscontent_職にあぶれた分が不満になる()
        {
            // 余剰0.6 × (1 − 受け皿0.25) × 鋭さ1.0 = 0.45
            Assert.AreEqual(0.45f, IntellectualCritiqueRules.UnderemployedDiscontent(0.6f, 0.25f), Eps);
            // 受け皿が全員吸収＝不満ゼロ（皆が職を得れば批判の担い手は生まれない）
            Assert.AreEqual(0f, IntellectualCritiqueRules.UnderemployedDiscontent(0.6f, 1f), Eps);
        }

        /// <summary>批判圧＝不満×批判重み×言論の自由。自由がゼロなら表向きの批判圧はゼロ。</summary>
        [Test]
        public void CritiquePressure_自由な環境で不満が批判に増幅される()
        {
            // 0.5(不満) × 0.8(重み) × 1.0(自由) = 0.4
            Assert.AreEqual(0.4f, IntellectualCritiqueRules.CritiquePressure(0.5f, 1f), Eps);
            // 言論統制（自由ゼロ）＝表の批判圧ゼロ
            Assert.AreEqual(0f, IntellectualCritiqueRules.CritiquePressure(0.5f, 0f), Eps);
        }

        /// <summary>正統性侵食＝批判圧が時間をかけて正統性を削る。批判圧ゼロなら不変。</summary>
        [Test]
        public void LegitimacyErosionTick_批判圧が正統性を内側から削る()
        {
            // 0.8 − 0.05(速度) × 0.6(批判圧) × 2.0(dt) = 0.8 − 0.06 = 0.74
            Assert.AreEqual(0.74f, IntellectualCritiqueRules.LegitimacyErosionTick(0.8f, 0.6f, 2f), Eps);
            // 批判圧ゼロ＝侵食なし
            Assert.AreEqual(0.8f, IntellectualCritiqueRules.LegitimacyErosionTick(0.8f, 0f, 5f), Eps);
        }

        /// <summary>取り込み＝庇護で批判が和らぐ（飼い慣らし）。庇護ゼロなら素通り。</summary>
        [Test]
        public void CooptationEffect_庇護で批判が飼い慣らされる()
        {
            // 0.5 × (1 − 0.6(重み) × 1.0(庇護)) = 0.5 × 0.4 = 0.2
            Assert.AreEqual(0.2f, IntellectualCritiqueRules.CooptationEffect(0.5f, 1f), Eps);
            // 庇護ゼロ＝批判圧は素通り
            Assert.AreEqual(0.5f, IntellectualCritiqueRules.CooptationEffect(0.5f, 0f), Eps);
        }

        /// <summary>弾圧の反発＝弾圧が殉教者を生み批判圧を増幅する逆効果。取り込みの正反対。</summary>
        [Test]
        public void RepressionBacklash_弾圧はかえって批判を膨らませる()
        {
            // 0.5 × (1 + 0.5(重み) × 0.8(弾圧)) = 0.5 × 1.4 = 0.7
            Assert.AreEqual(0.7f, IntellectualCritiqueRules.RepressionBacklash(0.5f, 0.8f), Eps);
            // 弾圧ゼロ＝素通り
            Assert.AreEqual(0.5f, IntellectualCritiqueRules.RepressionBacklash(0.5f, 0f), Eps);
            // 弾圧は取り込みの正反対＝同じ批判圧で取り込みより大きくなる
            float coopted = IntellectualCritiqueRules.CooptationEffect(0.5f, 0.8f);
            float repressed = IntellectualCritiqueRules.RepressionBacklash(0.5f, 0.8f);
            Assert.Greater(repressed, coopted);
        }

        /// <summary>自壊度＝繁栄×(1−正統性)。成功と正統性低下が重なるとき最大＝墓掘り人を育てる逆説。</summary>
        [Test]
        public void SelfUnderminingIndex_繁栄が高く正統性が低いほど自壊する()
        {
            // 0.9(繁栄) × (1 − 0.3(正統性)) = 0.9 × 0.7 = 0.63
            Assert.AreEqual(0.63f, IntellectualCritiqueRules.SelfUnderminingIndex(0.9f, 0.3f), Eps);
            // 繁栄ゼロ＝墓掘り人を養えない＝自壊なし
            Assert.AreEqual(0f, IntellectualCritiqueRules.SelfUnderminingIndex(0f, 0f), Eps);
            // 正統性満点＝批判が効かない＝自壊なし
            Assert.AreEqual(0f, IntellectualCritiqueRules.SelfUnderminingIndex(1f, 1f), Eps);
        }

        /// <summary>知識人反乱の判定＝批判圧が閾値を超えたか。既定閾値0.6境界を担保。</summary>
        [Test]
        public void IsIntelligentsiaRevolt_批判圧が閾値を超えると反乱()
        {
            // 既定閾値0.6
            Assert.IsTrue(IntellectualCritiqueRules.IsIntelligentsiaRevolt(0.7f));
            Assert.IsFalse(IntellectualCritiqueRules.IsIntelligentsiaRevolt(0.5f));
            // ちょうど閾値は超えていない（厳密に超過のみ反乱）
            Assert.IsFalse(IntellectualCritiqueRules.IsIntelligentsiaRevolt(0.6f, 0.6f));
            Assert.IsTrue(IntellectualCritiqueRules.IsIntelligentsiaRevolt(0.61f, 0.6f));
        }
    }
}
