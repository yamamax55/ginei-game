using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 土地の購入・売却・貸出（#2019・<see cref="LandTransactionRules"/>／<see cref="LandLeaseRegistry"/>）と zoned 権利の
    /// 全惑星換算集計（<see cref="PlanetLandRules"/>）を固定する。
    /// </summary>
    public class LandTransactionRulesTests
    {
        [SetUp]
        public void Clear() { PropertyDeedRegistry.Clear(); LandLeaseRegistry.Clear(); }

        private static PropertyDeed ZonedDeed(int sys, LandUseType use, float share, OwnerRef owner, float baseValue = 500f)
        {
            var d = new PropertyDeed(0, sys, share, baseValue) { zoned = true, landUse = use, rentRate = 0.04f };
            owner.ApplyTo(d);
            return PropertyDeedRegistry.Register(d);
        }

        // ===== 価格・地代 =====
        [Test]
        public void Price_IsBaseValueTimesShare()
        {
            var d = ZonedDeed(5, LandUseType.鉱山, 0.4f, OwnerRef.Person(1));
            Assert.AreEqual(100f, LandTransactionRules.Price(d, 0.2f), 1e-3f);        // 500×0.2
            Assert.AreEqual(120f, LandTransactionRules.Price(d, 0.2f, 1.2f), 1e-3f);  // ×プレミアム
            Assert.AreEqual(200f, LandTransactionRules.Price(d, 0.9f), 1e-3f);        // 持分上限0.4でクランプ＝500×0.4
        }

        [Test]
        public void LeaseAnnualRent_IsZoneValueTimesShareTimesRate()
        {
            Assert.AreEqual(4f, LandTransactionRules.LeaseAnnualRent(500f, 0.2f, 0.04f), 1e-3f);
        }

        // ===== 購入・売却（持分の移転） =====
        [Test]
        public void TransferShare_MovesShareAndCreatesBuyerDeed()
        {
            var seller = ZonedDeed(5, LandUseType.住宅地, 0.4f, OwnerRef.Person(1));
            float moved = LandTransactionRules.TransferShare(seller, OwnerRef.Enterprise(42), 0.1f);
            Assert.AreEqual(0.1f, moved, 1e-4f);
            Assert.AreEqual(0.3f, seller.share, 1e-4f); // 売主は減る
            PropertyDeed buyer = LandTransactionRules.FindDeed(5, true, LandUseType.住宅地, OwnerRef.Enterprise(42));
            Assert.IsNotNull(buyer);
            Assert.AreEqual(0.1f, buyer.share, 1e-4f);   // 買主に同区画の権利
            Assert.AreEqual(LandUseType.住宅地, buyer.landUse);
            Assert.IsTrue(buyer.zoned);
        }

        [Test]
        public void TransferShare_CapsAtSellerShare()
        {
            var seller = ZonedDeed(5, LandUseType.農地, 0.3f, OwnerRef.Person(1));
            float moved = LandTransactionRules.TransferShare(seller, OwnerRef.State(Faction.同盟), 0.5f);
            Assert.AreEqual(0.3f, moved, 1e-4f); // 保有を超えない
            Assert.AreEqual(0f, seller.share, 1e-4f);
        }

        [Test]
        public void TransferShare_AddsToExistingBuyerDeed()
        {
            var seller = ZonedDeed(5, LandUseType.工業地, 0.5f, OwnerRef.Person(1));
            ZonedDeed(5, LandUseType.工業地, 0.2f, OwnerRef.Enterprise(42)); // 買主は既に同区画を保有
            LandTransactionRules.TransferShare(seller, OwnerRef.Enterprise(42), 0.1f);
            PropertyDeed buyer = LandTransactionRules.FindDeed(5, true, LandUseType.工業地, OwnerRef.Enterprise(42));
            Assert.AreEqual(0.3f, buyer.share, 1e-4f); // 0.2＋0.1（重複生成しない）
        }

        [Test]
        public void Sell_ReturnsPriceAndMovedShare()
        {
            var seller = ZonedDeed(5, LandUseType.商業地, 0.4f, OwnerRef.Person(1));
            float price = LandTransactionRules.Sell(seller, OwnerRef.Person(2), 0.2f, out float moved);
            Assert.AreEqual(0.2f, moved, 1e-4f);
            Assert.AreEqual(100f, price, 1e-3f); // 500×0.2
        }

        // ===== 貸出（リース）＝所有権は移さず用益を貸す =====
        [Test]
        public void Lease_CreatesContract_OwnershipUnchanged()
        {
            var deed = ZonedDeed(5, LandUseType.住宅地, 0.6f, OwnerRef.Person(1));
            LandLease lease = LandTransactionRules.Lease(deed, OwnerRef.Enterprise(42), 0.3f, 0.05f, termYears: 5, startYear: 800);
            Assert.IsNotNull(lease);
            Assert.AreEqual(1, LandLeaseRegistry.All.Count);
            Assert.AreEqual(0.6f, deed.share, 1e-4f);   // 所有権（持分）は不変＝貸主のまま
            Assert.AreEqual("P:1", lease.Lessor.Key);
            Assert.AreEqual("E:42", lease.Lessee.Key);
            Assert.AreEqual(7.5f, lease.annualRent, 1e-3f); // 500×0.3×0.05
            Assert.AreEqual(1, LandLeaseRegistry.ByLessor(OwnerRef.Person(1)).Count);
            Assert.AreEqual(1, LandLeaseRegistry.ByLessee(OwnerRef.Enterprise(42)).Count);
        }

        [Test]
        public void Lease_ExpiryAndCleanup()
        {
            var deed = ZonedDeed(5, LandUseType.農地, 0.5f, OwnerRef.Person(1));
            LandLease lease = LandTransactionRules.Lease(deed, OwnerRef.State(Faction.帝国), 0.2f, 0.04f, termYears: 5, startYear: 800);
            Assert.IsFalse(LandTransactionRules.IsExpired(lease, 804));
            Assert.IsTrue(LandTransactionRules.IsExpired(lease, 805));
            Assert.AreEqual(1, LandTransactionRules.ExpireDue(805)); // 満了を整理
            Assert.AreEqual(0, LandLeaseRegistry.All.Count);
        }

        // ===== zoned 権利を全惑星換算で集計（土地分割の所有比率） =====
        [Test]
        public void PlanetLand_ZonedPrivateShare_IsApportioned()
        {
            // 居住惑星：住宅地0.5。個人が住宅地の40%を持つ＝全惑星換算 0.2
            ZonedDeed(5, LandUseType.住宅地, 0.4f, OwnerRef.Person(1));
            Assert.AreEqual(0.2f, PlanetLandRules.PersonShare(5, SystemType.居住), 1e-4f);
            Assert.AreEqual(0.2f, PlanetLandRules.PrivateShare(5, SystemType.居住), 1e-4f);
            Assert.AreEqual(0.8f, PlanetLandRules.StateShare(5, SystemType.居住), 1e-4f);

            // 国家が残余を所有＝完全所有
            PropertyDeed state = PlanetLandRules.EnsureStateDeed(5, Faction.帝国, 1000f, true, SystemType.居住);
            Assert.AreEqual(0.8f, state.share, 1e-4f);
            Assert.IsFalse(state.zoned); // 国家の残余は惑星全体
        }

        [Test]
        public void PlanetLand_SyncValuations_ZonedGetsZoneValue()
        {
            ZonedDeed(5, LandUseType.住宅地, 0.4f, OwnerRef.Person(1));
            var whole = new PropertyDeed(0, 5, 0.1f, 0f) { zoned = false };
            OwnerRef.Person(2).ApplyTo(whole);
            PropertyDeedRegistry.Register(whole);
            PlanetLandRules.SyncValuations(5, 1000f, SystemType.居住);
            PropertyDeed zoned = LandTransactionRules.FindDeed(5, true, LandUseType.住宅地, OwnerRef.Person(1));
            Assert.AreEqual(500f, zoned.baseValue, 1e-3f); // 地価1000×住宅地0.5
            Assert.AreEqual(1000f, whole.baseValue, 1e-3f); // whole は地価そのまま
        }
    }
}
