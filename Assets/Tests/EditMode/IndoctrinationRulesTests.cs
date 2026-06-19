using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>教化・思想注入（信念体系の植え付け）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class IndoctrinationRulesTests
    {
        const float Eps = 0.0001f;
        const float MulEps = 0.001f; // 乗算積の緩めの許容

        /// <summary>教化強度＝反復×0.6＋環境統制×0.4（既定の重み付き和）。</summary>
        [Test]
        public void IndoctrinationIntensity_反復と環境統制の重み付き和()
        {
            // 0.8*0.6 + 0.5*0.4 = 0.48 + 0.20 = 0.68
            Assert.AreEqual(0.68f, IndoctrinationRules.IndoctrinationIntensity(0.8f, 0.5f), Eps);
            // 反復のみ
            Assert.AreEqual(0.6f, IndoctrinationRules.IndoctrinationIntensity(1f, 0f), Eps);
            // 統制のみ
            Assert.AreEqual(0.4f, IndoctrinationRules.IndoctrinationIntensity(0f, 1f), Eps);
            // 両方揃うほど強い
            Assert.Greater(IndoctrinationRules.IndoctrinationIntensity(1f, 1f),
                           IndoctrinationRules.IndoctrinationIntensity(1f, 0f), "統制も加わると強い");
        }

        /// <summary>批判的思考の抑圧＝情報遮断×0.5＋教化強度×0.5（既定）。</summary>
        [Test]
        public void CriticalThinkingSuppression_情報遮断が批判を抑える()
        {
            // 0.8*0.5 + 0.6*0.5 = 0.4 + 0.3 = 0.7
            Assert.AreEqual(0.7f, IndoctrinationRules.CriticalThinkingSuppression(0.6f, 0.8f), Eps);
            // 遮断が強いほど抑圧も強い（単調）
            Assert.Greater(IndoctrinationRules.CriticalThinkingSuppression(0.5f, 1f),
                           IndoctrinationRules.CriticalThinkingSuppression(0.5f, 0f), "情報遮断が批判を抑える");
        }

        /// <summary>受容性＝若いほど高く、批判育成年齢で床へ低下しそれ以上は床のまま。</summary>
        [Test]
        public void AgeReceptivity_若いほど刷り込まれやすい()
        {
            Assert.AreEqual(0.9f, IndoctrinationRules.AgeReceptivity(0f), Eps);   // 最年少で上限
            Assert.AreEqual(0.2f, IndoctrinationRules.AgeReceptivity(30f), Eps);  // 批判育成年齢で床
            // 年齢15: t=0.5 → Lerp(0.9,0.2,0.5)=0.55
            Assert.AreEqual(0.55f, IndoctrinationRules.AgeReceptivity(15f), Eps);
            // それ以上は床のまま
            Assert.AreEqual(0.2f, IndoctrinationRules.AgeReceptivity(60f), Eps);
            // 若いほど受容性が高い
            Assert.Greater(IndoctrinationRules.AgeReceptivity(5f),
                           IndoctrinationRules.AgeReceptivity(25f), "若年層ほど受容性が高い");
        }

        /// <summary>内面化＝強度×受容性×思考抑圧の積（どれか欠ければ浅い）。</summary>
        [Test]
        public void Internalization_三者の積で内面化()
        {
            // 0.68 * 0.55 * 0.7 = 0.2618
            Assert.AreEqual(0.2618f, IndoctrinationRules.Internalization(0.68f, 0.55f, 0.7f), MulEps);
            // 思考抑圧が0なら内面化も0
            Assert.AreEqual(0f, IndoctrinationRules.Internalization(0.9f, 0.9f, 0f), Eps);
            // 全て最大で1
            Assert.AreEqual(1f, IndoctrinationRules.Internalization(1f, 1f, 1f), Eps);
        }

        /// <summary>抵抗＝既存信念0.4・批判力0.4・外部接触0.2の正規化重み付き和。</summary>
        [Test]
        public void Resistance_既存信念と批判力と外部接触が防壁()
        {
            // (0.5*0.4 + 0.5*0.4 + 0.5*0.2)/1.0 = 0.5
            Assert.AreEqual(0.5f, IndoctrinationRules.Resistance(0.5f, 0.5f, 0.5f), Eps);
            // 既存信念のみ → 0.4
            Assert.AreEqual(0.4f, IndoctrinationRules.Resistance(1f, 0f, 0f), Eps);
            // 外部接触のみ → 0.2
            Assert.AreEqual(0.2f, IndoctrinationRules.Resistance(0f, 0f, 1f), Eps);
            // 何も無ければ抵抗ゼロ
            Assert.AreEqual(0f, IndoctrinationRules.Resistance(0f, 0f, 0f), Eps);
        }

        /// <summary>信念採用＝内面化×(1−抵抗)（抵抗が強いほど採用されない）。</summary>
        [Test]
        public void BeliefAdoption_抵抗が採用を妨げる()
        {
            // 0.6 * (1 - 0.25) = 0.45
            Assert.AreEqual(0.45f, IndoctrinationRules.BeliefAdoption(0.6f, 0.25f), Eps);
            // 抵抗最大なら採用ゼロ
            Assert.AreEqual(0f, IndoctrinationRules.BeliefAdoption(0.8f, 1f), Eps);
            // 抵抗ゼロなら内面化そのまま
            Assert.AreEqual(0.6f, IndoctrinationRules.BeliefAdoption(0.6f, 0f), Eps);
        }

        /// <summary>世代再生産＝採用×(1＋制度的統制×0.4)で次世代へ増幅。</summary>
        [Test]
        public void GenerationalReproduction_制度的統制が再生産を強める()
        {
            // 0.5 * (1 + 1*0.4) = 0.7
            Assert.AreEqual(0.7f, IndoctrinationRules.GenerationalReproduction(0.5f, 1f), Eps);
            // 制度ゼロなら採用そのまま
            Assert.AreEqual(0.5f, IndoctrinationRules.GenerationalReproduction(0.5f, 0f), Eps);
            // 上限クランプ
            Assert.AreEqual(1f, IndoctrinationRules.GenerationalReproduction(0.9f, 1f), Eps);
        }

        /// <summary>効力判定＝信念採用が閾値0.5（既定）超で効いたとみなす（同値は不成立）。</summary>
        [Test]
        public void IsIndoctrinationEffective_閾値超で効力あり()
        {
            Assert.IsTrue(IndoctrinationRules.IsIndoctrinationEffective(0.6f));      // 既定閾値0.5超
            Assert.IsFalse(IndoctrinationRules.IsIndoctrinationEffective(0.4f));
            Assert.IsFalse(IndoctrinationRules.IsIndoctrinationEffective(0.5f));     // 同値は不成立
            Assert.IsTrue(IndoctrinationRules.IsIndoctrinationEffective(0.4f, 0.3f)); // 明示閾値
        }

        /// <summary>
        /// 物語テスト：若年層を情報遮断下で強く反復教化すると信念が根付き制度が再生産するが、
        /// 既存信念と批判力を備え外部に接した大人は同じ教化を跳ね返す。
        /// </summary>
        [Test]
        public void 物語_若者は刷り込まれ批判力ある大人は跳ね返す()
        {
            // --- 環境：最大の反復＋環境統制、深い情報遮断 ---
            float intensity = IndoctrinationRules.IndoctrinationIntensity(1f, 1f);          // 0.6+0.4=1.0
            float suppression = IndoctrinationRules.CriticalThinkingSuppression(intensity, 0.9f); // 0.95

            // --- 若年層：受容性が高く、既存信念も批判力も薄い ---
            float youthRecept = IndoctrinationRules.AgeReceptivity(5f);
            float youthIntern = IndoctrinationRules.Internalization(intensity, youthRecept, suppression);
            float youthResist = IndoctrinationRules.Resistance(0.05f, 0.05f, 0.05f);
            float youthAdoption = IndoctrinationRules.BeliefAdoption(youthIntern, youthResist);
            Assert.IsTrue(IndoctrinationRules.IsIndoctrinationEffective(youthAdoption),
                          "若年層は強い教化を内面化して信念を採用する");

            // --- 大人：受容性が低く、確たる信念・批判力・外部接触を備える ---
            float adultRecept = IndoctrinationRules.AgeReceptivity(45f);
            float adultIntern = IndoctrinationRules.Internalization(intensity, adultRecept, suppression);
            float adultResist = IndoctrinationRules.Resistance(0.9f, 0.9f, 0.9f);
            float adultAdoption = IndoctrinationRules.BeliefAdoption(adultIntern, adultResist);
            Assert.IsFalse(IndoctrinationRules.IsIndoctrinationEffective(adultAdoption),
                           "批判力ある大人は同じ教化を跳ね返す");
            Assert.Greater(youthAdoption, adultAdoption, "若者ほど信念を採用しやすい");

            // --- 制度的統制が若年層の信念を次世代へ再生産する ---
            float reproWithInst = IndoctrinationRules.GenerationalReproduction(youthAdoption, 1f);
            float reproNoInst = IndoctrinationRules.GenerationalReproduction(youthAdoption, 0f);
            Assert.Greater(reproWithInst, reproNoInst, "制度的統制が教化世代の再生産を強める");
        }
    }
}
