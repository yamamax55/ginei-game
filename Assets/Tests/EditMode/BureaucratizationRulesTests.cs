using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// BureaucratizationRules（官僚化とイノベーション死・SCHU-3 #1587）のEditModeテスト。
    /// 既定 Params の具体値で期待値を固定し、官僚化の進行・革新力の逆相関・ルーティン化・人材流出・
    /// 自壊・硬直化・改革コスト・革新死判定を担保する。
    /// </summary>
    public class BureaucratizationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>成功と規模が官僚化を時間で進める（成功が手続きを増やす）。</summary>
        [Test]
        public void BureaucratizationTick_成功と規模で官僚化が進む()
        {
            // 成功1×0.08 + 規模1×0.05 = 0.13/秒。2秒で +0.26。
            float b = BureaucratizationRules.BureaucratizationTick(0.1f, 1f, 1f, 2f);
            Assert.AreEqual(0.36f, b, Eps);

            // 成功0かつ規模0なら進まない（無風）。
            float still = BureaucratizationRules.BureaucratizationTick(0.4f, 0f, 0f, 5f);
            Assert.AreEqual(0.4f, still, Eps);
        }

        /// <summary>官僚化が進むほど革新力が落ちる（逆相関＝手続きが起業家精神を窒息）。</summary>
        [Test]
        public void InnovationCapacity_官僚化と逆相関()
        {
            // 官僚化0で満額1、官僚化1で0。
            Assert.AreEqual(1f, BureaucratizationRules.InnovationCapacity(0f), Eps);
            Assert.AreEqual(0f, BureaucratizationRules.InnovationCapacity(1f), Eps);

            // 官僚化0.5＝(0.5)^1.5 ≒ 0.35355。低官僚化の方が革新力が高い（単調減少）。
            float mid = BureaucratizationRules.InnovationCapacity(0.5f);
            Assert.AreEqual(Mathf.Pow(0.5f, 1.5f), mid, Eps);
            Assert.Greater(BureaucratizationRules.InnovationCapacity(0.3f),
                BureaucratizationRules.InnovationCapacity(0.7f));
        }

        /// <summary>革新が計画化・ルーティン化される（企業家機能の陳腐化）。</summary>
        [Test]
        public void RoutinizationOfInnovation_官僚化と計画化の積()
        {
            // 0.6 × 0.5 × maxRoutinization0.9 = 0.27。
            float r = BureaucratizationRules.RoutinizationOfInnovation(0.6f, 0.5f);
            Assert.AreEqual(0.27f, r, Eps);

            // 官僚化0ならまだ個人の才に依る＝ルーティン化0。
            Assert.AreEqual(0f, BureaucratizationRules.RoutinizationOfInnovation(0f, 1f), Eps);
        }

        /// <summary>官僚化を嫌った革新的人材が去る（窒息した才能の流出）。</summary>
        [Test]
        public void EntrepreneurExodus_官僚化と流動性で流出()
        {
            // 0.8 × 0.5 × maxExodus0.7 = 0.28。
            float e = BureaucratizationRules.EntrepreneurExodus(0.8f, 0.5f);
            Assert.AreEqual(0.28f, e, Eps);

            // 流動性0なら去れない（死蔵されるが流出0）。
            Assert.AreEqual(0f, BureaucratizationRules.EntrepreneurExodus(1f, 0f), Eps);
        }

        /// <summary>革新力低下が成功を蝕み次の自壊を呼ぶ（成功が墓を掘る）。</summary>
        [Test]
        public void SelfUnderminingTick_革新力欠如が成功を蝕む()
        {
            // 革新力0.2＝(1-0.2)×selfUndermineRate0.06×dt2 = 0.096 を成功から引く。
            float s = BureaucratizationRules.SelfUnderminingTick(0.8f, 0.2f, 2f);
            Assert.AreEqual(0.704f, s, Eps);

            // 革新力満額1なら蝕まない（革新し続ければ成功は保てる）。
            float kept = BureaucratizationRules.SelfUnderminingTick(0.8f, 1f, 10f);
            Assert.AreEqual(0.8f, kept, Eps);
        }

        /// <summary>成功→官僚化→革新力喪失→成功喪失の自壊ループが回る。</summary>
        [Test]
        public void 自壊ループ_成功が官僚化を呼び革新力を奪い成功を蝕む()
        {
            float success = 0.9f;
            float bureaucracy = 0.2f;
            // 数tick回すと官僚化は上がり、革新力は落ち、成功は下がる。
            for (int i = 0; i < 10; i++)
            {
                bureaucracy = BureaucratizationRules.BureaucratizationTick(bureaucracy, success, 0.8f, 1f);
                float innov = BureaucratizationRules.InnovationCapacity(bureaucracy);
                success = BureaucratizationRules.SelfUnderminingTick(success, innov, 1f);
            }
            Assert.Greater(bureaucracy, 0.2f, "官僚化は進む");
            Assert.Less(success, 0.9f, "成功は蝕まれる");
            Assert.Less(BureaucratizationRules.InnovationCapacity(bureaucracy), 1f, "革新力は失われる");
        }

        /// <summary>古く大きい組織ほど硬直化する。改革コストは官僚化が深いほど非線形に高い。</summary>
        [Test]
        public void Ossification_と_RevitalizationCost()
        {
            // 硬直化＝官僚化×古さ。0.8×0.5 = 0.4。新しい組織(age0)は固まらない。
            Assert.AreEqual(0.4f, BureaucratizationRules.Ossification(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, BureaucratizationRules.Ossification(1f, 0f), Eps);

            // 改革コスト＝官僚化^2（非線形）。0.6^2 = 0.36。官僚化0なら改革不要0。
            Assert.AreEqual(0.36f, BureaucratizationRules.RevitalizationCost(0.6f), Eps);
            Assert.AreEqual(0f, BureaucratizationRules.RevitalizationCost(0f), Eps);
            // 深い官僚化ほど桁違いに高い（凸性）。
            Assert.Greater(BureaucratizationRules.RevitalizationCost(0.9f) - BureaucratizationRules.RevitalizationCost(0.6f),
                BureaucratizationRules.RevitalizationCost(0.6f) - BureaucratizationRules.RevitalizationCost(0.3f));
        }

        /// <summary>革新力が閾値を下回るとイノベーションの死。</summary>
        [Test]
        public void IsInnovationDead_閾値判定()
        {
            Assert.IsTrue(BureaucratizationRules.IsInnovationDead(0.05f, 0.1f));
            Assert.IsFalse(BureaucratizationRules.IsInnovationDead(0.2f, 0.1f));
            // 閾値ちょうどは死なない（厳密に下回ったときのみ）。
            Assert.IsFalse(BureaucratizationRules.IsInnovationDead(0.1f, 0.1f));
        }
    }
}
