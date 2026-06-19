using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>惑星侵攻（惑星規模の戦略的制圧作戦）の純ロジック EditMode テスト。既定 Params で期待値を固定。</summary>
    public class PlanetaryInvasionRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 軌道制圧_艦隊と惑星防衛の比で決まる()
        {
            // 60 / (60 + 40) = 0.6
            Assert.AreEqual(0.6f, PlanetaryInvasionRules.OrbitalSupremacy(60f, 40f), Eps);
            // 双方ゼロは制圧なし、負入力はクランプ
            Assert.AreEqual(0f, PlanetaryInvasionRules.OrbitalSupremacy(0f, 0f), Eps);
            Assert.AreEqual(1f, PlanetaryInvasionRules.OrbitalSupremacy(100f, 0f), Eps);
        }

        [Test]
        public void 上陸成立_軌道制圧と上陸戦力の積()
        {
            // 0.6 × 0.5 = 0.3
            Assert.AreEqual(0.3f, PlanetaryInvasionRules.LandingViability(0.6f, 0.5f), Eps);
            // 軌道を握れば上陸戦力ぶんがそのまま乗る
            Assert.AreEqual(0.5f, PlanetaryInvasionRules.LandingViability(1f, 0.5f), Eps);
        }

        [Test]
        public void 地上戦力比_押し合いの比で決まる()
        {
            // 700 / (700 + 300) = 0.7
            Assert.AreEqual(0.7f, PlanetaryInvasionRules.ForceRatio(700f, 300f), Eps);
            // 双方ゼロは0
            Assert.AreEqual(0f, PlanetaryInvasionRules.ForceRatio(0f, 0f), Eps);
        }

        [Test]
        public void 占領地拡大_戦力比と上陸成立の積()
        {
            // 0.7 × 0.3 = 0.21
            Assert.AreEqual(0.21f, PlanetaryInvasionRules.TerritoryExpansion(0.7f, 0.3f), Eps);
        }

        [Test]
        public void 住民抵抗_敵意と士気の積()
        {
            // 0.8 × 0.5 × 1.0 = 0.4
            Assert.AreEqual(0.4f, PlanetaryInvasionRules.PopularResistance(0.8f, 0.5f), Eps);
            // どちらかゼロなら抵抗なし
            Assert.AreEqual(0f, PlanetaryInvasionRules.PopularResistance(0f, 0.5f), Eps);
        }

        [Test]
        public void 軌道補給_制圧と兵站の積()
        {
            // 0.6 × 0.5 = 0.3
            Assert.AreEqual(0.3f, PlanetaryInvasionRules.SupplyFromOrbit(0.6f, 0.5f), Eps);
        }

        [Test]
        public void 侵攻テンポ_拡大かける補給ひく抵抗()
        {
            // supplyFactor = Lerp(0.5, 1, 0.6) = 0.8、drag = 1.0 × 0.2 = 0.2
            // 0.5 × 0.8 - 0.2 = 0.2
            Assert.AreEqual(0.2f, PlanetaryInvasionRules.InvasionTempo(0.5f, 0.6f, 0.2f), Eps);
            // 補給満額・抵抗ゼロ: 0.8 × Lerp(0.5,1,1)=0.8 × 1 - 0 = 0.8
            Assert.AreEqual(0.8f, PlanetaryInvasionRules.InvasionTempo(0.8f, 1f, 0f), Eps);
            // 補給ゼロ・抵抗大: 0.1 × 0.5 - 0.5 = -0.45（押し戻される）
            Assert.AreEqual(-0.45f, PlanetaryInvasionRules.InvasionTempo(0.1f, 0f, 0.5f), Eps);
        }

        [Test]
        public void 制圧判定_拡大ひく抵抗が閾値以上()
        {
            // 0.7 - 0.2 = 0.5 ≥ 0.4 → 制圧
            Assert.IsTrue(PlanetaryInvasionRules.IsPlanetSubjugated(0.7f, 0.2f));
            // 0.5 - 0.3 = 0.2 < 0.4 → 未制圧
            Assert.IsFalse(PlanetaryInvasionRules.IsPlanetSubjugated(0.5f, 0.3f));
            // 0.6 - 0.2 = 0.4 ちょうど閾値は制圧
            Assert.IsTrue(PlanetaryInvasionRules.IsPlanetSubjugated(0.6f, 0.2f, 0.4f));
        }

        [Test]
        public void 物語_軌道を制した侵攻が惑星を制圧する()
        {
            var p = PlanetaryInvasionParams.Default;

            // 圧倒的な艦隊で軌道を制圧する。
            float supremacy = PlanetaryInvasionRules.OrbitalSupremacy(80f, 20f, p); // 0.8
            Assert.Greater(supremacy, 0.7f, "艦隊優位なら軌道を握る");

            // 軌道の傘の下で上陸が成立する。
            float viability = PlanetaryInvasionRules.LandingViability(supremacy, 0.9f, p); // 0.72
            Assert.Greater(viability, 0.6f, "軌道を制してこそ上陸が成り立つ");

            // 地上でも数で勝り、占領地を広げる。
            float ratio = PlanetaryInvasionRules.ForceRatio(1200f, 400f, p); // 0.75
            float expansion = PlanetaryInvasionRules.TerritoryExpansion(ratio, viability, p); // 0.54
            Assert.Greater(expansion, 0.5f, "戦力比と上陸成立がそろえば占領地が広がる");

            // 住民の抵抗は軽く、軌道からの補給も効いてテンポは正。
            float resistance = PlanetaryInvasionRules.PopularResistance(0.2f, 0.5f, p); // 0.1
            float supply = PlanetaryInvasionRules.SupplyFromOrbit(supremacy, 0.7f, p);   // 0.56
            float tempo = PlanetaryInvasionRules.InvasionTempo(expansion, supply, resistance, p);
            Assert.Greater(tempo, 0f, "抵抗が軽く補給が効けば侵攻は前進する");

            // 拡大が抵抗を十分上回り、惑星を制圧する。
            Assert.IsTrue(PlanetaryInvasionRules.IsPlanetSubjugated(expansion, resistance),
                "占領地拡大が抵抗を上回れば制圧完了");

            // 逆に軌道を失えば上陸が成立せず、占領地は広がらない。
            float weakSupremacy = PlanetaryInvasionRules.OrbitalSupremacy(20f, 80f, p); // 0.2
            float weakViability = PlanetaryInvasionRules.LandingViability(weakSupremacy, 0.9f, p);
            float weakExpansion = PlanetaryInvasionRules.TerritoryExpansion(ratio, weakViability, p);
            float heavyResistance = PlanetaryInvasionRules.PopularResistance(0.9f, 0.9f, p);
            Assert.IsFalse(PlanetaryInvasionRules.IsPlanetSubjugated(weakExpansion, heavyResistance),
                "軌道を失えば惑星は制圧できない");
        }
    }
}
