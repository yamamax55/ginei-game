using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>橋頭堡（敵地に確保した拠点）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class BridgeheadRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 揚陸戦力比で確保確率が決まり守備不在で確実()
        {
            // 300:100 → 300/400 = 0.75
            Assert.AreEqual(0.75f, BridgeheadRules.EstablishmentChance(300f, 100f), Eps);
            // 拮抗 → 0.5
            Assert.AreEqual(0.5f, BridgeheadRules.EstablishmentChance(100f, 100f), Eps);
            // 守備不在 → 確実
            Assert.AreEqual(1f, BridgeheadRules.EstablishmentChance(100f, 0f), Eps);
            // 揚陸戦力ゼロ → 足場を築けない
            Assert.AreEqual(0f, BridgeheadRules.EstablishmentChance(0f, 100f), Eps);
        }

        [Test]
        public void 規模が小さく後続が遅いほど脆い()
        {
            // 規模0・後続0 → 最も脆い1
            Assert.AreEqual(1f, BridgeheadRules.InitialFragility(0f, 0f), Eps);
            // 規模100・後続0（基準100）→ robustness1 → 0.5
            Assert.AreEqual(0.5f, BridgeheadRules.InitialFragility(100f, 0f), Eps);
            // 規模100・後続100 → robustness2 → 1/3
            Assert.AreEqual(1f / 3f, BridgeheadRules.InitialFragility(100f, 100f), Eps);
        }

        [Test]
        public void 戦力流入は回廊容量で律速される()
        {
            // 後続50 vs 容量30 → 容量で頭打ち
            Assert.AreEqual(30f, BridgeheadRules.BuildupRate(50f, 30f), Eps);
            // 後続50 vs 容量80 → 後続が律速
            Assert.AreEqual(50f, BridgeheadRules.BuildupRate(50f, 80f), Eps);
        }

        [Test]
        public void 反撃抵抗は戦力と防御地形で上がる()
        {
            // 戦力100・地形0（基準100）→ 100/200 = 0.5
            Assert.AreEqual(0.5f, BridgeheadRules.CounterattackResistance(100f, 0f), Eps);
            // 戦力100・地形1 → 実効200 → 200/300 = 2/3
            Assert.AreEqual(2f / 3f, BridgeheadRules.CounterattackResistance(100f, 1f), Eps);
        }

        [Test]
        public void 拡張は敵の封じ込めに抗う()
        {
            // 戦力200 vs 封じ込め200 → 0.5
            Assert.AreEqual(0.5f, BridgeheadRules.Expansion(200f, 200f), Eps);
            // 戦力300 vs 封じ込め100 → 0.75
            Assert.AreEqual(0.75f, BridgeheadRules.Expansion(300f, 100f), Eps);
        }

        [Test]
        public void 崩壊リスクは脆さと敵反撃の積()
        {
            // 脆さ0.5×反撃0.5 → 0.25
            Assert.AreEqual(0.25f, BridgeheadRules.CollapseRisk(0.5f, 0.5f), Eps);
            // 脆さゼロ or 反撃ゼロ → 崩壊リスクなし
            Assert.AreEqual(0f, BridgeheadRules.CollapseRisk(0f, 1f), Eps);
            Assert.AreEqual(0f, BridgeheadRules.CollapseRisk(1f, 0f), Eps);
        }

        [Test]
        public void 侵攻起点の価値は規模と戦略的位置で決まる()
        {
            // 規模100（基準100）→ 規模係数0.5 ×位置1.0 → 0.5
            Assert.AreEqual(0.5f, BridgeheadRules.BeachheadValue(100f, 1f), Eps);
            // 規模300 → 0.75 ×位置1.0 → 0.75
            Assert.AreEqual(0.75f, BridgeheadRules.BeachheadValue(300f, 1f), Eps);
            // 戦略的位置ゼロ → 価値ゼロ
            Assert.AreEqual(0f, BridgeheadRules.BeachheadValue(100f, 0f), Eps);
        }

        [Test]
        public void 閾値で橋頭堡の安定を判定する()
        {
            // 戦力300 vs 反撃100 → 安全度0.75 ≥ 0.5 → 安定
            Assert.IsTrue(BridgeheadRules.IsBridgeheadSecure(300f, 100f, 0.5f));
            // 戦力100 vs 反撃300 → 安全度0.25 < 0.5 → 不安定
            Assert.IsFalse(BridgeheadRules.IsBridgeheadSecure(100f, 300f, 0.5f));
        }

        [Test]
        public void 物語_橋頭堡を確保し後続を流し込み拡張するが確保直後は脆く反撃で押し戻されうる()
        {
            // 優勢な揚陸戦力（300:100）で敵地に橋頭堡を確保する。
            float establish = BridgeheadRules.EstablishmentChance(300f, 100f);
            Assert.AreEqual(0.75f, establish, Eps);

            // 確保直後は規模が小さく後続も細い＝脆い。
            float fragility = BridgeheadRules.InitialFragility(50f, 20f);
            Assert.Greater(fragility, 0.5f); // robustness=(50+20)/100=0.7 → 1/1.7≈0.588

            // この脆さに強い敵反撃（0.8）が来ると、押し戻される崩壊リスクが無視できない。
            float risk = BridgeheadRules.CollapseRisk(fragility, 0.8f);
            Assert.Greater(risk, 0.4f);
            // まだ足場は固まっていない（戦力50 vs 反撃120 → 安全度0.29 < 閾値0.5）。
            Assert.IsFalse(BridgeheadRules.IsBridgeheadSecure(50f, 120f, 0.5f));

            // だが回廊容量の許す範囲で後続を流し込めば（容量60が律速）、戦力が積み上がる。
            float buildup = BridgeheadRules.BuildupRate(100f, 60f);
            Assert.AreEqual(60f, buildup, Eps);
            // 戦力が積み上がった橋頭堡（300）は反撃を凌ぎ、足場が固まる。
            Assert.IsTrue(BridgeheadRules.IsBridgeheadSecure(300f, 100f, 0.5f));
            // 固まった橋頭堡は封じ込め（100）を押し広げ、侵攻の起点になる。
            Assert.Greater(BridgeheadRules.Expansion(300f, 100f), 0.5f);
            Assert.Greater(BridgeheadRules.BeachheadValue(300f, 0.8f), 0f);
        }
    }
}
