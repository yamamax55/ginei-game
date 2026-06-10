using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>攻勢終末点・戦略的過伸張（#1129）の純ロジックを既定Paramsの具体値で固定する。</summary>
    public class CulminatingPointRulesTests
    {
        /// <summary>基地直上は補給効率満杯・遠いほど逓減（補給網広い方が遠くまで保つ）。</summary>
        [Test]
        public void SupplyEfficiency_FallsOffWithDistance()
        {
            // 距離0＝満杯
            Assert.AreEqual(1f, CulminatingPointRules.SupplyEfficiency(0f, 1f), 1e-4f);
            // range=1.0, d=10：effectiveReach=10, ratio=1, falloff=1, eff=0.5
            Assert.AreEqual(0.5f, CulminatingPointRules.SupplyEfficiency(10f, 1f), 1e-4f);
            // さらに遠いと効率は単調に下がる
            float far = CulminatingPointRules.SupplyEfficiency(20f, 1f);
            Assert.Less(far, 0.5f);
            // 補給網が広い方が同じ距離でも効率が高い
            Assert.Greater(CulminatingPointRules.SupplyEfficiency(10f, 1f),
                CulminatingPointRules.SupplyEfficiency(10f, 0.2f));
        }

        /// <summary>補給効率下限は割り込まない（現地調達の底）。</summary>
        [Test]
        public void SupplyEfficiency_RespectsFloor()
        {
            Assert.AreEqual(0.1f, CulminatingPointRules.SupplyEfficiency(10000f, 0f), 1e-3f);
        }

        /// <summary>実効戦力倍率＝補給細りと損耗で逓減（満杯・無損耗は1.0）。</summary>
        [Test]
        public void CombatPowerFactor_DegradesWithSupplyAndAttrition()
        {
            // eff=1, attr=0 → 1.0
            Assert.AreEqual(1f, CulminatingPointRules.CombatPowerFactor(1f, 0f), 1e-4f);
            // eff=0.5, attr=0 → (1-0.5*0.5)=0.75
            Assert.AreEqual(0.75f, CulminatingPointRules.CombatPowerFactor(0.5f, 0f), 1e-4f);
            // eff=0.5, attr=0.5 → 0.75*(1-0.6*0.5)=0.525
            Assert.AreEqual(0.525f, CulminatingPointRules.CombatPowerFactor(0.5f, 0.5f), 1e-4f);
        }

        /// <summary>攻勢終末点の距離＝戦力比が逆転する地点（初期優勢なら有限・劣勢なら0）。</summary>
        [Test]
        public void CulminatingDistance_WhereStrengthEqualizes()
        {
            // init=200, def=100, range=1.0：targetEff=0.5 → reach=10 の地点
            Assert.AreEqual(10f, CulminatingPointRules.CulminatingDistance(200f, 100f, 1f), 1e-3f);
            // 最初から劣勢なら攻勢が成立せず0
            Assert.AreEqual(0f, CulminatingPointRules.CulminatingDistance(80f, 100f, 1f), 1e-4f);
            // 補給網が広いほど終末点は遠い
            Assert.Greater(CulminatingPointRules.CulminatingDistance(200f, 100f, 1f),
                CulminatingPointRules.CulminatingDistance(200f, 100f, 0.2f));
        }

        /// <summary>終末点判定＝実効攻撃力が防御を割ったら越えている（反撃の危険）。</summary>
        [Test]
        public void IsPastCulmination_WhenEffectiveAttackBelowDefender()
        {
            // nominal=200, factor=0.4 → 80 < def=100 ＝越えた
            Assert.IsTrue(CulminatingPointRules.IsPastCulmination(0.4f, 100f, 200f));
            // factor=0.75 → 150 >= 100 ＝まだ手前
            Assert.IsFalse(CulminatingPointRules.IsPastCulmination(0.75f, 100f, 200f));
        }

        /// <summary>終末点超過ペナルティ＝越えた距離で非線形に増え上限で頭打ち（手前なら0）。</summary>
        [Test]
        public void OverreachPenalty_NonlinearAndCapped()
        {
            Assert.AreEqual(0f, CulminatingPointRules.OverreachPenalty(0f), 1e-4f);
            // beyond=5：norm=0.5, raw=0.25
            Assert.AreEqual(0.25f, CulminatingPointRules.OverreachPenalty(5f), 1e-4f);
            // beyond=10：norm=1, raw=1 → 上限0.9で頭打ち
            Assert.AreEqual(0.9f, CulminatingPointRules.OverreachPenalty(10f), 1e-4f);
            Assert.AreEqual(0.9f, CulminatingPointRules.OverreachPenalty(100f), 1e-4f);
        }

        /// <summary>最適停止点＝終末点の手前（安全マージン×）で止める＝深追いしない。</summary>
        [Test]
        public void OptimalHaltPoint_StopsBeforeCulmination()
        {
            // assumedInit=2*def=200, culm=10（range=1）→ ×0.8 = 8
            Assert.AreEqual(8f, CulminatingPointRules.OptimalHaltPoint(1f, 100f), 1e-3f);
            // 停止点は必ず終末点の手前
            float culm = CulminatingPointRules.CulminatingDistance(200f, 100f, 1f);
            Assert.Less(CulminatingPointRules.OptimalHaltPoint(1f, 100f), culm);
        }
    }
}
