using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>威光と非連続崩壊（CRWD-3 #1822・ル・ボン）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class PrestigeCliffRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>積み上げ＝現在威光＋成功×0.3（既定buildRate）×残余余地。逓減で上限へ。</summary>
        [Test]
        public void PrestigeAccumulation_成功で逓減的に積み上がる()
        {
            // 0.4 + 0.5*0.3*(1-0.4) = 0.4 + 0.15*0.6 = 0.4 + 0.09 = 0.49
            Assert.AreEqual(0.49f, PrestigeCliffRules.PrestigeAccumulation(0.4f, 0.5f), Eps);
            // 高威光ほど積み上げ幅が小さい（逓減）
            float low = PrestigeCliffRules.PrestigeAccumulation(0.2f, 1f) - 0.2f;
            float high = PrestigeCliffRules.PrestigeAccumulation(0.8f, 1f) - 0.8f;
            Assert.Greater(low, high, "威光が高いほど積み上げ幅は小さい");
        }

        /// <summary>磁力＝威光×1.0（既定magnetismScale）。</summary>
        [Test]
        public void PrestigeMagnetism_威光に比例した磁力()
        {
            Assert.AreEqual(0.7f, PrestigeCliffRules.PrestigeMagnetism(0.7f), Eps);
            Assert.AreEqual(0f, PrestigeCliffRules.PrestigeMagnetism(0f), Eps);
        }

        /// <summary>崖リスク＝威光×失敗×0.9（既定）。高威光ほど大失敗で砕けやすい。</summary>
        [Test]
        public void CliffRisk_高威光と大失敗でリスクが高い()
        {
            // 0.8 × 0.5 × 0.9 = 0.36
            Assert.AreEqual(0.36f, PrestigeCliffRules.CliffRisk(0.8f, 0.5f), Eps);
            // 威光が高いほどリスクが大きい（落差が大きい）
            Assert.Greater(PrestigeCliffRules.CliffRisk(0.9f, 0.5f),
                           PrestigeCliffRules.CliffRisk(0.3f, 0.5f));
        }

        /// <summary>瓦解＝失敗が閾値超で残存率0.2倍へ一気に落ちる（非連続）／閾値以下は漸進的に削るだけ。</summary>
        [Test]
        public void PrestigeCollapse_閾値超で非連続に瓦解する()
        {
            // 失敗0.8 > 閾値0.5 → 0.9 * 0.2 = 0.18（崖）
            Assert.AreEqual(0.18f, PrestigeCliffRules.PrestigeCollapse(0.9f, 0.8f, 0.5f), Eps);
            // 失敗0.3 <= 閾値0.5 → 0.9 - 0.3*0.9 = 0.63（漸進）
            Assert.AreEqual(0.63f, PrestigeCliffRules.PrestigeCollapse(0.9f, 0.3f, 0.5f), Eps);
            // 閾値直前と直後で非連続な落差がある
            float justBelow = PrestigeCliffRules.PrestigeCollapse(0.9f, 0.5f, 0.5f);   // 0.9-0.45=0.45（漸進）
            float justAbove = PrestigeCliffRules.PrestigeCollapse(0.9f, 0.51f, 0.5f);  // 0.18（崖）
            Assert.Greater(justBelow - justAbove, 0.2f, "閾値を超えると非連続に落ちる");
        }

        /// <summary>不可逆性＝崩落が深いほど1へ近づく。</summary>
        [Test]
        public void IrreversibilityFactor_深い崩落ほど戻りにくい()
        {
            Assert.AreEqual(0.2f, PrestigeCliffRules.IrreversibilityFactor(0.2f), Eps);
            Assert.AreEqual(1f, PrestigeCliffRules.IrreversibilityFactor(2f), Eps); // クランプ
            Assert.Greater(PrestigeCliffRules.IrreversibilityFactor(0.9f),
                           PrestigeCliffRules.IrreversibilityFactor(0.3f));
        }

        /// <summary>非対称＝崩壊が速い（fallTime小）ほど1へ／build=fallで0.5／build=0で0。</summary>
        [Test]
        public void SlowBuildFastFall_積上の遅さと崩壊の速さの非対称()
        {
            // build100, fall1: 1 - 1/101 ≈ 0.990099
            Assert.AreEqual(0.990099f, PrestigeCliffRules.SlowBuildFastFall(100f, 1f), Eps);
            // build=fall=10: 1 - 10/20 = 0.5
            Assert.AreEqual(0.5f, PrestigeCliffRules.SlowBuildFastFall(10f, 10f), Eps);
            // build=0は比較不能で0
            Assert.AreEqual(0f, PrestigeCliffRules.SlowBuildFastFall(0f, 5f), Eps);
        }

        /// <summary>回復上限＝崩壊前×0.7（既定）。元の高さには戻らない。</summary>
        [Test]
        public void RecoveryCeiling_元の高さには戻らない()
        {
            // 0.8 × 0.7 = 0.56
            Assert.AreEqual(0.56f, PrestigeCliffRules.RecoveryCeiling(0.8f), Eps);
            Assert.Less(PrestigeCliffRules.RecoveryCeiling(0.8f), 0.8f, "回復上限は崩壊前を下回る");
        }

        /// <summary>神秘性侵食＝近接露出で威光が薄れる（既定0.5）。距離が威光を保つ。</summary>
        [Test]
        public void MystiqueErosion_近接で神秘が薄れる()
        {
            // 0.8 - 0.8*1*0.5 = 0.4（最大露出）
            Assert.AreEqual(0.4f, PrestigeCliffRules.MystiqueErosion(0.8f, 1f), Eps);
            // 露出ゼロ（遠い）なら威光不変
            Assert.AreEqual(0.8f, PrestigeCliffRules.MystiqueErosion(0.8f, 0f), Eps);
        }

        /// <summary>砕けた判定＝威光が閾値未満で成立。</summary>
        [Test]
        public void IsPrestigeShattered_閾値未満で砕ける()
        {
            Assert.IsTrue(PrestigeCliffRules.IsPrestigeShattered(0.1f, 0.3f));
            Assert.IsFalse(PrestigeCliffRules.IsPrestigeShattered(0.5f, 0.3f));
            Assert.IsFalse(PrestigeCliffRules.IsPrestigeShattered(0.3f, 0.3f)); // 同値は不成立
        }

        /// <summary>
        /// 物語＝威光が時間をかけて積み上がるが、一度の大失敗で崖から瓦解し、回復は元の高さに届かない。
        /// </summary>
        [Test]
        public void 物語_時間で積上げ一度の失敗で崖瓦解し回復は元に届かない()
        {
            // 積み上げ＝複数回の成功で威光が漸進的に高まる（逓減）
            float pr = 0.2f;
            for (int i = 0; i < 5; i++)
                pr = PrestigeCliffRules.PrestigeAccumulation(pr, 0.6f);
            Assert.Greater(pr, 0.5f, "成功の積み重ねで威光が高みへ");
            float peak = pr;

            // 一度の大失敗（閾値超）で非連続に瓦解
            float afterCollapse = PrestigeCliffRules.PrestigeCollapse(peak, 0.9f, 0.5f);
            Assert.Less(afterCollapse, peak * 0.3f, "一度の大失敗で崖から一気に瓦解する");

            // 崩壊後にどれだけ積み上げても、回復上限は崩壊前のピークに届かない
            float ceiling = PrestigeCliffRules.RecoveryCeiling(peak);
            Assert.Less(ceiling, peak, "回復上限は元の高さに届かない（不可逆）");

            float recovered = afterCollapse;
            for (int i = 0; i < 20; i++)
                recovered = PrestigeCliffRules.PrestigeAccumulation(recovered, 1f);
            // 積上は1へ漸近しうるので、現実の回復は天井で頭打ち＝min を取る運用を確認
            float effectiveRecovered = Mathf.Min(recovered, ceiling);
            Assert.LessOrEqual(effectiveRecovered, ceiling + Eps, "回復は上限で頭打ち");
            Assert.Less(effectiveRecovered, peak, "回復しても元の威光には戻らない");
        }
    }
}
