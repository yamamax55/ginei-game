using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦略予備（戦域全体のための予備兵力）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class StrategicReserveRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を経る箇所のみ緩める

        [Test]
        public void 動員済みは兵力規模そのまま_未動員は割引く即応度()
        {
            // mobFactor=0.5+0.5*mob。mob=1→1.0倍、mob=0→0.5倍。
            Assert.AreEqual(0.8f, StrategicReserveRules.ReserveReadiness(0.8f, 1.0f), Eps);
            Assert.AreEqual(0.4f, StrategicReserveRules.ReserveReadiness(0.8f, 0.0f), Eps);
        }

        [Test]
        public void 中央配置は内線で応答が速く_目標が遠いほど遅い()
        {
            // 中央1.0：実効距離 0.8*(1-0.5)=0.4（速い）。中央0.0：0.8*(1)=0.8（遅い）。
            Assert.AreEqual(0.4f, StrategicReserveRules.ResponseTime(1.0f, 0.8f), Eps);
            Assert.AreEqual(0.8f, StrategicReserveRules.ResponseTime(0.0f, 0.8f), Eps);
        }

        [Test]
        public void 危機戦線は即応度と速さと切迫度の積で救う()
        {
            // 0.8*(1-0.4)*1.0 = 0.48。
            Assert.AreEqual(0.48f, StrategicReserveRules.CrisisResponse(0.8f, 0.4f, 1.0f), Eps);
            // 応答が遅すぎる(1.0)なら間に合わず救援価値ゼロ。
            Assert.AreEqual(0f, StrategicReserveRules.CrisisResponse(0.8f, 1.0f, 1.0f), Eps);
        }

        [Test]
        public void 突破口へ予備を注ぐと拡張効果_突破口なしは効果ゼロ()
        {
            Assert.AreEqual(0.3f, StrategicReserveRules.OpportunityExploitation(0.6f, 0.5f), Eps);
            Assert.AreEqual(0f, StrategicReserveRules.OpportunityExploitation(0.6f, 0.0f), Eps);
        }

        [Test]
        public void 配分は切迫度に比例し最切迫戦線へ多く回る_総量保存()
        {
            // 切迫度[1,3,0]・総予備100 → sum4 → [25,75,0]。
            float[] alloc = StrategicReserveRules.ReserveAllocation(new float[] { 1f, 3f, 0f }, 100f);
            Assert.AreEqual(3, alloc.Length);
            Assert.AreEqual(25f, alloc[0], Eps);
            Assert.AreEqual(75f, alloc[1], Eps);
            Assert.AreEqual(0f, alloc[2], Eps);
            // 総量保存。
            Assert.AreEqual(100f, alloc[0] + alloc[1] + alloc[2], Eps);
        }

        [Test]
        public void 配分はnullと空配列に安全_全0切迫は温存()
        {
            Assert.AreEqual(0, StrategicReserveRules.ReserveAllocation(null, 100f).Length);
            Assert.AreEqual(0, StrategicReserveRules.ReserveAllocation(new float[0], 100f).Length);
            // 全戦線が切迫していなければ予備は温存（全0・行き先なし）。
            float[] calm = StrategicReserveRules.ReserveAllocation(new float[] { 0f, 0f }, 100f);
            Assert.AreEqual(2, calm.Length);
            Assert.AreEqual(0f, calm[0], Eps);
            Assert.AreEqual(0f, calm[1], Eps);
        }

        [Test]
        public void 予備を使い切るほど次の危機への過伸展リスクが急増()
        {
            // 既定指数2.0：0.5^2=0.25、全投入1.0^2=1.0（次の危機に裸）。
            Assert.AreEqual(0.25f, StrategicReserveRules.OverstretchFromNoReserve(0.5f), PowEps);
            Assert.AreEqual(1.0f, StrategicReserveRules.OverstretchFromNoReserve(1.0f), PowEps);
            // 未投入はリスクゼロ。
            Assert.AreEqual(0f, StrategicReserveRules.OverstretchFromNoReserve(0.0f), PowEps);
        }

        [Test]
        public void 中央配置の価値は多戦線で活き_単一戦線では薄い()
        {
            // 中央1.0・4戦線：1*(3/4)*1.0=0.75。
            Assert.AreEqual(0.75f, StrategicReserveRules.CentralPositionValue(1.0f, 4), Eps);
            // 戦線1つなら内線の旨味なし＝0。
            Assert.AreEqual(0f, StrategicReserveRules.CentralPositionValue(1.0f, 1), Eps);
        }

        [Test]
        public void 切迫かつ即応なら投入_どちらか欠ければ温存()
        {
            // 切迫1.0×即応0.5=0.5 >= 既定しきい値0.5 → 投入。
            Assert.IsTrue(StrategicReserveRules.ShouldDeployReserve(1.0f, 0.5f));
            // 切迫していても即応が低い（0.4×0.5=0.2<0.5）→ 間に合わず温存。
            Assert.IsFalse(StrategicReserveRules.ShouldDeployReserve(0.4f, 0.5f));
            // 即応できても切迫していなければ温存（0.2×1.0=0.2<0.5）。
            Assert.IsFalse(StrategicReserveRules.ShouldDeployReserve(0.2f, 1.0f));
        }

        [Test]
        public void 物語_中央の戦略予備が危機を救うが使い切ると次の危機に対応できない()
        {
            // 中央に温存された強力な戦略予備（兵力0.9・動員済み）。
            float readiness = StrategicReserveRules.ReserveReadiness(0.9f, 1.0f);
            Assert.AreEqual(0.9f, readiness, Eps);

            // 中央配置ゆえ遠い戦線(距離0.8)でも内線で速く駆けつける。
            float response = StrategicReserveRules.ResponseTime(1.0f, 0.8f);
            Assert.AreEqual(0.4f, response, Eps);

            // 切迫した第一の危機(0.9)を救援すべきと判断し、実際に救う。
            Assert.IsTrue(StrategicReserveRules.ShouldDeployReserve(0.9f, readiness));
            float rescue = StrategicReserveRules.CrisisResponse(readiness, response, 0.9f);
            // 0.9*(1-0.4)*0.9 = 0.486。
            Assert.AreEqual(0.486f, rescue, Eps);

            // だが予備を全投入(committed=1.0)すると、次の危機には裸＝過伸展リスク最大。
            float overstretch = StrategicReserveRules.OverstretchFromNoReserve(1.0f);
            Assert.AreEqual(1.0f, overstretch, PowEps);

            // 予備を使い切った後（即応度0）は、次の切迫した戦線が来ても投入できない。
            Assert.IsFalse(StrategicReserveRules.ShouldDeployReserve(0.9f, 0f));
        }
    }
}
