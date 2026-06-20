using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 個人崇拝＝指導者神格化の純ロジックの EditMode テスト。崇拝の醸成・偶像化・偶像と実像の乖離・
    /// 忠誠動員・批判封殺・神話の脆さ・後継危機・崇拝の安定判定を既定 Params の具体値で固定する。
    /// </summary>
    public class CultOfPersonalityRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsPow = 1e-3f;

        /// <summary>崇拝醸成＝カリスマ×宣伝の積を実績で底上げ（実績重み0.5）。</summary>
        [Test]
        public void CultBuilding_カリスマと宣伝と実績で醸成()
        {
            // 0.8×0.75 = 0.6、achFactor = 0.5 + 0.5×0.6 = 0.8 → 0.6×0.8 = 0.48
            Assert.AreEqual(0.48f, CultOfPersonalityRules.CultBuilding(0.8f, 0.75f, 0.6f), Eps);
            // 宣伝機構が無ければカリスマと実績があっても崇拝は立たない（積）
            Assert.AreEqual(0f, CultOfPersonalityRules.CultBuilding(1f, 0f, 1f), Eps);
        }

        /// <summary>実績が皆無でも achFactor 下限(1−0.5=0.5)が残る＝虚像のカリスマ崇拝。</summary>
        [Test]
        public void CultBuilding_実績ゼロでも虚像で崇拝が立つ()
        {
            // 0.8×0.8 = 0.64、achFactor = 0.5 + 0.5×0 = 0.5 → 0.64×0.5 = 0.32
            Assert.AreEqual(0.32f, CultOfPersonalityRules.CultBuilding(0.8f, 0.8f, 0f), Eps);
        }

        /// <summary>偶像化＝崇拝^0.7（指数1未満で誇張・神格化）。</summary>
        [Test]
        public void Idolization_崇拝が神格化で膨らむ()
        {
            // 0.5^0.7 = 2^-0.7 ≈ 0.61557（小さな崇拝も偶像化で持ち上がる）
            Assert.AreEqual(0.61557f, CultOfPersonalityRules.Idolization(0.5f), EpsPow);
            // 崇拝0は偶像化も0
            Assert.AreEqual(0f, CultOfPersonalityRules.Idolization(0f), EpsPow);
        }

        /// <summary>乖離＝偶像化−実力。正＝過大評価で危うい／負＝過小評価で健全寄り（符号付き−1..1）。</summary>
        [Test]
        public void RealityGap_偶像が実力を超えると正の乖離()
        {
            // 0.6 - 0.4 = 0.2（偶像が実力を超える＝危うい）
            Assert.AreEqual(0.2f, CultOfPersonalityRules.RealityGap(0.6f, 0.4f), Eps);
            // 実力が偶像を上回れば負（過小評価＝健全寄り）
            Assert.AreEqual(-0.3f, CultOfPersonalityRules.RealityGap(0.4f, 0.7f), Eps);
        }

        /// <summary>忠誠動員＝偶像化×動員倍率(1.0)。神格化が高いほど大衆を一斉に動かせる。</summary>
        [Test]
        public void LoyaltyMobilization_偶像化が忠誠を動員する()
        {
            Assert.AreEqual(0.8f, CultOfPersonalityRules.LoyaltyMobilization(0.8f), Eps);
            Assert.AreEqual(0f, CultOfPersonalityRules.LoyaltyMobilization(0f), Eps);
        }

        /// <summary>批判封殺＝崇拝/(崇拝+異論許容+床0.1)。異論が薄いほど黙らされる。</summary>
        [Test]
        public void CriticismSuppression_崇拝が強く異論が薄いほど封殺()
        {
            // 0.6/(0.6+0.2+0.1) = 0.6/0.9 ≈ 0.66667
            Assert.AreEqual(0.66667f, CultOfPersonalityRules.CriticismSuppression(0.6f, 0.2f), Eps);
            // 異論許容が厚いと封殺は効きにくい：0.6/(0.6+0.9+0.1) = 0.6/1.6 = 0.375
            Assert.AreEqual(0.375f, CultOfPersonalityRules.CriticismSuppression(0.6f, 0.9f), Eps);
        }

        /// <summary>神話の脆さ＝乖離(正部)×0.6 + 封殺×0.4。過大評価で封殺依存なほど脆い。</summary>
        [Test]
        public void MythFragility_乖離と封殺依存で神話が脆い()
        {
            // 0.5×0.6 + 0.6×0.4 = 0.3 + 0.24 = 0.54
            Assert.AreEqual(0.54f, CultOfPersonalityRules.MythFragility(0.5f, 0.6f), Eps);
            // 負の乖離（過小評価）は脆さに効かない：max(0,-0.3)=0 → 0 + 0.5×0.4 = 0.2
            Assert.AreEqual(0.2f, CultOfPersonalityRules.MythFragility(-0.3f, 0.5f), Eps);
        }

        /// <summary>後継危機＝崇拝×(1−制度)。個人依存で制度が弱いほどカリスマを継げない。</summary>
        [Test]
        public void SuccessionCrisis_制度が弱いとカリスマを継げない()
        {
            // 0.8×(1-0.25) = 0.6
            Assert.AreEqual(0.6f, CultOfPersonalityRules.SuccessionCrisis(0.8f, 0.25f), Eps);
            // 制度が固ければ崇拝が強くても継承は安定＝危機0
            Assert.AreEqual(0f, CultOfPersonalityRules.SuccessionCrisis(0.8f, 1f), Eps);
        }

        /// <summary>安定判定＝動員−脆さが既定閾値(0.2)以上か。脆さに食われると崇拝は維持できない。</summary>
        [Test]
        public void IsCultStable_動員が脆さを閾値ぶん上回れば安定()
        {
            // 0.8 - 0.54 = 0.26 ≥ 0.2 → 安定
            Assert.IsTrue(CultOfPersonalityRules.IsCultStable(0.8f, 0.54f));
            // 0.5 - 0.4 = 0.1 < 0.2 → 不安定（脆さに食われる）
            Assert.IsFalse(CultOfPersonalityRules.IsCultStable(0.5f, 0.4f));
        }

        /// <summary>
        /// 物語：宣伝で実績乏しい指導者を神格化→動員力は高いが偶像が実力を超え封殺で保つ脆い神話。
        /// 制度も弱く後継危機。衝撃で安定が崩れる「上から作った偶像は一気に剥がれる」の筋を通す。
        /// </summary>
        [Test]
        public void Story_虚像の神格化は脆く後継できない()
        {
            // 強いカリスマ×強い宣伝・実績は乏しい
            float cult = CultOfPersonalityRules.CultBuilding(0.9f, 0.9f, 0.1f);
            // 0.81 × (0.5 + 0.5×0.1) = 0.81×0.55 = 0.4455
            Assert.AreEqual(0.4455f, cult, Eps);

            float idol = CultOfPersonalityRules.Idolization(cult);   // 0.4455^0.7 ≈ 0.56780（神格化で持ち上がる）
            Assert.AreEqual(0.56780f, idol, EpsPow);
            Assert.Greater(idol, cult);                              // 偶像化は崇拝を誇張する

            // 実力は偶像化に及ばない＝過大評価
            float gap = CultOfPersonalityRules.RealityGap(idol, 0.45f);
            Assert.Greater(gap, 0f);

            float mob = CultOfPersonalityRules.LoyaltyMobilization(idol);  // 高い動員力
            float supp = CultOfPersonalityRules.CriticismSuppression(cult, 0.3f); // 批判を封殺
            float frag = CultOfPersonalityRules.MythFragility(gap, supp);  // 乖離＋封殺依存で脆い

            // 平時は動員が脆さを上回り崇拝は保たれる
            Assert.IsTrue(CultOfPersonalityRules.IsCultStable(mob, frag));

            // 制度が弱いと後継危機が深い
            float crisis = CultOfPersonalityRules.SuccessionCrisis(cult, 0.1f);
            Assert.Greater(crisis, 0.3f);

            // 衝撃で動員が崩れる（指導者の死など）と脆さに食われ崩壊する
            Assert.IsFalse(CultOfPersonalityRules.IsCultStable(mob * 0.5f, frag));
        }
    }
}
