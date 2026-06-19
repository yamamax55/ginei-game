using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>傀儡政権（占領地の従属政府運営）の純ロジックテスト。既定Paramsで期待値を固定。</summary>
    public class PuppetGovernmentRulesTests
    {
        const float Tol = 1e-4f;
        const float PowTol = 1e-3f; // Pow/Sqrt を含む箇所

        /// <summary>正統性＝現地有力者の権威と民衆の負託の重み付き和。</summary>
        [Test]
        public void PuppetLegitimacy_有力者と民意の重み付き和()
        {
            // 0.6×0.5 + 0.4×0.5 = 0.5
            Assert.AreEqual(0.5f, PuppetGovernmentRules.PuppetLegitimacy(0.6f, 0.4f), Tol);

            // 有力者だけでは正統性が下がる（民意も要る）
            Assert.Less(PuppetGovernmentRules.PuppetLegitimacy(0.8f, 0.0f),
                PuppetGovernmentRules.PuppetLegitimacy(0.8f, 1.0f));
            // 入力はクランプされる
            Assert.AreEqual(1f, PuppetGovernmentRules.PuppetLegitimacy(2f, 2f), Tol);
        }

        /// <summary>見かけの自治＝主権/(主権＋宗主の介入)で介入が体裁を薄める。</summary>
        [Test]
        public void PerceivedAutonomy_介入が自治の体裁を薄める()
        {
            // 0.6/(0.6+0.3×1) = 0.666667
            Assert.AreEqual(0.6f / 0.9f, PuppetGovernmentRules.PerceivedAutonomy(0.6f, 0.3f), Tol);

            // 介入ゼロなら主権そのまま（体裁=1）
            Assert.AreEqual(1f, PuppetGovernmentRules.PerceivedAutonomy(0.6f, 0f), Tol);
            // 主権ゼロなら自治なし
            Assert.AreEqual(0f, PuppetGovernmentRules.PerceivedAutonomy(0f, 0.5f), Tol);
            // 介入が増えるほど体裁は薄れる（単調減少）
            Assert.Greater(PuppetGovernmentRules.PerceivedAutonomy(0.6f, 0.2f),
                PuppetGovernmentRules.PerceivedAutonomy(0.6f, 0.8f));
        }

        /// <summary>板挟み＝忠誠と支持の積が低いほど苦しい（両立しにくい）。</summary>
        [Test]
        public void CollaborationDilemma_忠誠と支持は両立しにくい()
        {
            // 1 - sqrt(0.6×0.4) = 1 - sqrt(0.24) = 0.510102
            Assert.AreEqual(0.510102f, PuppetGovernmentRules.CollaborationDilemma(0.6f, 0.4f), PowTol);

            // 両方が高い理想状態でのみ板挟みが緩む
            Assert.Less(PuppetGovernmentRules.CollaborationDilemma(1f, 1f),
                PuppetGovernmentRules.CollaborationDilemma(0.5f, 0.5f));
            // 片方ゼロなら板挟み最大
            Assert.AreEqual(1f, PuppetGovernmentRules.CollaborationDilemma(1f, 0f), Tol);
        }

        /// <summary>間接統治の効率＝正統性×自治の体裁を効率天井で抑えた値。</summary>
        [Test]
        public void IndirectRuleEfficiency_正統性と体裁で安く治まる()
        {
            // 0.6×0.7×0.9 = 0.378
            Assert.AreEqual(0.378f, PuppetGovernmentRules.IndirectRuleEfficiency(0.6f, 0.7f), Tol);

            // 正統性か体裁が無ければ間接統治の旨味は消える
            Assert.AreEqual(0f, PuppetGovernmentRules.IndirectRuleEfficiency(0f, 1f), Tol);
            Assert.AreEqual(0f, PuppetGovernmentRules.IndirectRuleEfficiency(1f, 0f), Tol);
            // 天井0.9で完全には達しない
            Assert.AreEqual(0.9f, PuppetGovernmentRules.IndirectRuleEfficiency(1f, 1f), Tol);
        }

        /// <summary>反抗圧＝民の不支持と民族主義が高まると宗主への反抗が増す。</summary>
        [Test]
        public void DefiancePressure_不支持と民族主義で反抗が燃える()
        {
            // (1-0.4)×0.5 + 0.6×0.5 = 0.6
            Assert.AreEqual(0.6f, PuppetGovernmentRules.DefiancePressure(0.4f, 0.6f), Tol);

            // 民の支持が厚く民族主義が低ければ反抗圧は小さい
            Assert.AreEqual(0f, PuppetGovernmentRules.DefiancePressure(1f, 0f), Tol);
            // 民族主義が高いほど反抗が増す
            Assert.Greater(PuppetGovernmentRules.DefiancePressure(0.4f, 1f),
                PuppetGovernmentRules.DefiancePressure(0.4f, 0f));
        }

        /// <summary>傀儡の安定＝正統性−板挟み−反抗圧で-1..1にクランプ。</summary>
        [Test]
        public void PuppetStability_正統性が支え板挟みと反抗が削る()
        {
            // 0.6 - 0.5×0.5 - 0.5×0.5 = 0.1
            Assert.AreEqual(0.1f, PuppetGovernmentRules.PuppetStability(0.6f, 0.5f, 0.5f), Tol);

            // 民にも宗主にも見放され反抗が高いと負（崩壊へ）
            Assert.AreEqual(-0.6f, PuppetGovernmentRules.PuppetStability(0.2f, 0.8f, 0.8f), Tol);
            // 下限-1でクランプ
            Assert.AreEqual(-1f, PuppetGovernmentRules.PuppetStability(0f, 1f, 1f), Tol);
        }

        /// <summary>宗主の統制＝忠誠×後ろ盾の幾何平均を反抗圧で侵食した値。</summary>
        [Test]
        public void OverlordControl_忠誠と駐留後ろ盾を反抗圧が侵食()
        {
            // sqrt(0.6×0.5)×(1-0.4×0.5) = 0.547723×0.8 = 0.438178
            Assert.AreEqual(0.438178f, PuppetGovernmentRules.OverlordControl(0.6f, 0.5f, 0.4f), PowTol);

            // 後ろ盾ゼロなら統制は崩れる（積）
            Assert.AreEqual(0f, PuppetGovernmentRules.OverlordControl(1f, 0f, 0f), Tol);
            // 反抗圧が高いほど統制が削られる
            Assert.Greater(PuppetGovernmentRules.OverlordControl(0.6f, 0.5f, 0f),
                PuppetGovernmentRules.OverlordControl(0.6f, 0.5f, 1f));
        }

        /// <summary>維持判定＝安定＋統制が閾値を超えれば傀儡は持ちこたえる。</summary>
        [Test]
        public void WillPuppetHold_安定と統制の総合余力で崩壊判定()
        {
            // 0.1+0.4 = 0.5 >= 既定閾値0.2 → 維持
            Assert.IsTrue(PuppetGovernmentRules.WillPuppetHold(0.1f, 0.4f));
            // 安定が崩れても強い後ろ盾が支えれば倒れない
            Assert.IsTrue(PuppetGovernmentRules.WillPuppetHold(-0.3f, 0.6f));
            // 安定も統制も乏しければ崩壊
            Assert.IsFalse(PuppetGovernmentRules.WillPuppetHold(-0.6f, 0.1f));
        }

        /// <summary>物語＝忠実な傀儡が民を失い反抗が燃えて崩壊するまで。</summary>
        [Test]
        public void Narrative_忠実な傀儡が民を失い反抗で崩壊する()
        {
            var p = PuppetGovernmentParams.Default;

            // 初期：現地有力者の権威と民意が揃い、宗主への忠誠も民の支持も厚い安定した傀儡
            float legitEarly = PuppetGovernmentRules.PuppetLegitimacy(0.7f, 0.7f, p);
            float dilemmaEarly = PuppetGovernmentRules.CollaborationDilemma(0.8f, 0.7f, p);
            float defianceEarly = PuppetGovernmentRules.DefiancePressure(0.7f, 0.2f, p);
            float stabilityEarly = PuppetGovernmentRules.PuppetStability(legitEarly, dilemmaEarly, defianceEarly, p);
            float controlEarly = PuppetGovernmentRules.OverlordControl(0.8f, 0.7f, defianceEarly, p);
            Assert.IsTrue(PuppetGovernmentRules.WillPuppetHold(stabilityEarly, controlEarly),
                "初期は安定した傀儡として持ちこたえる");

            // 末期：宗主に忠実すぎて民意と支持を失い、民族主義が燃え上がり、駐留も細る
            float legitLate = PuppetGovernmentRules.PuppetLegitimacy(0.6f, 0.1f, p);
            float dilemmaLate = PuppetGovernmentRules.CollaborationDilemma(0.9f, 0.15f, p);
            float defianceLate = PuppetGovernmentRules.DefiancePressure(0.15f, 0.9f, p);
            float stabilityLate = PuppetGovernmentRules.PuppetStability(legitLate, dilemmaLate, defianceLate, p);
            float controlLate = PuppetGovernmentRules.OverlordControl(0.9f, 0.2f, defianceLate, p);

            // 板挟みも反抗も深まり、安定と統制が崩れて傀儡は持たない
            Assert.Greater(dilemmaLate, dilemmaEarly, "民を切って忠誠に偏るほど板挟みが深まる");
            Assert.Greater(defianceLate, defianceEarly, "民を失い民族主義が燃えると反抗圧が増す");
            Assert.Less(stabilityLate, stabilityEarly, "安定は崩れていく");
            Assert.IsFalse(PuppetGovernmentRules.WillPuppetHold(stabilityLate, controlLate),
                "末期は傀儡が崩壊する");
        }
    }
}
