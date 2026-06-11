using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>余剰性（superfluousness）の純ロジックのテスト（TOTL-4 #1524・アーレント型）。</summary>
    public class SuperfluousnessRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>余剰割合＝失業・没落・根なし化の加重合成（既定重み0.4/0.35/0.25・和=1で正規化）。</summary>
        [Test]
        public void SuperfluousShare_重み付き合成で不要人口の割合を出す()
        {
            // 0.4*0.5 + 0.35*0.2 + 0.25*0.8 = 0.2 + 0.07 + 0.2 = 0.47 （和=1なので除算なし）
            float share = SuperfluousnessRules.SuperfluousShare(0.5f, 0.2f, 0.8f);
            Assert.AreEqual(0.47f, share, Eps);

            // 全て最大なら1.0（飽和）。
            Assert.AreEqual(1f, SuperfluousnessRules.SuperfluousShare(1f, 1f, 1f), Eps);
        }

        /// <summary>不要の感覚＝余剰割合を承認の欠如が深める（承認1なら深化なし）。</summary>
        [Test]
        public void FeelingOfRedundancy_承認の欠如が不要感を深める()
        {
            // 承認1.0なら深化分0＝余剰割合そのまま。
            Assert.AreEqual(0.6f, SuperfluousnessRules.FeelingOfRedundancy(0.6f, 1f), Eps);

            // 承認0.0：0.6 + 0.6*1*0.5 = 0.6 + 0.3 = 0.9（承認の欠如が増幅）。
            Assert.AreEqual(0.9f, SuperfluousnessRules.FeelingOfRedundancy(0.6f, 0f), Eps);
        }

        /// <summary>運動の吸収＝余剰割合×訴求×吸収率。両者揃って回り、どちらか0なら0。</summary>
        [Test]
        public void MovementAbsorption_余剰人口が運動に吸収される()
        {
            // 0.8 * 0.5 * 0.7 = 0.28
            Assert.AreEqual(0.28f, SuperfluousnessRules.MovementAbsorption(0.8f, 0.5f), Eps);

            // 訴求0なら吸収0、余剰0でも吸収0。
            Assert.AreEqual(0f, SuperfluousnessRules.MovementAbsorption(0.8f, 0f), Eps);
            Assert.AreEqual(0f, SuperfluousnessRules.MovementAbsorption(0f, 0.5f), Eps);
        }

        /// <summary>動員燃料＝吸収率がそのまま運動の燃料量（TotalitarianRules への入力）。</summary>
        [Test]
        public void MobilizationFuel_吸収率がそのまま燃料量になる()
        {
            Assert.AreEqual(0.28f, SuperfluousnessRules.MobilizationFuel(0.28f), Eps);
            // クランプ確認。
            Assert.AreEqual(1f, SuperfluousnessRules.MobilizationFuel(1.5f), Eps);
        }

        /// <summary>使い捨ての常態化＝余剰×非人間化ぶんずつ進む（強制収容所の論理）。</summary>
        [Test]
        public void DisposabilityNormalization_使い捨て感覚が常態化していく()
        {
            // 0.1 + 0.2*0.8*0.5*1 = 0.1 + 0.08 = 0.18
            float n = SuperfluousnessRules.DisposabilityNormalization(0.1f, 0.8f, 0.5f, 1f);
            Assert.AreEqual(0.18f, n, Eps);

            // 非人間化0なら進まない。
            Assert.AreEqual(0.1f, SuperfluousnessRules.DisposabilityNormalization(0.1f, 0.8f, 0f, 1f), Eps);
        }

        /// <summary>意味への飢え＝不要の感覚がそのまま意味への渇望（虚構の意味への燃えやすさ）。</summary>
        [Test]
        public void MeaningHunger_無意味感が意味への飢えを生む()
        {
            Assert.AreEqual(0.7f, SuperfluousnessRules.MeaningHunger(0.7f), Eps);
            Assert.AreEqual(0f, SuperfluousnessRules.MeaningHunger(0f), Eps);
        }

        /// <summary>意味による再統合＝意味ある仕事ぶん余剰割合が下がる（運動の燃料を断つ処方）。</summary>
        [Test]
        public void ReintegrationViaPurpose_意味ある役割が余剰性を解消する()
        {
            // 0.6 - 0.3*0.5*1 = 0.6 - 0.15 = 0.45
            float s = SuperfluousnessRules.ReintegrationViaPurpose(0.6f, 0.5f, 1f);
            Assert.AreEqual(0.45f, s, Eps);

            // 仕事0なら解消なし＝据え置き。
            Assert.AreEqual(0.6f, SuperfluousnessRules.ReintegrationViaPurpose(0.6f, 0f, 1f), Eps);
        }

        /// <summary>大量余剰判定＝余剰割合が既定しきい値(0.5)以上で運動の温床。</summary>
        [Test]
        public void IsMassSuperfluity_大量の余剰人口が温床になる()
        {
            Assert.IsTrue(SuperfluousnessRules.IsMassSuperfluity(0.5f));
            Assert.IsTrue(SuperfluousnessRules.IsMassSuperfluity(0.7f));
            Assert.IsFalse(SuperfluousnessRules.IsMassSuperfluity(0.49f));
        }

        /// <summary>既定Paramsの具体値（合成重み和=1・各係数）を固定。</summary>
        [Test]
        public void Default_既定パラメータの値を固定する()
        {
            var p = SuperfluousnessParams.Default;
            Assert.AreEqual(0.4f, p.unemploymentWeight, Eps);
            Assert.AreEqual(0.35f, p.declassedWeight, Eps);
            Assert.AreEqual(0.25f, p.rootlessWeight, Eps);
            Assert.AreEqual(0.5f, p.redundancyDeepenWeight, Eps);
            Assert.AreEqual(0.7f, p.absorptionWeight, Eps);
            Assert.AreEqual(0.2f, p.normalizeRate, Eps);
            Assert.AreEqual(0.3f, p.reintegrationRate, Eps);
            Assert.AreEqual(0.5f, p.massThreshold, Eps);
        }
    }
}
