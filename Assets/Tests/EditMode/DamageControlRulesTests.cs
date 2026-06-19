using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// DamageControlRules（被弾後の応急処置と生存）の EditMode テスト。
    /// 既定 Params の期待値を手計算で固定。延焼/浸水拡大→鎮圧→局限→冗長→戦闘能力喪失→轟沈判定の連鎖。
    /// </summary>
    public class DamageControlRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void DamageSpread_拡大は初期被害と経過時間で増える()
        {
            // 100 × 1 × (1 + 0.5×2) = 200
            Assert.AreEqual(200f, DamageControlRules.DamageSpread(100f, 1f, 2f), Eps);
            // 鎮圧が早い（time=0）と拡大なし＝初期被害のまま
            Assert.AreEqual(100f, DamageControlRules.DamageSpread(100f, 1f, 0f), Eps);
        }

        [Test]
        public void DamageControlEffort_乗員技量と人手の積()
        {
            // 0.8 × 10 × 1 = 8
            Assert.AreEqual(8f, DamageControlRules.DamageControlEffort(0.8f, 10f), Eps);
            // 人手ゼロで効力ゼロ
            Assert.AreEqual(0f, DamageControlRules.DamageControlEffort(0.8f, 0f), Eps);
        }

        [Test]
        public void Containment_効力と拡大の綱引き()
        {
            // 8 / (8+200) = 0.0384615...
            Assert.AreEqual(8f / 208f, DamageControlRules.Containment(8f, 200f), Eps);
            // 60 / (60+40) = 0.6
            Assert.AreEqual(0.6f, DamageControlRules.Containment(60f, 40f), Eps);
            // 拡大なし＝完全鎮圧 / 効力なし＝鎮圧不能
            Assert.AreEqual(1f, DamageControlRules.Containment(10f, 0f), Eps);
            Assert.AreEqual(0f, DamageControlRules.Containment(0f, 50f), Eps);
        }

        [Test]
        public void CompartmentSealing_隔壁健全性で被害局限()
        {
            // 0.5 × 0.8 = 0.4
            Assert.AreEqual(0.4f, DamageControlRules.CompartmentSealing(0.5f), Eps);
            // 健全な隔壁で最大カット率（0.8）
            Assert.AreEqual(0.8f, DamageControlRules.CompartmentSealing(1f), Eps);
            // 隔壁崩壊で局限ゼロ
            Assert.AreEqual(0f, DamageControlRules.CompartmentSealing(0f), Eps);
        }

        [Test]
        public void SystemRedundancy_予備系統で重要区画被弾を補う()
        {
            // 4被弾を予備1基（1区画ぶん）でしか補えない＝0.25
            Assert.AreEqual(0.25f, DamageControlRules.SystemRedundancy(4f, 1f), Eps);
            // 2被弾を予備3基で過剰に補える＝1へ飽和
            Assert.AreEqual(1f, DamageControlRules.SystemRedundancy(2f, 3f), Eps);
            // 重要区画無傷＝完全冗長
            Assert.AreEqual(1f, DamageControlRules.SystemRedundancy(0f, 0f), Eps);
        }

        [Test]
        public void CombatCapabilityLoss_鎮圧と冗長で被害を差し引く()
        {
            // normalized = 200/201, ×(1-0.6) = 0.398009..., criticalLoss=(1-1)×0.5=0
            float expected = (200f / 201f) * (1f - 0.6f);
            Assert.AreEqual(expected, DamageControlRules.CombatCapabilityLoss(200f, 0.6f, 1f), Eps);
            // 完全鎮圧かつ完全冗長なら戦闘能力喪失ゼロ
            Assert.AreEqual(0f, DamageControlRules.CombatCapabilityLoss(200f, 1f, 1f), Eps);
        }

        [Test]
        public void FloodingProgression_排水が破孔に追いつかぬほど沈む()
        {
            // net = 5×(1-0.2)=4, 4/5 = 0.8
            Assert.AreEqual(0.8f, DamageControlRules.FloodingProgression(5f, 0.2f), Eps);
            // 完全排水（pump=1）で浸水ゼロ / 破孔なしで浸水ゼロ
            Assert.AreEqual(0f, DamageControlRules.FloodingProgression(5f, 1f), Eps);
            Assert.AreEqual(0f, DamageControlRules.FloodingProgression(0f, 0f), Eps);
        }

        [Test]
        public void IsShipLost_戦闘不能か沈没で喪失()
        {
            // 既定閾値 0.85：軽損傷は生存
            Assert.IsFalse(DamageControlRules.IsShipLost(0.4f, 0.8f));
            // 戦闘能力喪失が閾値超え＝喪失（航行不能/轟沈）
            Assert.IsTrue(DamageControlRules.IsShipLost(0.9f, 0.0f));
            // 浸水が閾値超え＝沈没
            Assert.IsTrue(DamageControlRules.IsShipLost(0.0f, 0.9f));
        }

        [Test]
        public void 入力クランプ_負値や範囲外でも破綻しない()
        {
            Assert.AreEqual(0f, DamageControlRules.DamageSpread(-100f, -1f, -5f), Eps);
            Assert.AreEqual(0f, DamageControlRules.DamageControlEffort(-1f, -10f), Eps);
            // crewSkill>1 はクランプされ 1×5×1=5
            Assert.AreEqual(5f, DamageControlRules.DamageControlEffort(2f, 5f), Eps);
            // sealing/redundancy は 0..1 へ収まる
            Assert.AreEqual(0.8f, DamageControlRules.CompartmentSealing(99f), Eps);
            Assert.AreEqual(1f, DamageControlRules.SystemRedundancy(1f, 99f), Eps);
        }

        /// <summary>
        /// 物語テスト：軽傷で熟練ダメコン班が早期鎮火した艦は生き残り、
        /// 同じ被害でも人手薄く隔壁崩壊・浸水放置の艦は轟沈に至る。
        /// </summary>
        [Test]
        public void 物語_練度の高い艦は鎮火し生き残り放置艦は轟沈する()
        {
            // --- 善戦艦：軽い火災を熟練班が即鎮火、重要区画は予備で冗長、浸水は排水が追いつく ---
            float goodSpread = DamageControlRules.DamageSpread(40f, 1f, 0.5f);   // 40×1×1.25=50
            float goodEffort = DamageControlRules.DamageControlEffort(0.9f, 200f); // 0.9×200=180
            float goodContain = DamageControlRules.Containment(goodEffort, goodSpread); // 180/230≈0.7826
            float goodRedund = DamageControlRules.SystemRedundancy(1f, 2f);      // min(2/1,1)=1
            float goodLoss = DamageControlRules.CombatCapabilityLoss(goodSpread, goodContain, goodRedund);
            float goodFlood = DamageControlRules.FloodingProgression(1f, 0.9f);  // net=0.1→0.1/1.1≈0.0909

            Assert.Less(goodContain, 1f);
            Assert.Greater(goodContain, 0.7f);                    // よく鎮火している
            Assert.IsFalse(DamageControlRules.IsShipLost(goodLoss, goodFlood)); // 生存

            // --- 放置艦：大火災を人手薄く鎮火できず、重要区画予備なし、破孔多く排水弱い ---
            float badSpread = DamageControlRules.DamageSpread(300f, 1.5f, 4f);   // 300×1.5×3=1350
            float badEffort = DamageControlRules.DamageControlEffort(0.2f, 20f); // 0.2×20=4
            float badContain = DamageControlRules.Containment(badEffort, badSpread); // 4/1354≈0.003
            float badRedund = DamageControlRules.SystemRedundancy(5f, 0f);       // 0/5=0
            float badLoss = DamageControlRules.CombatCapabilityLoss(badSpread, badContain, badRedund);
            float badFlood = DamageControlRules.FloodingProgression(8f, 0.1f);   // net=7.2→7.2/8.2≈0.878

            Assert.Less(badContain, 0.1f);                        // ほぼ鎮火できていない
            Assert.IsTrue(DamageControlRules.IsShipLost(badLoss, badFlood));    // 轟沈/航行不能

            // 善戦艦の戦闘能力喪失は放置艦より明確に小さい
            Assert.Less(goodLoss, badLoss);
        }
    }
}
