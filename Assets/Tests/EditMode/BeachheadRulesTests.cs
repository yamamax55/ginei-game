using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>橋頭堡（上陸拠点）確保・拡張の純ロジック検証。
    /// 第一波生存→確立→反撃圧力→防衛→増援/補給→拡張→突破口の綱引きを既定Paramsで固定。</summary>
    public class BeachheadRulesTests
    {
        const float Eps = 0.0001f;
        const float SqrtEps = 0.001f; // Sqrt を含む箇所

        /// <summary>第一波生存＝上陸戦力×(1−敵防御火力)。</summary>
        [Test]
        public void FirstWaveSurvival_敵火力で削られた残りが浜に届く()
        {
            Assert.AreEqual(0.56f, BeachheadRules.FirstWaveSurvival(0.8f, 0.3f), Eps); // 0.8×0.7
            Assert.AreEqual(0f, BeachheadRules.FirstWaveSurvival(1f, 1f), Eps);        // 全火力＝全滅
            Assert.AreEqual(1f, BeachheadRules.FirstWaveSurvival(1f, 0f), Eps);        // 無抵抗＝無傷
            Assert.AreEqual(0f, BeachheadRules.FirstWaveSurvival(0f, 0.3f), Eps);      // 上陸なし
        }

        /// <summary>橋頭堡確立＝第一波生存×確保面積（足場無しでは確立しない）。</summary>
        [Test]
        public void BeachheadEstablishment_生存と足場の積()
        {
            Assert.AreEqual(0.392f, BeachheadRules.BeachheadEstablishment(0.56f, 0.7f), Eps); // 0.56×0.7
            Assert.AreEqual(0f, BeachheadRules.BeachheadEstablishment(0.56f, 0f), Eps);       // 足場0
        }

        /// <summary>水際反撃の圧力＝敵予備×反撃速度。</summary>
        [Test]
        public void CounterattackPressure_予備と即応の積()
        {
            Assert.AreEqual(0.3f, BeachheadRules.CounterattackPressure(0.6f, 0.5f), Eps); // 0.6×0.5
            Assert.AreEqual(0f, BeachheadRules.CounterattackPressure(0f, 0.5f), Eps);     // 予備なし
            Assert.AreEqual(0.5f, BeachheadRules.CounterattackPressure(2f, 0.5f), Eps);   // 過大予備はクランプ(1×0.5)
        }

        /// <summary>防衛＝確立/(確立+反撃)。拮抗で0.5・両者0で0。</summary>
        [Test]
        public void BeachheadDefense_確立と反撃の綱引き()
        {
            Assert.AreEqual(0.5f, BeachheadRules.BeachheadDefense(0.4f, 0.4f), Eps); // 拮抗
            Assert.AreEqual(0.566473f, BeachheadRules.BeachheadDefense(0.392f, 0.3f), Eps); // 0.392/0.692
            Assert.AreEqual(0f, BeachheadRules.BeachheadDefense(0f, 0f), Eps);      // 何も無い＝0
        }

        /// <summary>増援流入＝防衛×輸送力（守れている間だけ後続が入る）。</summary>
        [Test]
        public void ReinforcementInflow_守れている橋頭堡へ流入()
        {
            Assert.AreEqual(0.4f, BeachheadRules.ReinforcementInflow(0.5f, 0.8f), Eps); // 0.5×0.8
            Assert.AreEqual(0f, BeachheadRules.ReinforcementInflow(0f, 0.8f), Eps);     // 防衛崩壊＝流入なし
        }

        /// <summary>補給集積＝防衛×物資処理。</summary>
        [Test]
        public void SupplyBuildup_守れている間だけ揚陸できる()
        {
            Assert.AreEqual(0.3f, BeachheadRules.SupplyBuildup(0.5f, 0.6f), Eps); // 0.5×0.6
            Assert.AreEqual(0f, BeachheadRules.SupplyBuildup(0f, 0.6f), Eps);     // 防衛崩壊＝補給なし
        }

        /// <summary>橋頭堡拡張＝増援×補給の幾何平均（どちらか欠けると伸びない）。</summary>
        [Test]
        public void BeachheadExpansion_人と物資の相乗()
        {
            Assert.AreEqual(0.4f, BeachheadRules.BeachheadExpansion(0.4f, 0.4f), SqrtEps); // sqrt(0.16)
            Assert.AreEqual(0.346410f, BeachheadRules.BeachheadExpansion(0.4f, 0.3f), SqrtEps); // sqrt(0.12)
            Assert.AreEqual(0f, BeachheadRules.BeachheadExpansion(0.5f, 0f), SqrtEps);    // 補給0＝伸びない
        }

        /// <summary>突破口準備＝実効拡張(拡張−0.5×反撃)が閾値以上か。</summary>
        [Test]
        public void BreakoutReadiness_反撃に屈しなければ突破口が開く()
        {
            Assert.IsTrue(BeachheadRules.BreakoutReadiness(0.7f, 0.2f));  // 0.7−0.1=0.6≥0.5
            Assert.IsFalse(BeachheadRules.BreakoutReadiness(0.6f, 0.4f)); // 0.6−0.2=0.4<0.5
            Assert.IsFalse(BeachheadRules.BreakoutReadiness(0.4f, 0f));   // 拡張不足
        }

        /// <summary>物語：水際を突破した強襲上陸が橋頭堡を確保し突破口へ発展する一連。</summary>
        [Test]
        public void 物語_強襲上陸が橋頭堡を確保し突破口へ発展する()
        {
            // 上陸戦力満載・敵防御火力は薄い(0.1)
            float wave = BeachheadRules.FirstWaveSurvival(1f, 0.1f);
            Assert.AreEqual(0.9f, wave, Eps); // 1×0.9

            // 広い足場(0.9)を確保
            float est = BeachheadRules.BeachheadEstablishment(wave, 0.9f);
            Assert.AreEqual(0.81f, est, Eps); // 0.9×0.9

            // 敵は予備0.4を速度0.5で反撃
            float counter = BeachheadRules.CounterattackPressure(0.4f, 0.5f);
            Assert.AreEqual(0.2f, counter, Eps);

            // 確立が反撃を上回り踏みとどまる
            float defense = BeachheadRules.BeachheadDefense(est, counter);
            Assert.AreEqual(0.801980f, defense, Eps); // 0.81/1.01

            // 増援と補給が流入
            float inflow = BeachheadRules.ReinforcementInflow(defense, 0.9f);
            float supply = BeachheadRules.SupplyBuildup(defense, 0.8f);
            Assert.AreEqual(0.721782f, inflow, Eps); // 0.801980×0.9
            Assert.AreEqual(0.641584f, supply, Eps); // 0.801980×0.8

            // 橋頭堡が拡張し突破口が開く
            float expansion = BeachheadRules.BeachheadExpansion(inflow, supply);
            Assert.AreEqual(0.680502f, expansion, SqrtEps); // sqrt(0.463082)
            Assert.IsTrue(BeachheadRules.BreakoutReadiness(expansion, counter)); // 0.680502−0.1=0.580502≥0.5
        }
    }
}
