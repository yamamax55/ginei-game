using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 前哨基地（辺境の小規模拠点・補給拠点）の運営の純ロジック検証。位置価値→駐留規模→維持コスト→
    /// 孤立脆弱→補給途絶→作戦支援→正味価値→放棄か増強か。既定 <see cref="OutpostParams.Default"/>
    /// （要衝0.5/資源0.3/辺境0.2・駐留0.8・維持基礎0.2・孤立0.6・脆弱0.9・途絶0.8・支援0.9・コスト罰0.6・
    /// 脆弱罰0.5・増強閾値0.2）で期待値を固定。
    /// </summary>
    public class OutpostRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>位置価値＝要衝×資源×辺境の加重和（重み正規化）。満点で1。</summary>
        [Test]
        public void StrategicPosition_要衝資源辺境の加重和()
        {
            // 全1 → wSum=1.0で raw=1.0 → 1.0
            Assert.AreEqual(1f, OutpostRules.StrategicPosition(1f, 1f, 1f), Eps);
            // 要衝0.6/資源0.4/辺境0.2 → 0.6×0.5+0.4×0.3+0.2×0.2=0.46、/1.0=0.46
            Assert.AreEqual(0.46f, OutpostRules.StrategicPosition(0.6f, 0.4f, 0.2f), Eps);
            // 要衝だけ → 0.5/1.0=0.5
            Assert.AreEqual(0.5f, OutpostRules.StrategicPosition(1f, 0f, 0f), Eps);
        }

        /// <summary>適正駐留＝位置価値×利用可能戦力×スケール。どちらか0なら駐留しない。</summary>
        [Test]
        public void GarrisonSize_位置価値と戦力の積()
        {
            // 0.5×0.5 → 0.8×0.25=0.2
            Assert.AreEqual(0.2f, OutpostRules.GarrisonSize(0.5f, 0.5f), Eps);
            // 満点 → 0.8（スケール上限）
            Assert.AreEqual(0.8f, OutpostRules.GarrisonSize(1f, 1f), Eps);
            // 戦力0なら駐留0
            Assert.AreEqual(0f, OutpostRules.GarrisonSize(1f, 0f), Eps);
        }

        /// <summary>維持コスト＝駐留に比例し孤立で割増。遠隔の大守備隊ほど高い。</summary>
        [Test]
        public void MaintenanceCost_駐留に比例し孤立で割増()
        {
            // 駐留0.5・孤立0 → base=0.2+0.8×0.5=0.6、×1=0.6
            Assert.AreEqual(0.6f, OutpostRules.MaintenanceCost(0.5f, 0f), Eps);
            // 駐留0.5・孤立1 → 0.6×(1+0.6)=0.96
            Assert.AreEqual(0.96f, OutpostRules.MaintenanceCost(0.5f, 1f), Eps);
            // 駐留1・孤立1 → base=1.0、×1.6=1.6 → clamp 1.0
            Assert.AreEqual(1f, OutpostRules.MaintenanceCost(1f, 1f), Eps);
        }

        /// <summary>孤立脆弱＝孤立×(1−駐留)×救援距離。守備満点なら脆弱性0。</summary>
        [Test]
        public void IsolationVulnerability_孤立かつ手薄かつ救援遠で脆い()
        {
            // 孤立1・駐留0・救援1 → 0.9×(1×1×1)=0.9
            Assert.AreEqual(0.9f, OutpostRules.IsolationVulnerability(1f, 0f, 1f), Eps);
            // 孤立0.5・駐留0.5・救援1 → 0.5×0.5×1=0.25、×0.9=0.225
            Assert.AreEqual(0.225f, OutpostRules.IsolationVulnerability(0.5f, 0.5f, 1f), Eps);
            // 駐留満点なら脆弱性0
            Assert.AreEqual(0f, OutpostRules.IsolationVulnerability(1f, 1f, 1f), Eps);
        }

        /// <summary>補給途絶リスク＝補給線長×敵活動。どちらか0ならリスク0。</summary>
        [Test]
        public void SupplyInterdictionRisk_補給線長と敵活動の積()
        {
            // 満点 → 0.8（スケール）
            Assert.AreEqual(0.8f, OutpostRules.SupplyInterdictionRisk(1f, 1f), Eps);
            // 0.5×0.5 → 0.8×0.25=0.2
            Assert.AreEqual(0.2f, OutpostRules.SupplyInterdictionRisk(0.5f, 0.5f), Eps);
            // 敵活動0ならリスク0
            Assert.AreEqual(0f, OutpostRules.SupplyInterdictionRisk(1f, 0f), Eps);
        }

        /// <summary>作戦支援＝位置価値×駐留。良い位置に十分な戦力ほど前方作戦の足場になる。</summary>
        [Test]
        public void OperationalSupport_位置価値と駐留の積()
        {
            // 0.5×0.5 → 0.9×0.25=0.225
            Assert.AreEqual(0.225f, OutpostRules.OperationalSupport(0.5f, 0.5f), Eps);
            // 満点 → 0.9（スケール）
            Assert.AreEqual(0.9f, OutpostRules.OperationalSupport(1f, 1f), Eps);
            // 駐留0なら足場にならない
            Assert.AreEqual(0f, OutpostRules.OperationalSupport(1f, 0f), Eps);
        }

        /// <summary>正味価値＝支援 − 維持×0.6 − 脆弱×0.5。支援が負担と危険を上回れば正。</summary>
        [Test]
        public void OutpostValue_支援から維持と脆弱を引く()
        {
            // 支援0.9・維持0.6・脆弱0.225 → 0.9 − 0.6×0.6 − 0.225×0.5 = 0.9−0.36−0.1125 = 0.4275
            Assert.AreEqual(0.4275f, OutpostRules.OutpostValue(0.9f, 0.6f, 0.225f), Eps);
            // 支援薄く負担重い拠点は負（放棄を促す）
            Assert.AreEqual(0.1f - 0.6f - 0.45f, OutpostRules.OutpostValue(0.1f, 1f, 0.9f), Eps);
        }

        /// <summary>増強判定＝(正味価値 + 位置の符号化)÷2 が閾値以上。位置が良ければ増強へ傾く。</summary>
        [Test]
        public void ShouldReinforce_価値と位置の合成が閾値以上()
        {
            // 正味0.4275・位置0.46 → posSigned=-0.08、score=(0.4275-0.08)/2=0.17375 < 0.2 → false
            Assert.IsFalse(OutpostRules.ShouldReinforce(0.4275f, 0.46f));
            // 正味0.4275・位置1.0 → posSigned=1、score=(0.4275+1)/2=0.71375 ≥ 0.2 → true
            Assert.IsTrue(OutpostRules.ShouldReinforce(0.4275f, 1f));
        }

        /// <summary>
        /// 物語：辺境の要衝に小拠点を築く。要衝近接が高く（0.9）資源そこそこ（0.4）辺境到達が深い（0.7）拠点に
        /// 戦力を割き（0.6）置く。本国から遠く（孤立0.8）救援も遠い（0.7）が、駐留があれば足場として機能し、
        /// 位置の良さが放棄ではなく増強の判断へ傾く——という一連の流れを通しで検算する。
        /// </summary>
        [Test]
        public void Narrative_辺境要衝の前哨を増強する流れ()
        {
            var p = OutpostParams.Default;

            // 位置価値＝0.9×0.5+0.4×0.3+0.7×0.2=0.45+0.12+0.14=0.71、/1.0=0.71
            float pos = OutpostRules.StrategicPosition(0.9f, 0.4f, 0.7f, p);
            Assert.AreEqual(0.71f, pos, Eps);

            // 駐留＝0.8×0.71×0.6=0.3408
            float gar = OutpostRules.GarrisonSize(pos, 0.6f, p);
            Assert.AreEqual(0.3408f, gar, Eps);

            // 維持＝base=0.2+0.8×0.3408=0.47264、孤立0.8で×(1+0.6×0.8)=×1.48 → 0.6995072
            float cost = OutpostRules.MaintenanceCost(gar, 0.8f, p);
            Assert.AreEqual(0.6995072f, cost, Eps);

            // 脆弱＝0.8×(1-0.3408)×0.7=0.8×0.6592×0.7=0.369152、×0.9=0.3322368
            float vuln = OutpostRules.IsolationVulnerability(0.8f, gar, 0.7f, p);
            Assert.AreEqual(0.3322368f, vuln, Eps);

            // 支援＝0.9×0.71×0.3408=0.2177712
            float support = OutpostRules.OperationalSupport(pos, gar, p);
            Assert.AreEqual(0.2177712f, support, Eps);

            // 正味＝0.2177712 − 0.6995072×0.6 − 0.3322368×0.5
            //      = 0.2177712 − 0.41970432 − 0.1661184 = -0.36805152
            float value = OutpostRules.OutpostValue(support, cost, vuln, p);
            Assert.AreEqual(-0.36805152f, value, Eps);

            // 正味は負だが位置が良い（0.71）。posSigned=0.42、score=(-0.36805152+0.42)/2=0.02597424
            // 既定閾値0.2には届かず、この条件では放棄寄り＝false
            Assert.IsFalse(OutpostRules.ShouldReinforce(value, pos, p.reinforceThreshold));

            // しかし孤立が浅く（後方支援が届き）脆弱が消えれば正味が好転し増強へ傾く
            float saferVuln = OutpostRules.IsolationVulnerability(0.2f, gar, 0.2f, p); // 0.2×0.6592×0.2×0.9=0.0237312
            float saferCost = OutpostRules.MaintenanceCost(gar, 0.2f, p);             // 0.47264×1.12=0.5293568
            float saferValue = OutpostRules.OutpostValue(support, saferCost, saferVuln, p);
            // 0.2177712 − 0.5293568×0.6 − 0.0237312×0.5 = 0.2177712 − 0.31761408 − 0.0118656 = -0.11170848
            Assert.AreEqual(-0.11170848f, saferValue, Eps);
            // posSigned=0.42、score=(-0.11170848+0.42)/2=0.15414576 < 0.2 → なお false（位置だけでは押し切れない）
            Assert.IsFalse(OutpostRules.ShouldReinforce(saferValue, pos, p.reinforceThreshold));
            // 位置が極上（1.0）なら posSigned=1、score=(-0.11170848+1)/2=0.44414576 ≥ 0.2 → 増強
            Assert.IsTrue(OutpostRules.ShouldReinforce(saferValue, 1f, p.reinforceThreshold));
        }
    }
}
