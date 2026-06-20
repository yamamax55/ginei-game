using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 封臣の軍役（#封臣軍役）の純ロジック検証。既定 Params の期待値を手計算で固定。
    /// 動員義務→応召→私兵の質→私利→拒否→連合の結束→指揮の難しさ→封建軍の実効戦力。
    /// </summary>
    public class VassalLevyRulesTests
    {
        private const float Tol = 1e-4f;

        // ── 動員義務 ──
        [Test]
        public void LevyObligation_FiefTimesContract()
        {
            // 2 × 0.8 × 1000 = 1600
            Assert.AreEqual(1600f, VassalLevyRules.LevyObligation(2f, 0.8f), Tol);
        }

        [Test]
        public void LevyObligation_NegativeAndOverflowClamped()
        {
            // 負の封土は0扱い／契約>1 は1にクランプ
            Assert.AreEqual(0f, VassalLevyRules.LevyObligation(-5f, 1f), Tol);
            Assert.AreEqual(3000f, VassalLevyRules.LevyObligation(3f, 2f), Tol); // 3×1×1000
        }

        // ── 応召 ──
        [Test]
        public void MusterResponse_LoyaltyScalesAboveFloor()
        {
            // 忠誠0.5 → 応召率 Lerp(0.2,1,0.5)=0.6 → 1000×0.6 = 600
            Assert.AreEqual(600f, VassalLevyRules.MusterResponse(1000f, 0.5f), Tol);
            // 忠誠0でも最低 musterFloor=0.2 ぶんは参集
            Assert.AreEqual(200f, VassalLevyRules.MusterResponse(1000f, 0f), Tol);
        }

        // ── 私兵の質 ──
        [Test]
        public void RetinueQuality_WeightedAverageOfWealthAndTradition()
        {
            // 0.8×0.5 + 0.4×0.5 = 0.6
            Assert.AreEqual(0.6f, VassalLevyRules.RetinueQuality(0.8f, 0.4f), Tol);
        }

        // ── 私的利害 ──
        [Test]
        public void VassalSelfInterest_AmbitionTimesProfit()
        {
            // 0.8 × 0.5 = 0.4（両方揃って初めて利害が立つ）
            Assert.AreEqual(0.4f, VassalLevyRules.VassalSelfInterest(0.8f, 0.5f), Tol);
            // 旨味ゼロなら私利ゼロ
            Assert.AreEqual(0f, VassalLevyRules.VassalSelfInterest(1f, 0f), Tol);
        }

        // ── 動員拒否 ──
        [Test]
        public void DefiantRefusal_DisloyaltyTimesWeaknessTimesSelfInterest()
        {
            // (1-0.2)×0.8×0.5×1 = 0.32
            Assert.AreEqual(0.32f, VassalLevyRules.DefiantRefusal(0.2f, 0.8f, 0.5f), Tol);
            // 忠誠が満ちていれば拒否なし
            Assert.AreEqual(0f, VassalLevyRules.DefiantRefusal(1f, 1f, 1f), Tol);
        }

        // ── 連合の結束 ──
        [Test]
        public void CoalitionCohesion_CommonEnemyErodedByRivalry()
        {
            // 0.9 ×(1 - 0.6×0.5=0.3) = 0.9×0.7 = 0.63
            Assert.AreEqual(0.63f, VassalLevyRules.CoalitionCohesion(0.9f, 0.6f), Tol);
        }

        // ── 指揮の難しさ ──
        [Test]
        public void CommandDifficulty_CrowdingAmplifiedByAutonomy()
        {
            // 単独は難しさ0
            Assert.AreEqual(0f, VassalLevyRules.CommandDifficulty(1, 1f), Tol);
            // 5人・自治1.0 → crowding=(5-1)/8=0.5 → 0.5×(1+0.5×1)=0.75
            Assert.AreEqual(0.75f, VassalLevyRules.CommandDifficulty(5, 1f), Tol);
            // 大連合は1.0に飽和（烏合の衆）
            Assert.AreEqual(1f, VassalLevyRules.CommandDifficulty(20, 1f), Tol);
        }

        // ── 封建軍の実効戦力 ──
        [Test]
        public void FeudalArmyStrength_ProductOfMusterQualityCohesion()
        {
            // 0.6 × 0.6 × 0.63 = 0.2268
            Assert.AreEqual(0.2268f, VassalLevyRules.FeudalArmyStrength(0.6f, 0.6f, 0.63f), Tol);
            // どれか欠ければ崩れる（結束0で戦力0）
            Assert.AreEqual(0f, VassalLevyRules.FeudalArmyStrength(1f, 1f, 0f), Tol);
        }

        // ── 物語テスト：リップシュタット貴族連合 ──
        [Test]
        public void Narrative_LippstadtCoalition_DisunityBreedsWeakArmy()
        {
            // 多くの門閥貴族が私兵を率いて参集するが、互いに対立し各々が自治を主張する貴族連合。
            // 共通の敵（簒奪者）はいるが、封臣間の対立が深く結束が緩い。
            float cohesionDisunited = VassalLevyRules.CoalitionCohesion(0.9f, 0.8f); // 高い対立
            float cohesionUnited = VassalLevyRules.CoalitionCohesion(0.9f, 0.1f);    // 低い対立
            Assert.Less(cohesionDisunited, cohesionUnited, "対立が深いほど連合の結束は緩む");

            // 自治を主張する大勢の封臣＝烏合の衆で指揮が難しい
            float cmdMany = VassalLevyRules.CommandDifficulty(15, 0.9f);
            float cmdFew = VassalLevyRules.CommandDifficulty(3, 0.9f);
            Assert.Greater(cmdMany, cmdFew, "封臣が多いほど指揮は難しい");

            // 結束の緩い大連合の実効戦力は、結束した精鋭より弱い（数で勝っても烏合の衆）。
            float strengthDisunited = VassalLevyRules.FeudalArmyStrength(0.9f, 0.6f, cohesionDisunited);
            float strengthUnited = VassalLevyRules.FeudalArmyStrength(0.7f, 0.8f, cohesionUnited);
            Assert.Less(strengthDisunited, strengthUnited, "烏合の衆は結束した軍に劣る");

            // 忠誠薄く主君（首魁）が弱く戦に私利を見出す封臣は動員を拒否しうる。
            float selfInterest = VassalLevyRules.VassalSelfInterest(0.9f, 0.8f);
            float refusal = VassalLevyRules.DefiantRefusal(0.2f, 0.7f, selfInterest);
            Assert.Greater(refusal, 0.3f, "私利が勝てば離反の芽が立つ");
        }
    }
}
