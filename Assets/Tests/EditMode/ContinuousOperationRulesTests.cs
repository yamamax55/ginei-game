using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 連続運転の硬直を固定する（#1115）：連続性が高いほど最低稼働率は底上げされゼロまで絞れず、停止コスト・
    /// 再起動時間が跳ねる。需要が落ちても最低稼働ぶんは過剰生産になり、止めるより回す方が安い領域では操業が続く
    /// ＝戦時硬直。戦時ロックインは連続性×継続時間で積み上がる。境界・既定Params具体値で担保。
    /// </summary>
    public class ContinuousOperationRulesTests
    {
        private static readonly ContinuousOperationParams P = ContinuousOperationParams.Default;
        // 最低稼働0.7・停止コスト100・再起動50・ロックイン0.02

        [Test]
        public void TurndownFloor_ScalesWithContinuity()
        {
            // バッチ炉はゼロまで絞れる
            Assert.AreEqual(0f, ContinuousOperationRules.TurndownFloor(0f, P), 1e-5f);
            // 連続炉は最低稼働まで（高炉は止められない）
            Assert.AreEqual(0.7f, ContinuousOperationRules.TurndownFloor(1f, P), 1e-5f);
            // 中間は比例
            Assert.AreEqual(0.35f, ContinuousOperationRules.TurndownFloor(0.5f, P), 1e-5f);
        }

        [Test]
        public void ShutdownCost_NonlinearInContinuity()
        {
            // 連続性²×100：バッチ炉は0、連続炉は100
            Assert.AreEqual(0f, ContinuousOperationRules.ShutdownCost(0f, P), 1e-5f);
            Assert.AreEqual(100f, ContinuousOperationRules.ShutdownCost(1f, P), 1e-5f);
            // 0.5² = 0.25 → 25（非線形＝半分の連続性でも止め起こしは四半分で済む）
            Assert.AreEqual(25f, ContinuousOperationRules.ShutdownCost(0.5f, P), 1e-5f);
        }

        [Test]
        public void RestartTime_ScalesWithContinuity()
        {
            Assert.AreEqual(0f, ContinuousOperationRules.RestartTime(0f, P), 1e-5f);
            Assert.AreEqual(50f, ContinuousOperationRules.RestartTime(1f, P), 1e-5f);
        }

        [Test]
        public void OverproductionFromRigidity_FloorKeepsRunning()
        {
            // capacity=100・最低稼働0.7＝最低70は作り続ける。需要40 → 70-40=30 が在庫の山
            Assert.AreEqual(30f, ContinuousOperationRules.OverproductionFromRigidity(40f, 0.7f, 100f), 1e-5f);
            // 需要が最低稼働ライン以上なら過剰なし
            Assert.AreEqual(0f, ContinuousOperationRules.OverproductionFromRigidity(80f, 0.7f, 100f), 1e-5f);
            // 需要ゼロでも最低稼働ぶん丸ごと過剰（止められない）
            Assert.AreEqual(70f, ContinuousOperationRules.OverproductionFromRigidity(0f, 0.7f, 100f), 1e-5f);
        }

        [Test]
        public void OperatingDecision_KeepRunningWhenStoppingIsDearer()
        {
            // 需要0.2＜最低稼働0.7。空回し浪費=0.5。止めるコスト（停止100＋再起動50=150）≫浪費 → 回し続ける
            Assert.IsTrue(ContinuousOperationRules.OperatingDecision(0.2f, 0.7f, 100f, 50f));
            // 需要が最低稼働を上回れば当然操業
            Assert.IsTrue(ContinuousOperationRules.OperatingDecision(0.9f, 0.7f, 100f, 50f));
        }

        [Test]
        public void OperatingDecision_StopWhenCheapToRestart()
        {
            // バッチ炉に近い＝停止コスト・再起動ともゼロ。需要0で空回しするより止めた方が安い
            Assert.IsFalse(ContinuousOperationRules.OperatingDecision(0f, 0.5f, 0f, 0f));
        }

        [Test]
        public void WartimeLockIn_AccumulatesWithContinuityAndDuration()
        {
            // 連続性0＝決してロックインしない（バッチ炉はいつでも止められる）
            Assert.AreEqual(0f, ContinuousOperationRules.WartimeLockIn(0f, 9999f, P), 1e-5f);
            // 連続性1×継続時間10×0.02 = 0.2
            Assert.AreEqual(0.2f, ContinuousOperationRules.WartimeLockIn(1f, 10f, P), 1e-5f);
            // 長期戦で完全ロックイン（1で頭打ち＝戦争が終わるまで止まらない）
            Assert.AreEqual(1f, ContinuousOperationRules.WartimeLockIn(1f, 9999f, P), 1e-5f);
        }
    }
}
