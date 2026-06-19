using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>補給の優先配分（限られた補給を主攻/前線/危機にどう割り振るか）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class SupplyPriorityRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を経る箇所のみ緩める

        [Test]
        public void 補給不足は需要から供給を引いた非負量()
        {
            // 需要100・供給70 → 不足30。
            Assert.AreEqual(30f, SupplyPriorityRules.TotalShortfall(100f, 70f), Eps);
            // 供給が需要を満たせば不足ゼロ。
            Assert.AreEqual(0f, SupplyPriorityRules.TotalShortfall(70f, 100f), Eps);
        }

        [Test]
        public void 優先度は重要度と切迫度の重み付き和_主攻ほど高い()
        {
            // 重要度1.0×1 + 切迫度0.5×1 = 1.5（主攻かつやや切迫）。
            Assert.AreEqual(1.5f, SupplyPriorityRules.PriorityWeight(1.0f, 0.5f), Eps);
            // 重要でも切迫でもなければ優先度ゼロ。
            Assert.AreEqual(0f, SupplyPriorityRules.PriorityWeight(0.0f, 0.0f), Eps);
        }

        [Test]
        public void 配分は優先度に比例し主攻へ厚く回る_総量保存()
        {
            // 優先度[3,1]・利用可能100 → sum4 → [75,25]（主攻へ厚く）。
            float[] alloc = SupplyPriorityRules.Allocation(new float[] { 3f, 1f }, 100f);
            Assert.AreEqual(2, alloc.Length);
            Assert.AreEqual(75f, alloc[0], Eps);
            Assert.AreEqual(25f, alloc[1], Eps);
            // 総量保存。
            Assert.AreEqual(100f, alloc[0] + alloc[1], Eps);
        }

        [Test]
        public void 配分はnullと空配列に安全_全0優先は配らない()
        {
            Assert.AreEqual(0, SupplyPriorityRules.Allocation(null, 100f).Length);
            Assert.AreEqual(0, SupplyPriorityRules.Allocation(new float[0], 100f).Length);
            // どの戦線も優先度0なら配り先なし（全0）。
            float[] none = SupplyPriorityRules.Allocation(new float[] { 0f, 0f }, 100f);
            Assert.AreEqual(2, none.Length);
            Assert.AreEqual(0f, none[0], Eps);
            Assert.AreEqual(0f, none[1], Eps);
        }

        [Test]
        public void 戦線充足は配分割る需要_需要ゼロは充足()
        {
            // 配分50・需要100 → 0.5。
            Assert.AreEqual(0.5f, SupplyPriorityRules.SectorReadiness(50f, 100f), Eps);
            // 需要を満たせば1.0（過剰は1にクランプ）。
            Assert.AreEqual(1.0f, SupplyPriorityRules.SectorReadiness(120f, 100f), Eps);
            // 需要ゼロは満たすものなし＝充足。
            Assert.AreEqual(1.0f, SupplyPriorityRules.SectorReadiness(0f, 0f), Eps);
        }

        [Test]
        public void 主攻の充足は主攻配分が主攻需要をどれだけ満たすか()
        {
            // 主攻配分80・主攻需要100 → 0.8（厚く配分できたか）。
            Assert.AreEqual(0.8f, SupplyPriorityRules.MainEffortPriority(80f, 100f), Eps);
            // 主攻需要を満たせば主攻は止まらない（1.0）。
            Assert.AreEqual(1.0f, SupplyPriorityRules.MainEffortPriority(100f, 100f), Eps);
        }

        [Test]
        public void 最低線を割り込むほど崩壊リスクが急増_最低線充足はリスクゼロ()
        {
            // 既定指数2.0。最低線100に対し配分50 → 不足割合0.5 → 0.5^2=0.25。
            Assert.AreEqual(0.25f, SupplyPriorityRules.StarvedSectorRisk(50f, 100f), PowEps);
            // 配分ゼロ → 不足1.0 → 1.0^2=1.0（崩壊必至）。
            Assert.AreEqual(1.0f, SupplyPriorityRules.StarvedSectorRisk(0f, 100f), PowEps);
            // 最低線を満たせばリスクゼロ。
            Assert.AreEqual(0f, SupplyPriorityRules.StarvedSectorRisk(100f, 100f), PowEps);
        }

        [Test]
        public void トリアージは危機需要を総供給から最優先で満たす()
        {
            // 危機需要40・総供給100 → 危機を全救援（1.0）。
            Assert.AreEqual(1.0f, SupplyPriorityRules.LogisticsTriage(40f, 100f), Eps);
            // 危機需要100・総供給40 → 供給ぶんしか救えない（0.4）。
            Assert.AreEqual(0.4f, SupplyPriorityRules.LogisticsTriage(100f, 40f), Eps);
            // 救うべき危機が無ければ充足。
            Assert.AreEqual(1.0f, SupplyPriorityRules.LogisticsTriage(0f, 40f), Eps);
        }

        [Test]
        public void 充足度が既定しきい値以上なら補給は足りている()
        {
            // 既定しきい値0.8。充足0.8 → 足りている。
            Assert.IsTrue(SupplyPriorityRules.IsSupplyAdequate(0.8f));
            // 充足0.5 → 配分不足で飢える。
            Assert.IsFalse(SupplyPriorityRules.IsSupplyAdequate(0.5f));
        }

        [Test]
        public void 物語_限られた補給を主攻へ厚く配り二次正面を削ると削った戦線が崩れる()
        {
            // 補給能力(供給80)が総需要(100)を満たせない＝不足20。
            float shortfall = SupplyPriorityRules.TotalShortfall(100f, 80f);
            Assert.AreEqual(20f, shortfall, Eps);

            // 主攻（重要度1.0・切迫0.5＝優先1.5）と二次正面（重要度0.5・切迫0＝優先0.5）。
            float mainPri = SupplyPriorityRules.PriorityWeight(1.0f, 0.5f);
            float secondaryPri = SupplyPriorityRules.PriorityWeight(0.5f, 0.0f);
            Assert.AreEqual(1.5f, mainPri, Eps);
            Assert.AreEqual(0.5f, secondaryPri, Eps);

            // 利用可能80を優先度比[1.5,0.5]＝[3:1]で配分 → 主攻60・二次正面20。
            float[] alloc = SupplyPriorityRules.Allocation(new float[] { mainPri, secondaryPri }, 80f);
            Assert.AreEqual(60f, alloc[0], Eps);
            Assert.AreEqual(20f, alloc[1], Eps);

            // 主攻は需要60に対し60配分＝充足1.0で止まらない（厚く配れた）。
            float mainReady = SupplyPriorityRules.MainEffortPriority(alloc[0], 60f);
            Assert.AreEqual(1.0f, mainReady, Eps);
            Assert.IsTrue(SupplyPriorityRules.IsSupplyAdequate(mainReady));

            // だが二次正面は最低線40を割り込んで20しか来ず（不足割合0.5）→ 崩壊リスク0.25。
            float risk = SupplyPriorityRules.StarvedSectorRisk(alloc[1], 40f);
            Assert.AreEqual(0.25f, risk, PowEps);
            // 二次正面の充足は需要40に対し0.5＝既定しきい値0.8未満で飢える。
            float secondaryReady = SupplyPriorityRules.SectorReadiness(alloc[1], 40f);
            Assert.AreEqual(0.5f, secondaryReady, Eps);
            Assert.IsFalse(SupplyPriorityRules.IsSupplyAdequate(secondaryReady));
        }
    }
}
