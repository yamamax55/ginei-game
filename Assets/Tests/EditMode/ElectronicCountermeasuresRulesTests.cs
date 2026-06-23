using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// ElectronicCountermeasuresRules（ECM/ECCM の応酬）の EditMode テスト。
    /// 既定 Params（妨害1/対妨害1/欺瞞1/エスカレーション感度1/優越閾値0.2）での期待値を手計算で固定。
    /// </summary>
    public class ElectronicCountermeasuresRulesTests
    {
        const float Eps = 1e-4f;     // 通常
        const float EpsPow = 1e-3f;  // Sqrt 箇所

        [Test]
        public void JammingStrength_PowerTimesCoverage()
        {
            // 0.8 × 0.5 × 1 = 0.4
            Assert.AreEqual(0.4f, ElectronicCountermeasuresRules.JammingStrength(0.8f, 0.5f), Eps);
        }

        [Test]
        public void CounterJamming_TechTimesAgility()
        {
            // 0.6 × 0.5 × 1 = 0.3
            Assert.AreEqual(0.3f, ElectronicCountermeasuresRules.CounterJamming(0.6f, 0.5f), Eps);
        }

        [Test]
        public void JammingEffectiveness_Ratio()
        {
            // 0.6 / (0.6 + 0.2) = 0.75
            Assert.AreEqual(0.75f, ElectronicCountermeasuresRules.JammingEffectiveness(0.6f, 0.2f), Eps);
            // 両者ゼロは 0（妨害も対妨害も無い）
            Assert.AreEqual(0f, ElectronicCountermeasuresRules.JammingEffectiveness(0f, 0f), Eps);
            // 対妨害ゼロは完全有効 1
            Assert.AreEqual(1f, ElectronicCountermeasuresRules.JammingEffectiveness(0.5f, 0f), Eps);
        }

        [Test]
        public void DeceptionJamming_TargetsTimesMimicry()
        {
            // 0.5 × 0.4 × 1 = 0.2
            Assert.AreEqual(0.2f, ElectronicCountermeasuresRules.DeceptionJamming(0.5f, 0.4f), Eps);
        }

        [Test]
        public void FrequencyHopEvasion_Ratio()
        {
            // 0.7 / (0.7 + 0.3) = 0.7
            Assert.AreEqual(0.7f, ElectronicCountermeasuresRules.FrequencyHopEvasion(0.7f, 0.3f), Eps);
            // 両者ゼロは 0
            Assert.AreEqual(0f, ElectronicCountermeasuresRules.FrequencyHopEvasion(0f, 0f), Eps);
        }

        [Test]
        public void ElectronicSuperiority_EffectTimesEvasionMinusCounter()
        {
            // 0.8 × 0.5 − 0.3 = 0.1
            Assert.AreEqual(0.1f, ElectronicCountermeasuresRules.ElectronicSuperiority(0.8f, 0.3f, 0.5f), Eps);
            // 強い対妨害で負（劣勢）：0.3 × 0.4 − 0.9 = −0.78
            Assert.AreEqual(-0.78f, ElectronicCountermeasuresRules.ElectronicSuperiority(0.3f, 0.9f, 0.4f), Eps);
            // 下限 −1 にクランプ：0 − 1 = −1
            Assert.AreEqual(-1f, ElectronicCountermeasuresRules.ElectronicSuperiority(0f, 1f, 0.5f), Eps);
        }

        [Test]
        public void EscalationSpiral_GeometricMean()
        {
            // sqrt(0.4 × 0.9) = sqrt(0.36) = 0.6
            Assert.AreEqual(0.6f, ElectronicCountermeasuresRules.EscalationSpiral(0.4f, 0.9f), EpsPow);
            // 片方ゼロなら螺旋は止まる：sqrt(0) = 0
            Assert.AreEqual(0f, ElectronicCountermeasuresRules.EscalationSpiral(0f, 1f), EpsPow);
        }

        [Test]
        public void IsElectronicallyDominant_DefaultThreshold()
        {
            // 既定閾値 0.2。0.3 ≥ 0.2 → true、0.1 < 0.2 → false
            Assert.IsTrue(ElectronicCountermeasuresRules.IsElectronicallyDominant(0.3f));
            Assert.IsFalse(ElectronicCountermeasuresRules.IsElectronicallyDominant(0.1f));
            // 明示閾値版
            Assert.IsTrue(ElectronicCountermeasuresRules.IsElectronicallyDominant(0.5f, 0.5f));
            Assert.IsFalse(ElectronicCountermeasuresRules.IsElectronicallyDominant(0.49f, 0.5f));
        }

        [Test]
        public void Inputs_AreClamped()
        {
            // 範囲外入力は clamp されて爆発しない
            Assert.AreEqual(1f, ElectronicCountermeasuresRules.JammingStrength(5f, 9f), Eps);
            Assert.AreEqual(0f, ElectronicCountermeasuresRules.CounterJamming(-3f, 0.5f), Eps);
            Assert.AreEqual(1f, ElectronicCountermeasuresRules.JammingEffectiveness(9f, 0f), Eps);
            // 優越は符号付き −1..1 にクランプ
            float sup = ElectronicCountermeasuresRules.ElectronicSuperiority(9f, 9f, 9f);
            Assert.GreaterOrEqual(sup, -1f);
            Assert.LessOrEqual(sup, 1f);
        }

        [Test]
        public void ParamsCtor_ClampsAllFields()
        {
            var p = new ElectronicCountermeasuresParams(5f, -1f, 9f, -2f, 5f);
            Assert.AreEqual(1f, p.jammingScale, Eps);
            Assert.AreEqual(0f, p.counterScale, Eps);
            Assert.AreEqual(1f, p.deceptionScale, Eps);
            Assert.AreEqual(0f, p.escalationSensitivity, Eps);
            Assert.AreEqual(1f, p.dominanceThreshold, Eps); // 符号付き閾値は −1..1
        }

        /// <summary>
        /// 物語テスト：いたちごっこ。最初は自軍 ECM が圧倒（広域強出力＋高速ホッピング）して電子的に優越するが、
        /// 敵が ECCM（対妨害技術＋周波数追従）に投資して対抗すると優越が崩れ、双方の投資でエスカレーションが激化する。
        /// </summary>
        [Test]
        public void Narrative_CatAndMouseEscalation()
        {
            // 第1幕：自軍が強い妨害（出力0.9×範囲0.8）＋速いホッピング、敵 ECCM はまだ弱い
            float jamming = ElectronicCountermeasuresRules.JammingStrength(0.9f, 0.8f); // 0.72
            float weakCounter = ElectronicCountermeasuresRules.CounterJamming(0.2f, 0.2f); // 0.04
            float effect = ElectronicCountermeasuresRules.JammingEffectiveness(jamming, weakCounter); // 0.72/0.76
            float evasion = ElectronicCountermeasuresRules.FrequencyHopEvasion(0.8f, 0.2f); // 0.8
            float sup1 = ElectronicCountermeasuresRules.ElectronicSuperiority(effect, weakCounter, evasion);
            Assert.IsTrue(ElectronicCountermeasuresRules.IsElectronicallyDominant(sup1),
                "弱い敵 ECCM 相手なら電子的に優越するはず");

            // 第2幕：敵が ECCM に投資（高技術＋高速追従）。対妨害が跳ね上がり優越が崩れる
            float strongCounter = ElectronicCountermeasuresRules.CounterJamming(0.9f, 0.9f); // 0.81
            float effect2 = ElectronicCountermeasuresRules.JammingEffectiveness(jamming, strongCounter);
            float evasion2 = ElectronicCountermeasuresRules.FrequencyHopEvasion(0.8f, 0.85f);
            float sup2 = ElectronicCountermeasuresRules.ElectronicSuperiority(effect2, strongCounter, evasion2);
            Assert.Less(sup2, sup1, "敵の ECCM 投資で優越は後退するはず");
            Assert.IsFalse(ElectronicCountermeasuresRules.IsElectronicallyDominant(sup2),
                "強い敵 ECCM 相手なら優越を失うはず");

            // 第3幕：双方が投資し続けるほどいたちごっこは激化する
            float lowSpiral = ElectronicCountermeasuresRules.EscalationSpiral(0.2f, 0.2f);
            float highSpiral = ElectronicCountermeasuresRules.EscalationSpiral(0.9f, 0.9f);
            Assert.Less(lowSpiral, highSpiral, "双方の投資が増えるほどエスカレーションは激化するはず");
        }
    }
}
