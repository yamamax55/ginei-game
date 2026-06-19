using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 支援艦（工作艦・補給艦・病院船）の純ロジック検証。前線での野戦修理・洋上補給・医療で艦隊の継戦力を
    /// 延伸するが脆く、喪失すると打撃。既定 <see cref="FleetTenderParams.Default"/>（修理単位0.25/補給単位0.25/
    /// 継戦延伸0.6/脆弱0.8/喪失打撃0.7/支援閾値0.4）で期待値を固定。
    /// </summary>
    public class FleetTenderRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>野戦修理能力＝工作艦数(0.25/隻で正規化)×修理設備。どちらかが0なら修理できない。</summary>
        [Test]
        public void FieldRepairCapacity_工作艦数と設備の積で決まる()
        {
            // 4隻×0.25=1.0、設備1 → 1.0
            Assert.AreEqual(1f, FleetTenderRules.FieldRepairCapacity(4f, 1f), Eps);
            // 2隻×0.25=0.5、設備0.5 → 0.25
            Assert.AreEqual(0.25f, FleetTenderRules.FieldRepairCapacity(2f, 0.5f), Eps);
            // 工作艦なし・設備なしでは修理0
            Assert.AreEqual(0f, FleetTenderRules.FieldRepairCapacity(0f, 1f), Eps);
            Assert.AreEqual(0f, FleetTenderRules.FieldRepairCapacity(4f, 0f), Eps);
        }

        /// <summary>修理処理率＝能力/(能力+損傷量)。損傷が小さいほど高速、膨大なら捌ききれず低下。</summary>
        [Test]
        public void RepairThroughput_損傷量に対して飽和する()
        {
            // 能力0.6・損傷0.6 → 0.6/1.2=0.5
            Assert.AreEqual(0.5f, FleetTenderRules.RepairThroughput(0.6f, 0.6f), Eps);
            // 損傷0なら満点
            Assert.AreEqual(1f, FleetTenderRules.RepairThroughput(0.8f, 0f), Eps);
            // 能力0なら0
            Assert.AreEqual(0f, FleetTenderRules.RepairThroughput(0f, 0.5f), Eps);
            // 損傷が増えると処理率は下がる（単調減少）
            Assert.Less(FleetTenderRules.RepairThroughput(0.5f, 2f), FleetTenderRules.RepairThroughput(0.5f, 0.5f));
        }

        /// <summary>洋上補給速度＝補給艦数(0.25/隻)×移送設備。どちらかが0なら補給できない。</summary>
        [Test]
        public void ReplenishmentRate_補給艦数と設備の積で決まる()
        {
            // 4隻×0.25=1.0、設備1 → 1.0
            Assert.AreEqual(1f, FleetTenderRules.ReplenishmentRate(4f, 1f), Eps);
            // 2隻×0.25=0.5、設備0.8 → 0.4
            Assert.AreEqual(0.4f, FleetTenderRules.ReplenishmentRate(2f, 0.8f), Eps);
            // 設備なしでは補給0
            Assert.AreEqual(0f, FleetTenderRules.ReplenishmentRate(4f, 0f), Eps);
        }

        /// <summary>継戦力延伸＝補給×修理。両輪が揃ってはじめて延びる。どちらかが0なら延伸0。</summary>
        [Test]
        public void SustainmentExtension_補給と修理の両輪で延伸する()
        {
            // 補給1・修理1 → 0.6×1×1=0.6（上限）
            Assert.AreEqual(0.6f, FleetTenderRules.SustainmentExtension(1f, 1f), Eps);
            // 補給0.5・修理0.5 → 0.6×0.5×0.5=0.15
            Assert.AreEqual(0.15f, FleetTenderRules.SustainmentExtension(0.5f, 0.5f), Eps);
            // 補給だけ・修理だけでは続かない
            Assert.AreEqual(0f, FleetTenderRules.SustainmentExtension(1f, 0f), Eps);
            Assert.AreEqual(0f, FleetTenderRules.SustainmentExtension(0f, 1f), Eps);
        }

        /// <summary>支援艦の脆弱性＝(1-装甲)×(1-護衛)×0.8。装甲か護衛で守るほど安全。</summary>
        [Test]
        public void TenderVulnerability_装甲と護衛で下がる()
        {
            // 装甲0・護衛0 → 0.8×1×1=0.8（最脆弱）
            Assert.AreEqual(0.8f, FleetTenderRules.TenderVulnerability(0f, 0f), Eps);
            // 装甲0.5・護衛0.5 → 0.8×0.5×0.5=0.2
            Assert.AreEqual(0.2f, FleetTenderRules.TenderVulnerability(0.5f, 0.5f), Eps);
            // 護衛満点なら脆弱性0
            Assert.AreEqual(0f, FleetTenderRules.TenderVulnerability(0f, 1f), Eps);
        }

        /// <summary>医療支援の充足＝病院船容量/(容量+死傷者)。死傷者が膨大なら収容しきれず低下。</summary>
        [Test]
        public void MedicalSupport_死傷者に対して飽和する()
        {
            // 容量0.6・死傷0.6 → 0.6/1.2=0.5
            Assert.AreEqual(0.5f, FleetTenderRules.MedicalSupport(0.6f, 0.6f), Eps);
            // 死傷者0なら満点
            Assert.AreEqual(1f, FleetTenderRules.MedicalSupport(0.8f, 0f), Eps);
            // 容量0なら0
            Assert.AreEqual(0f, FleetTenderRules.MedicalSupport(0f, 0.5f), Eps);
        }

        /// <summary>支援艦喪失の打撃＝喪失割合×継戦依存×0.7。支援に頼るほど喪失が痛い。</summary>
        [Test]
        public void SupportLossImpact_喪失と継戦依存で痛む()
        {
            // 喪失1・継戦1 → 0.7×1×1=0.7
            Assert.AreEqual(0.7f, FleetTenderRules.SupportLossImpact(1f, 1f), Eps);
            // 喪失0.5・継戦0.6 → 0.7×0.5×0.6=0.21
            Assert.AreEqual(0.21f, FleetTenderRules.SupportLossImpact(0.5f, 0.6f), Eps);
            // 支援に頼っていなければ打撃なし
            Assert.AreEqual(0f, FleetTenderRules.SupportLossImpact(1f, 0f), Eps);
        }

        /// <summary>兵站充足の判定＝継戦力延伸が既定閾値0.4以上で支えられている。</summary>
        [Test]
        public void IsLogisticallySustained_閾値で支援成立を判定する()
        {
            Assert.IsTrue(FleetTenderRules.IsLogisticallySustained(0.5f));
            Assert.IsTrue(FleetTenderRules.IsLogisticallySustained(0.4f)); // 境界（以上）
            Assert.IsFalse(FleetTenderRules.IsLogisticallySustained(0.3f));
            // 明示閾値版
            Assert.IsTrue(FleetTenderRules.IsLogisticallySustained(0.7f, 0.6f));
            Assert.IsFalse(FleetTenderRules.IsLogisticallySustained(0.5f, 0.6f));
        }

        /// <summary>
        /// 物語：イゼルローン回廊の前線で消耗した艦隊。工作艦4隻・設備満載で野戦修理を全開、補給艦4隻で
        /// 洋上補給も全開＝継戦力が延伸し前線に居座る。だが手薄な護衛のまま敵襲で支援艦を半数失うと、
        /// 延ばしていた継戦が剥がれて打撃を被り、補給線は細る＝支援艦は艦隊の生命線でありかつ急所。
        /// </summary>
        [Test]
        public void 物語_前線支援艦が継戦を延ばし喪失で生命線が断たれる()
        {
            // 工作艦4隻×設備満載＝野戦修理全開
            float repair = FleetTenderRules.FieldRepairCapacity(4f, 1f);
            Assert.AreEqual(1f, repair, Eps);
            // 補給艦4隻×移送設備満載＝洋上補給全開
            float replenish = FleetTenderRules.ReplenishmentRate(4f, 1f);
            Assert.AreEqual(1f, replenish, Eps);
            // 両輪が揃い継戦力が上限まで延伸
            float sust = FleetTenderRules.SustainmentExtension(replenish, repair);
            Assert.AreEqual(0.6f, sust, Eps);
            // 延伸が閾値を超え兵站的に支えられている＝前線に居座れる
            Assert.IsTrue(FleetTenderRules.IsLogisticallySustained(sust));

            // だが装甲薄・無護衛の支援艦は脆い
            float vuln = FleetTenderRules.TenderVulnerability(0.2f, 0f);
            Assert.Greater(vuln, 0.5f);
            // 敵襲で半数喪失＝延ばしていた継戦が剥がれ打撃
            float impact = FleetTenderRules.SupportLossImpact(0.5f, sust);
            Assert.Greater(impact, 0f);
            // 支援に頼っていたぶん、頼っていない場合より喪失が痛い
            float impactNoDependence = FleetTenderRules.SupportLossImpact(0.5f, 0f);
            Assert.Greater(impact, impactNoDependence);
        }
    }
}
