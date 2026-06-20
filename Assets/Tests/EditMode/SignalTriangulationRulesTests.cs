using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 電波測位を固定する：方位精度（利得×信号・弱信号でぶれる）、三角測量（単点は0・広く散ると締まる）、
    /// 位置標定誤差（精度/質/距離で膨らむ）、無線封止突破（敵が黙ると鈍る）、放射源識別、即時性（短い放射は捉えにくい）、
    /// 射撃に使える標定の成否。既定Paramsで期待値を手計算固定し境界を担保。
    /// </summary>
    public class SignalTriangulationRulesTests
    {
        private static readonly SignalTriangulationParams P = SignalTriangulationParams.Default;
        // 利得2・受信点重み0.5・幾何重み0.6・距離誤差1・封止重み1・識別鋭さ1

        [Test]
        public void BearingAccuracy_GainTimesStrengthClamped()
        {
            Assert.AreEqual(0.8f, SignalTriangulationRules.BearingAccuracy(0.5f, 0.8f, P), 1e-4f); // 0.5×0.8×2
            Assert.AreEqual(1f, SignalTriangulationRules.BearingAccuracy(1f, 1f, P), 1e-4f);       // 1×1×2→clamp
            Assert.AreEqual(0f, SignalTriangulationRules.BearingAccuracy(0f, 0.8f, P), 1e-4f);     // 信号なし
        }

        [Test]
        public void TriangulationQuality_SingleReceiverIsZero()
        {
            // 単点＝方位線一本＝交点を結べない
            Assert.AreEqual(0f, SignalTriangulationRules.TriangulationQuality(1, 1f, P), 1e-4f);
            Assert.AreEqual(0f, SignalTriangulationRules.TriangulationQuality(0, 1f, P), 1e-4f);
        }

        [Test]
        public void TriangulationQuality_SpreadReceiversSharpen()
        {
            // 3点・幾何満点＝1.0（countTerm1×0.4＋1×1×0.6）
            Assert.AreEqual(1f, SignalTriangulationRules.TriangulationQuality(3, 1f, P), 1e-4f);
            // 3点でも幾何ゼロ（一直線）＝0.4どまり
            Assert.AreEqual(0.4f, SignalTriangulationRules.TriangulationQuality(3, 0f, P), 1e-4f);
            // 2点・幾何満点＝0.5（countTerm0.5×0.4＋0.5×1×0.6）
            Assert.AreEqual(0.5f, SignalTriangulationRules.TriangulationQuality(2, 1f, P), 1e-4f);
        }

        [Test]
        public void PositionError_LowAccuracyAndDistanceInflate()
        {
            // (1-0.8)(1-0.5)×1×1＝0.1
            Assert.AreEqual(0.1f, SignalTriangulationRules.PositionError(0.8f, 0.5f, 1f, P), 1e-4f);
            // 精度・質満点＝誤差ゼロ
            Assert.AreEqual(0f, SignalTriangulationRules.PositionError(1f, 1f, 1f, P), 1e-4f);
            // 至近（距離0）＝誤差ゼロ
            Assert.AreEqual(0f, SignalTriangulationRules.PositionError(0f, 0f, 0f, P), 1e-4f);
        }

        [Test]
        public void EmconPenetration_SilentEnemyHardensFix()
        {
            // 敵規律1・感度0.5＝0.5/(0.5+1)＝0.3333
            Assert.AreEqual(0.3333f, SignalTriangulationRules.EmconPenetration(1f, 0.5f, P), 1e-3f);
            // 敵が黙らない（規律0）＝感度頼みで突破満額
            Assert.AreEqual(1f, SignalTriangulationRules.EmconPenetration(0f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void EmitterIdentification_CharacteristicsTimesLibrary()
        {
            // 既定鋭さ1＝0.8×0.5＝0.4
            Assert.AreEqual(0.4f, SignalTriangulationRules.EmitterIdentification(0.8f, 0.5f, P), 1e-3f);
            // ライブラリに無ければ識別できない
            Assert.AreEqual(0f, SignalTriangulationRules.EmitterIdentification(0.8f, 0f, P), 1e-4f);
        }

        [Test]
        public void FixTimeliness_ShortEmissionHardToCatch()
        {
            // 処理満点でも持続0.2＝即時性0.2（一瞬の放射は捉えにくい）
            Assert.AreEqual(0.2f, SignalTriangulationRules.FixTimeliness(1f, 0.2f, P), 1e-4f);
            // 持続的に放射＝満額
            Assert.AreEqual(1f, SignalTriangulationRules.FixTimeliness(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void TargetingQuality_AndActionability()
        {
            // 誤差0.1・即時0.8＝(0.9)(0.8)＝0.72
            float tq = SignalTriangulationRules.TargetingQuality(0.1f, 0.8f, P);
            Assert.AreEqual(0.72f, tq, 1e-4f);
            Assert.IsTrue(SignalTriangulationRules.IsFixActionable(tq));   // 既定閾値0.5超え＝撃てる
            Assert.IsFalse(SignalTriangulationRules.IsFixActionable(0.4f)); // 標定が甘い＝撃てない
        }

        [Test]
        public void Narrative_SilentEnemyDefeatsFire_DispersedNetEnablesIt()
        {
            // 物語：敵が無線封止し短く放射する場面。
            // 単艦（受信点1）では方位線一本＝三角測量できず、封止で突破も鈍り、短い放射で即時性も低い。
            float soloTriang = SignalTriangulationRules.TriangulationQuality(1, 0.9f, P);
            float soloAcc = SignalTriangulationRules.BearingAccuracy(0.6f, 0.3f, P);  // 弱い放射
            float soloErr = SignalTriangulationRules.PositionError(soloAcc, soloTriang, 0.8f, P);
            float soloTimely = SignalTriangulationRules.FixTimeliness(0.7f, 0.2f, P); // 短い放射
            float soloTq = SignalTriangulationRules.TargetingQuality(soloErr, soloTimely, P);
            Assert.IsFalse(SignalTriangulationRules.IsFixActionable(soloTq), "単艦＝撃てる標定にならない");

            // 散開した逆探知網（4点・広い基線）＋十分な処理速度なら、同じ弱信号でも交点が締まり撃てる。
            float netTriang = SignalTriangulationRules.TriangulationQuality(4, 0.9f, P);
            float netErr = SignalTriangulationRules.PositionError(soloAcc, netTriang, 0.8f, P);
            float netTimely = SignalTriangulationRules.FixTimeliness(1f, 0.9f, P);
            float netTq = SignalTriangulationRules.TargetingQuality(netErr, netTimely, P);
            Assert.Greater(netTq, soloTq, "網のほうが標定が締まる");
            Assert.IsTrue(SignalTriangulationRules.IsFixActionable(netTq), "散開網＝撃てる標定");

            // 散開＝基線が広いほど三角測量の質は単艦より高い（受信点が散ると精度up）。
            Assert.Greater(netTriang, soloTriang);
        }
    }
}
