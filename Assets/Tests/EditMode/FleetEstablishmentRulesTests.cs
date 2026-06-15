using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>艦隊の設立・解散＋稟議橋渡し（FleetEstablishmentRules・艦隊編制の建議）の EditMode テスト。</summary>
    public class FleetEstablishmentRulesTests
    {
        [SetUp]
        public void Reset() { FleetRoster.Clear(); FleetPool.Clear(); }

        // ===== 設立 =====

        [Test]
        public void CanEstablish_RequiresAvailablePool()
        {
            FleetPool.Set(Faction.同盟, 10000);
            Assert.IsTrue(FleetEstablishmentRules.CanEstablish(Faction.同盟, 8000));
            Assert.IsFalse(FleetEstablishmentRules.CanEstablish(Faction.同盟, 12000)); // プール超過
            Assert.IsFalse(FleetEstablishmentRules.CanEstablish(Faction.同盟, 0));     // 兵力0は不可
        }

        [Test]
        public void Establish_AllocatesFromPool_AndCreatesActiveFleet()
        {
            FleetPool.Set(Faction.同盟, 20000);
            var unit = FleetEstablishmentRules.Establish(Faction.同盟, 12000, "第13艦隊");
            Assert.IsNotNull(unit);
            Assert.IsTrue(unit.IsActive);
            Assert.AreEqual(12000, unit.baseStrength);
            Assert.AreEqual("第13艦隊", unit.fleetName);
            // 割当ぶん available が減る（総プールは不変）
            Assert.AreEqual(8000, FleetPoolRules.Available(Faction.同盟));
            Assert.AreEqual(20000, FleetPool.Get(Faction.同盟));
        }

        [Test]
        public void Establish_FailsWhenPoolInsufficient()
        {
            FleetPool.Set(Faction.同盟, 5000);
            var unit = FleetEstablishmentRules.Establish(Faction.同盟, 12000);
            Assert.IsNull(unit);
            // 台帳を汚さない（巻き戻し）
            Assert.AreEqual(0, FleetPoolRules.Allocated(Faction.同盟));
        }

        // ===== 解散 =====

        [Test]
        public void Disband_FreesAllocationBackToPool()
        {
            FleetPool.Set(Faction.帝国, 20000);
            var unit = FleetEstablishmentRules.Establish(Faction.帝国, 12000);
            Assert.AreEqual(8000, FleetPoolRules.Available(Faction.帝国));

            Assert.IsTrue(FleetEstablishmentRules.CanDisband(Faction.帝国, unit.fleetNumber));
            Assert.IsTrue(FleetEstablishmentRules.Disband(Faction.帝国, unit.fleetNumber));
            // 解隊で割当が外れ available が満額へ回復（総プール不変）
            Assert.AreEqual(20000, FleetPoolRules.Available(Faction.帝国));
            Assert.AreEqual(20000, FleetPool.Get(Faction.帝国));
            Assert.IsFalse(FleetRoster.GetFleet(Faction.帝国, unit.fleetNumber).IsActive);
        }

        [Test]
        public void CanDisband_RejectsMissingOrInactive()
        {
            Assert.IsFalse(FleetEstablishmentRules.CanDisband(Faction.帝国, 1)); // 在席しない
            FleetPool.Set(Faction.帝国, 20000);
            var unit = FleetEstablishmentRules.Establish(Faction.帝国, 5000);
            FleetEstablishmentRules.Disband(Faction.帝国, unit.fleetNumber);
            Assert.IsFalse(FleetEstablishmentRules.CanDisband(Faction.帝国, unit.fleetNumber)); // 既に解隊
        }

        // ===== 建議の執行 =====

        [Test]
        public void Execute_Establish_ScalesStrengthByMagnitude()
        {
            FleetPool.Set(Faction.同盟, 20000);
            var p = new FleetProposal(Faction.同盟, FleetProposalKind.設立, 12000);
            Assert.IsTrue(FleetEstablishmentRules.Execute(p, 0.5f)); // 官僚が半分に値切る
            Assert.Greater(p.fleetNumber, 0);                       // 実番号が書き戻る
            var unit = FleetRoster.GetFleet(Faction.同盟, p.fleetNumber);
            Assert.AreEqual(6000, unit.baseStrength);
        }

        [Test]
        public void Execute_Establish_FailsWhenBelowMinAfterClamp()
        {
            FleetPool.Set(Faction.同盟, 500); // 最小規模未満しか割けない
            var p = new FleetProposal(Faction.同盟, FleetProposalKind.設立, 12000);
            Assert.IsFalse(FleetEstablishmentRules.Execute(p, 1f));
        }

        [Test]
        public void Execute_Disband_IgnoresMagnitude()
        {
            FleetPool.Set(Faction.帝国, 20000);
            var u = FleetEstablishmentRules.Establish(Faction.帝国, 5000);
            var p = new FleetProposal(Faction.帝国, FleetProposalKind.解散, fleetNumber: u.fleetNumber);
            Assert.IsTrue(FleetEstablishmentRules.Execute(p, 0f));
            Assert.IsFalse(FleetRoster.GetFleet(Faction.帝国, u.fleetNumber).IsActive);
        }

        // ===== 稟議への橋渡し（Encode/Decode/Petition） =====

        [Test]
        public void EncodeDecode_Roundtrips()
        {
            var est = new FleetProposal(Faction.同盟, FleetProposalKind.設立, 12000);
            var back = FleetEstablishmentRules.Decode(Faction.同盟, FleetEstablishmentRules.EncodeEffectKey(est));
            Assert.AreEqual(FleetProposalKind.設立, back.kind);
            Assert.AreEqual(12000, back.strength);

            var dis = new FleetProposal(Faction.帝国, FleetProposalKind.解散, fleetNumber: 3);
            var back2 = FleetEstablishmentRules.Decode(Faction.帝国, FleetEstablishmentRules.EncodeEffectKey(dis));
            Assert.AreEqual(FleetProposalKind.解散, back2.kind);
            Assert.AreEqual(3, back2.fleetNumber);
        }

        [Test]
        public void Decode_RejectsNonFleetKeys()
        {
            Assert.IsNull(FleetEstablishmentRules.Decode(Faction.同盟, "tax.cut"));
            Assert.IsNull(FleetEstablishmentRules.Decode(Faction.同盟, ""));
            Assert.IsNull(FleetEstablishmentRules.Decode(Faction.同盟, "fleet.establish:abc"));
        }

        [Test]
        public void ToPetition_And_ExecuteFromPetition()
        {
            FleetPool.Set(Faction.同盟, 20000);
            var p = new FleetProposal(Faction.同盟, FleetProposalKind.設立, 12000, proposerId: 7);
            var pet = FleetEstablishmentRules.ToPetition(p, id: 100);
            Assert.AreEqual(Faction.同盟, pet.faction);
            Assert.AreEqual(7, pet.drafterId);
            Assert.IsTrue(FleetEstablishmentRules.IsFleetPetition(pet));

            Assert.IsTrue(FleetEstablishmentRules.ExecuteFromPetition(pet, 1f));
            Assert.AreEqual(12000, FleetPoolRules.Allocated(Faction.同盟));
        }

        [Test]
        public void IsFleetPetition_FalseForTaxPetition()
        {
            var tax = new Petition(1, "減税", Faction.同盟, BoxKind.国王, PetitionOrigin.建白, "tax.cut");
            Assert.IsFalse(FleetEstablishmentRules.IsFleetPetition(tax));
        }

        // ===== AIの自動建議 =====

        [Test]
        public void RecommendProposal_EstablishesWhenPoolSurplus()
        {
            FleetPool.Set(Faction.帝国, FleetEstablishmentRules.StandardFleetStrength + 5000);
            var p = FleetEstablishmentRules.RecommendProposal(Faction.帝国);
            Assert.IsNotNull(p);
            Assert.AreEqual(FleetProposalKind.設立, p.kind);
            Assert.AreEqual(FleetEstablishmentRules.StandardFleetStrength, p.strength);
        }

        [Test]
        public void RecommendProposal_DisbandsEmptyHusk()
        {
            // プール残は標準規模未満だが、兵力0の空艦隊がある＝解散を勧める
            FleetPool.Set(Faction.帝国, 1000);
            FleetRoster.CreateFleet(Faction.帝国); // baseStrength 0 の現役
            var p = FleetEstablishmentRules.RecommendProposal(Faction.帝国);
            Assert.IsNotNull(p);
            Assert.AreEqual(FleetProposalKind.解散, p.kind);
        }

        [Test]
        public void RecommendProposal_NullWhenNothingToDo()
        {
            FleetPool.Set(Faction.帝国, 1000); // 余剰なし
            // 兵力ありの現役艦隊だけ（空殻なし）
            var u = FleetRoster.CreateFleet(Faction.帝国);
            u.baseStrength = 800;
            Assert.IsNull(FleetEstablishmentRules.RecommendProposal(Faction.帝国));
        }
    }
}
