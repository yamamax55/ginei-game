using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>代表による派閥濾過（FED-6 #1494・フェデラリスト第10篇）の純ロジック検証。</summary>
    public class RepresentativeFilterRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>濾過強度＝規模と質の加重和（既定0.5/0.5）。区が大きく質が高いほど強い。</summary>
        [Test]
        public void FilterStrength_加重和()
        {
            // 規模0.8×0.5 ＋ 質0.6×0.5 ＝ 0.4＋0.3 ＝ 0.7
            Assert.AreEqual(0.7f, RepresentativeFilterRules.FilterStrength(0.8f, 0.6f), Eps);
            // 両方0で0、両方1で1
            Assert.AreEqual(0f, RepresentativeFilterRules.FilterStrength(0f, 0f), Eps);
            Assert.AreEqual(1f, RepresentativeFilterRules.FilterStrength(1f, 1f), Eps);
        }

        /// <summary>情念の冷却＝濾過強度ぶん生の情念を薄める。</summary>
        [Test]
        public void PassionCooling_濾過で冷める()
        {
            // 情念0.8、濾過0.5 → 0.8×(1−0.5)＝0.4
            Assert.AreEqual(0.4f, RepresentativeFilterRules.PassionCooling(0.5f, 0.8f), Eps);
            // 濾過1で完全に冷める、0で生のまま
            Assert.AreEqual(0f, RepresentativeFilterRules.PassionCooling(1f, 0.8f), Eps);
            Assert.AreEqual(0.8f, RepresentativeFilterRules.PassionCooling(0f, 0.8f), Eps);
        }

        /// <summary>世論の精錬＝生の世論を中庸0.5（公益）へ引き寄せる（refine and enlarge）。</summary>
        [Test]
        public void PublicViewRefinement_公益へ精錬()
        {
            // 生0.9、濾過0.5 → Lerp(0.9,0.5,0.5)＝0.7
            Assert.AreEqual(0.7f, RepresentativeFilterRules.PublicViewRefinement(0.5f, 0.9f), Eps);
            // 濾過0なら生のまま
            Assert.AreEqual(0.9f, RepresentativeFilterRules.PublicViewRefinement(0f, 0.9f), Eps);
            // 濾過1で完全に中庸へ
            Assert.AreEqual(0.5f, RepresentativeFilterRules.PublicViewRefinement(1f, 0.1f), Eps);
        }

        /// <summary>小選挙区の乗っ取り＝区が小さいほど派閥集中度が効く。</summary>
        [Test]
        public void SmallDistrictCapture_小区で派閥に握られる()
        {
            // 小区(規模0.2)×集中0.8 → (1−0.2)×0.8＝0.64
            Assert.AreEqual(0.64f, RepresentativeFilterRules.SmallDistrictCapture(0.2f, 0.8f), Eps);
            // 大区は乗っ取りにくい
            Assert.Less(RepresentativeFilterRules.SmallDistrictCapture(0.9f, 0.8f),
                        RepresentativeFilterRules.SmallDistrictCapture(0.2f, 0.8f));
            // 規模1なら乗っ取り0
            Assert.AreEqual(0f, RepresentativeFilterRules.SmallDistrictCapture(1f, 1f), Eps);
        }

        /// <summary>大選挙区の遊離＝閾値超過ぶんが残り幅で正規化、閾値以下は0。</summary>
        [Test]
        public void LargeDistrictDetachment_大区で民意から離れる()
        {
            // 閾値0.8、規模0.9 → (0.9−0.8)/(1−0.8)＝0.5
            Assert.AreEqual(0.5f, RepresentativeFilterRules.LargeDistrictDetachment(0.9f, 0.8f), Eps);
            // 閾値以下なら遊離なし
            Assert.AreEqual(0f, RepresentativeFilterRules.LargeDistrictDetachment(0.7f, 0.8f), Eps);
            // 規模1で最大遊離
            Assert.AreEqual(1f, RepresentativeFilterRules.LargeDistrictDetachment(1f, 0.8f), Eps);
        }

        /// <summary>最適選挙区規模＝多様性が高いほど大きめの区が要る。</summary>
        [Test]
        public void OptimalDistrictSize_多様性で上振れ()
        {
            // 多様性0.5 → 0.4＋0.4×0.5＝0.6
            Assert.AreEqual(0.6f, RepresentativeFilterRules.OptimalDistrictSize(0.5f), Eps);
            // 多様性0で下限0.4、1で上限0.8
            Assert.AreEqual(0.4f, RepresentativeFilterRules.OptimalDistrictSize(0f), Eps);
            Assert.AreEqual(0.8f, RepresentativeFilterRules.OptimalDistrictSize(1f), Eps);
        }

        /// <summary>扇動家への抵抗＝濾過強度がそのまま抵抗力（直接動員の逆）。</summary>
        [Test]
        public void DemagogueResistance_濾過が抵抗()
        {
            Assert.AreEqual(0.7f, RepresentativeFilterRules.DemagogueResistance(0.7f), Eps);
            Assert.AreEqual(0f, RepresentativeFilterRules.DemagogueResistance(0f), Eps);
            Assert.AreEqual(1f, RepresentativeFilterRules.DemagogueResistance(1.5f), Eps); // クランプ
        }

        /// <summary>良い代表制＝濾過強度が閾値以上かつ乗っ取りリスクが閾値未満（既定0.5）。</summary>
        [Test]
        public void IsWellFiltered_濾せて派閥に握られない()
        {
            // 濾過0.7≥0.5 かつ 乗っ取り0.3<0.5 → 良い
            Assert.IsTrue(RepresentativeFilterRules.IsWellFiltered(0.7f, 0.3f));
            // 濾過が足りない
            Assert.IsFalse(RepresentativeFilterRules.IsWellFiltered(0.4f, 0.3f));
            // 濾せても派閥に握られていれば良くない
            Assert.IsFalse(RepresentativeFilterRules.IsWellFiltered(0.7f, 0.6f));
        }
    }
}
