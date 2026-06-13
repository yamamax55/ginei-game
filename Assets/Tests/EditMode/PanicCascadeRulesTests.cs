using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>恐慌の空間カスケード（CRWD-4 #1823・ル・ボン参考）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class PanicCascadeRulesTests
    {
        const float Eps = 0.0001f;
        const float EpsLoose = 0.001f;   // 除算（距離減衰・防火帯）箇所はやや緩める

        /// <summary>発火源パニック＝士気崩壊×1（既定seedScale）。</summary>
        [Test]
        public void PanicSeed_士気崩壊にスケールを掛ける()
        {
            Assert.AreEqual(0.6f, PanicCascadeRules.PanicSeed(0.6f), Eps);
            Assert.AreEqual(1f, PanicCascadeRules.PanicSeed(2f), Eps);   // クランプ
            Assert.AreEqual(0f, PanicCascadeRules.PanicSeed(0f), Eps);
        }

        /// <summary>距離減衰＝1/(1+kd)。近いほど1、遠いほど0へ。</summary>
        [Test]
        public void DistanceDecay_距離で減衰する()
        {
            Assert.AreEqual(1f, PanicCascadeRules.DistanceDecay(0f), Eps);      // 接触＝素通し
            Assert.AreEqual(0.5f, PanicCascadeRules.DistanceDecay(1f), EpsLoose); // 1/(1+1)
            Assert.AreEqual(0.25f, PanicCascadeRules.DistanceDecay(3f), EpsLoose); // 1/(1+3)
            // 単調減少
            Assert.Greater(PanicCascadeRules.DistanceDecay(1f), PanicCascadeRules.DistanceDecay(5f));
        }

        /// <summary>隣接伝染＝パニック×距離減衰×(1−隣の士気)。近くて隣が脆いほど強い。</summary>
        [Test]
        public void ContagionToNeighbor_近くて脆い隣ほど伝染する()
        {
            // 0.8 × DistanceDecay(1)=0.5 × (1-0.2)=0.8 = 0.32
            Assert.AreEqual(0.32f, PanicCascadeRules.ContagionToNeighbor(0.8f, 1f, 0.2f), EpsLoose);
            // 隣が高士気なら伝染しにくい（単調）
            float weakNeighbor = PanicCascadeRules.ContagionToNeighbor(0.8f, 1f, 0.1f);
            float strongNeighbor = PanicCascadeRules.ContagionToNeighbor(0.8f, 1f, 0.9f);
            Assert.Greater(weakNeighbor, strongNeighbor, "士気の低い隣ほど崩れる");
            // 隣が完全士気なら伝染ゼロ
            Assert.AreEqual(0f, PanicCascadeRules.ContagionToNeighbor(1f, 0f, 1f), Eps);
        }

        /// <summary>威光抑制＝伝染×(1−威光)。名将がいると伝播が殺される。</summary>
        [Test]
        public void PrestigeSuppression_威光が伝播を抑える()
        {
            // 0.5 × (1-0.6) = 0.2
            Assert.AreEqual(0.2f, PanicCascadeRules.PrestigeSuppression(0.5f, 0.6f), Eps);
            // 威光なしなら素通し
            Assert.AreEqual(0.5f, PanicCascadeRules.PrestigeSuppression(0.5f, 0f), Eps);
            // 完全な威光は伝播を断つ
            Assert.AreEqual(0f, PanicCascadeRules.PrestigeSuppression(1f, 1f), Eps);
        }

        /// <summary>連鎖長＝初期パニック×8×(1−結束)。結束が連鎖を止める。</summary>
        [Test]
        public void RoutChainLength_結束が将棋倒しを止める()
        {
            // 0.5 × 8 × (1-0.25) = 3.0
            Assert.AreEqual(3.0f, PanicCascadeRules.RoutChainLength(0.5f, 0.25f), Eps);
            // 結束1なら連鎖ゼロ＝戦線が踏みとどまる
            Assert.AreEqual(0f, PanicCascadeRules.RoutChainLength(1f, 1f), Eps);
            // 結束が弱いほど長く倒れる（単調）
            Assert.Greater(PanicCascadeRules.RoutChainLength(0.8f, 0.1f),
                           PanicCascadeRules.RoutChainLength(0.8f, 0.7f));
        }

        /// <summary>防火帯＝1/(1+gap/2)。間隔が広いと伝染が止まる。</summary>
        [Test]
        public void FirebreakEffect_間隔が広いと飛び火しない()
        {
            Assert.AreEqual(1f, PanicCascadeRules.FirebreakEffect(0f), Eps);       // 密集＝素通し
            Assert.AreEqual(0.5f, PanicCascadeRules.FirebreakEffect(2f), EpsLoose); // 基準間隔で半減
            // 広いほど止まる（単調減少）
            Assert.Greater(PanicCascadeRules.FirebreakEffect(1f), PanicCascadeRules.FirebreakEffect(6f));
        }

        /// <summary>敗走トリガー＝閾値超え かつ roll超えで敗走（決定論）。</summary>
        [Test]
        public void PanicTrigger_閾値とrollで敗走に転じる()
        {
            // パニック0.8 > 閾値0.5 かつ 0.8 > roll0.3 → 敗走
            Assert.IsTrue(PanicCascadeRules.PanicTrigger(0.8f, 0.3f, 0.5f));
            // 閾値以下なら踏みとどまる
            Assert.IsFalse(PanicCascadeRules.PanicTrigger(0.4f, 0.1f, 0.5f));
            // 閾値は超えても roll が高ければ耐える
            Assert.IsFalse(PanicCascadeRules.PanicTrigger(0.6f, 0.9f, 0.5f));
        }

        /// <summary>戦線崩壊判定＝カスケード規模が閾値超え。</summary>
        [Test]
        public void IsLineCollapsing_規模が閾値を超えると崩壊()
        {
            Assert.IsTrue(PanicCascadeRules.IsLineCollapsing(0.8f, 0.5f));
            Assert.IsFalse(PanicCascadeRules.IsLineCollapsing(0.3f, 0.5f));
        }

        /// <summary>CascadeStep の配列null/空安全。</summary>
        [Test]
        public void CascadeStep_配列がnullや空でも安全()
        {
            Assert.AreEqual(0f, PanicCascadeRules.CascadeStep(null, null, 0.5f), Eps);
            Assert.AreEqual(0f, PanicCascadeRules.CascadeStep(new float[0], new float[0], 0.5f), Eps);
            // distances が null でも panicLevels だけで動く（距離0扱い＝発火源を保つ）
            Assert.AreEqual(0.7f, PanicCascadeRules.CascadeStep(new float[] { 0.7f, 0.1f }, null, 1f), Eps);
        }

        /// <summary>
        /// 物語テスト：一部隊の敗走が近接部隊へ伝染して将棋倒しになるが、
        /// 名将の威光や部隊間隔（防火帯）が伝播を止める。
        /// </summary>
        [Test]
        public void 物語_敗走の伝染は威光と間隔で止まる()
        {
            // 端の一部隊が士気崩壊して敗走の種になる。
            float seed = PanicCascadeRules.PanicSeed(1f);
            Assert.AreEqual(1f, seed, Eps);

            // 威光なしの戦線：発火源0.8、近接(0)と遠方(1)の2部隊へ伝播。
            // i=0: ContagionToNeighbor(0.8,0,0)=0.8、抑制なしで0.8。i=1: 0.8*0.5=0.4。最大0.8を保持。
            float[] panic = { 0.8f, 0.1f };
            float[] dist = { 0f, 1f };
            float noPrestige = PanicCascadeRules.CascadeStep(panic, dist, 0f);
            Assert.AreEqual(0.8f, noPrestige, EpsLoose, "威光なしでは戦線が将棋倒しに巻き込まれる");
            Assert.IsTrue(PanicCascadeRules.IsLineCollapsing(noPrestige, 0.6f), "崩壊しつつある");

            // 名将の威光は伝播そのものを殺す＝隣への飛び火（伝染）が弱まる。
            float spreadNoPrestige = PanicCascadeRules.PrestigeSuppression(0.8f, 0f);   // 0.8
            float spreadWithPrestige = PanicCascadeRules.PrestigeSuppression(0.8f, 0.8f); // 0.16
            Assert.Less(spreadWithPrestige, spreadNoPrestige, "名将の威光が伝播を抑える");

            // 結束した戦線は連鎖が短く済む＝将棋倒しが止まる。
            float looseLine = PanicCascadeRules.RoutChainLength(0.8f, 0.1f);
            float tightLine = PanicCascadeRules.RoutChainLength(0.8f, 0.9f);
            Assert.Greater(looseLine, tightLine, "結束した戦線ほど連鎖が止まる");

            // 部隊間隔を広く取れば（防火帯）飛び火が弱まる。
            float closeGap = PanicCascadeRules.FirebreakEffect(0.5f);
            float wideGap = PanicCascadeRules.FirebreakEffect(8f);
            Assert.Greater(closeGap, wideGap, "間隔が広いほど伝染が止まる");
        }
    }
}
