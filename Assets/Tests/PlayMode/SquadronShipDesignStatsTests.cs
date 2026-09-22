using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// Squadron → ShipClassStats への艦艇設計反映（#1066 第2段階）を固定する。
    /// 所属勢力の現役 ShipDesign（ShipDesignRules.ActiveFor→PerformanceOf）が
    /// firepower/durability/mobility 系の実効倍率へ乗り、艦種・勢力で仕分けされ、
    /// 設計なし・不正設計・Campaign/FactionState なしは従来値のまま、多重呼出しでも累積しない。
    /// </summary>
    public class SquadronShipDesignStatsTests
    {
        private CampaignState previousCampaign;

        [SetUp]
        public void SetUp()
        {
            previousCampaign = StrategySession.Campaign;
        }

        [TearDown]
        public void TearDown()
        {
            StrategySession.Campaign = previousCampaign;
        }

        static HullSpec Hull() => new HullSpec(80f, 10f, 4, "標準艦体");
        static HullSpec WeakPowerHull() => new HullSpec(100f, 10f, 2, "過電型艦体");
        static ShipModule Engine() => new ShipModule(ModuleType.機関, 10f, -30f, 20f, 20f, "標準機関");
        static ShipModule Gun() => new ShipModule(ModuleType.主砲, 15f, 12f, 30f, 25f, "標準主砲");
        static ShipModule Armor() => new ShipModule(ModuleType.装甲, 12f, 0f, 25f, 30f, "標準装甲");

        [Test]
        public void 現役設計は同じ艦種の実効倍率へ反映され他艦種は従来値のまま()
        {
            var campaign = new CampaignState(new GalaxyMap());
            var imperial = new FactionState(Faction.帝国);
            campaign.states.Add(imperial);
            ShipDesignRules.TryRegister(imperial.shipDesigns, "帝国戦艦I", ShipClass.戦艦,
                Hull(), new[] { Engine(), Gun(), Armor() }, out ShipDesign design, out _);
            ShipDesignPerformance perf = ShipDesignRules.PerformanceOf(design);
            StrategySession.Campaign = campaign;

            var root = new GameObject("squadron-shipdesign-class-test");
            var members = new List<GameObject>();
            try
            {
                Squadron squadron = CreateSquadron(root, Faction.帝国, 10, members);
                RunClassSetup(squadron);

                // 戦艦の現役設計＝主砲+機関+装甲 → 火力・機動・耐久が設計由来で上乗せされる。
                EscortShip battleship = members[0].GetComponent<EscortShip>();
                Assert.AreEqual(ShipClass.戦艦, battleship.shipClass);
                Assert.Greater(battleship.firepowerMultiplier, squadron.battleshipStats.firepowerMultiplier);
                Assert.AreEqual(squadron.battleshipStats.firepowerMultiplier * perf.firepower,
                    battleship.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(squadron.battleshipStats.speedMultiplier * perf.mobility,
                    battleship.speedMultiplier, 1e-4f);
                Assert.AreEqual(Mathf.RoundToInt(squadron.escortShipCount
                    * squadron.battleshipStats.durabilityMultiplier * perf.durability),
                    battleship.shipCount);
                Assert.Greater(battleship.shipCount,
                    Mathf.RoundToInt(squadron.escortShipCount * squadron.battleshipStats.durabilityMultiplier));

                // 設計は戦艦だけ → 巡航艦・駆逐艦は従来値（基準）のまま。
                EscortShip cruiser = members[5].GetComponent<EscortShip>();
                Assert.AreEqual(ShipClass.巡航艦, cruiser.shipClass);
                Assert.AreEqual(squadron.cruiserStats.firepowerMultiplier, cruiser.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(squadron.cruiserStats.speedMultiplier, cruiser.speedMultiplier, 1e-4f);
                EscortShip destroyer = members[9].GetComponent<EscortShip>();
                Assert.AreEqual(ShipClass.駆逐艦, destroyer.shipClass);
                Assert.AreEqual(squadron.destroyerStats.firepowerMultiplier, destroyer.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(squadron.destroyerStats.speedMultiplier, destroyer.speedMultiplier, 1e-4f);

                // 表示系（classTint）は設計非接続＝基準のまま。
                Assert.AreEqual(squadron.battleshipStats.tint, battleship.classTint);
            }
            finally
            {
                DestroyAll(root, members);
            }
        }

        [Test]
        public void 設計のある勢力と無い勢力で実効値が分かれる()
        {
            var campaign = new CampaignState(new GalaxyMap());
            var imperial = new FactionState(Faction.帝国);
            campaign.states.Add(imperial);
            campaign.states.Add(new FactionState(Faction.同盟));
            ShipDesignRules.TryRegister(imperial.shipDesigns, "帝国戦艦I", ShipClass.戦艦,
                Hull(), new[] { Engine(), Gun(), Armor() }, out ShipDesign design, out _);
            ShipDesignPerformance perf = ShipDesignRules.PerformanceOf(design);
            StrategySession.Campaign = campaign;

            var imperialRoot = new GameObject("squadron-shipdesign-imperial-test");
            var allianceRoot = new GameObject("squadron-shipdesign-alliance-test");
            var imperialMembers = new List<GameObject>();
            var allianceMembers = new List<GameObject>();
            try
            {
                Squadron imperialSquadron = CreateSquadron(imperialRoot, Faction.帝国, 10, imperialMembers);
                Squadron allianceSquadron = CreateSquadron(allianceRoot, Faction.同盟, 10, allianceMembers);
                RunClassSetup(imperialSquadron);
                RunClassSetup(allianceSquadron);

                // 帝国：現役設計が乗る。
                EscortShip imperialBb = imperialMembers[0].GetComponent<EscortShip>();
                Assert.AreEqual(imperialSquadron.battleshipStats.firepowerMultiplier * perf.firepower,
                    imperialBb.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(imperialSquadron.battleshipStats.speedMultiplier * perf.mobility,
                    imperialBb.speedMultiplier, 1e-4f);
                Assert.AreEqual(Mathf.RoundToInt(imperialSquadron.escortShipCount
                    * imperialSquadron.battleshipStats.durabilityMultiplier * perf.durability), imperialBb.shipCount);

                // 同盟：設計台帳が空＝従来値（基準）のまま。
                EscortShip allianceBb = allianceMembers[0].GetComponent<EscortShip>();
                Assert.AreEqual(allianceSquadron.battleshipStats.firepowerMultiplier,
                    allianceBb.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(allianceSquadron.battleshipStats.speedMultiplier,
                    allianceBb.speedMultiplier, 1e-4f);
                Assert.AreEqual(Mathf.RoundToInt(allianceSquadron.escortShipCount
                    * allianceSquadron.battleshipStats.durabilityMultiplier), allianceBb.shipCount);
            }
            finally
            {
                DestroyAll(imperialRoot, imperialMembers);
                DestroyAll(allianceRoot, allianceMembers);
            }
        }

        [Test]
        public void 多重呼出しでも実効値は累積しない()
        {
            var campaign = new CampaignState(new GalaxyMap());
            var imperial = new FactionState(Faction.帝国);
            campaign.states.Add(imperial);
            ShipDesignRules.TryRegister(imperial.shipDesigns, "帝国戦艦I", ShipClass.戦艦,
                Hull(), new[] { Engine(), Gun(), Armor() }, out _, out _);
            StrategySession.Campaign = campaign;

            var root = new GameObject("squadron-shipdesign-idempotent-test");
            var members = new List<GameObject>();
            try
            {
                Squadron squadron = CreateSquadron(root, Faction.帝国, 10, members);
                RunClassSetup(squadron);
                float onceFirepower = members[0].GetComponent<EscortShip>().firepowerMultiplier;
                float onceSpeed = members[0].GetComponent<EscortShip>().speedMultiplier;
                int onceShipCount = members[0].GetComponent<EscortShip>().shipCount;

                RunClassSetup(squadron);
                RunClassSetup(squadron);

                EscortShip battleship = members[0].GetComponent<EscortShip>();
                Assert.AreEqual(onceFirepower, battleship.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(onceSpeed, battleship.speedMultiplier, 1e-4f);
                Assert.AreEqual(onceShipCount, battleship.shipCount);

                // 共有基準（Inspector 調整値）も破壊されていない。
                Assert.AreEqual(1.6f, squadron.battleshipStats.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(2.0f, squadron.battleshipStats.durabilityMultiplier, 1e-4f);
                Assert.AreEqual(0.7f, squadron.battleshipStats.speedMultiplier, 1e-4f);
            }
            finally
            {
                DestroyAll(root, members);
            }
        }

        [Test]
        public void Campaignが無いときは従来値のまま()
        {
            StrategySession.Campaign = null;

            var root = new GameObject("squadron-shipdesign-no-campaign-test");
            var members = new List<GameObject>();
            try
            {
                Squadron squadron = CreateSquadron(root, Faction.帝国, 10, members);
                RunClassSetup(squadron);

                EscortShip battleship = members[0].GetComponent<EscortShip>();
                Assert.AreEqual(squadron.battleshipStats.firepowerMultiplier, battleship.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(squadron.battleshipStats.speedMultiplier, battleship.speedMultiplier, 1e-4f);
                Assert.AreEqual(Mathf.RoundToInt(squadron.escortShipCount
                    * squadron.battleshipStats.durabilityMultiplier), battleship.shipCount);
            }
            finally
            {
                DestroyAll(root, members);
            }
        }

        [Test]
        public void 所属FactionStateが無いときは従来値のまま()
        {
            // 盤面には同盟しかいないのに、帝国の艦隊が来た場合。
            var campaign = new CampaignState(new GalaxyMap());
            campaign.states.Add(new FactionState(Faction.同盟));
            StrategySession.Campaign = campaign;

            var root = new GameObject("squadron-shipdesign-no-faction-test");
            var members = new List<GameObject>();
            try
            {
                Squadron squadron = CreateSquadron(root, Faction.帝国, 10, members);
                RunClassSetup(squadron);

                EscortShip battleship = members[0].GetComponent<EscortShip>();
                Assert.AreEqual(squadron.battleshipStats.firepowerMultiplier, battleship.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(squadron.battleshipStats.speedMultiplier, battleship.speedMultiplier, 1e-4f);
            }
            finally
            {
                DestroyAll(root, members);
            }
        }

        [Test]
        public void 現役設計が不正でも従来値のまま()
        {
            var campaign = new CampaignState(new GalaxyMap());
            var imperial = new FactionState(Faction.帝国);
            campaign.states.Add(imperial);
            // 電力超過の不正設計を、検証を通さず手で現役として入れる
            // （ActiveFor は拾うが PerformanceOf が基準倍率へ落とす経路の固定）。
            imperial.shipDesigns.designs.Add(new ShipDesign
            {
                id = 1,
                designName = "過電型",
                shipClass = ShipClass.戦艦,
                hull = WeakPowerHull(),
                modules = new[] { Gun() },
                active = true
            });
            StrategySession.Campaign = campaign;

            var root = new GameObject("squadron-shipdesign-invalid-test");
            var members = new List<GameObject>();
            try
            {
                Squadron squadron = CreateSquadron(root, Faction.帝国, 10, members);
                RunClassSetup(squadron);

                EscortShip battleship = members[0].GetComponent<EscortShip>();
                Assert.AreEqual(squadron.battleshipStats.firepowerMultiplier, battleship.firepowerMultiplier, 1e-4f);
                Assert.AreEqual(squadron.battleshipStats.speedMultiplier, battleship.speedMultiplier, 1e-4f);
                Assert.AreEqual(Mathf.RoundToInt(squadron.escortShipCount
                    * squadron.battleshipStats.durabilityMultiplier), battleship.shipCount);
            }
            finally
            {
                DestroyAll(root, members);
            }
        }

        private static Squadron CreateSquadron(GameObject root, Faction faction, int memberCount, List<GameObject> members)
        {
            var squadron = root.AddComponent<Squadron>();
            // Squadron.Awake が RequireComponent(FleetStrength) の FleetSustainment を追加するため、
            // 既に自動生成された旗艦強度を再利用して二重コンポーネント化を防ぐ。
            var strength = root.GetComponent<FleetStrength>();
            if (strength == null) strength = root.AddComponent<FleetStrength>();
            strength.faction = faction;
            squadron.escortCount = memberCount; // 手置きメンバで充足＝GenerateEscorts は走らせない
            for (int i = 0; i < memberCount; i++)
            {
                var member = new GameObject($"member-{i}");
                member.transform.SetParent(root.transform, false);
                squadron.memberShips.Add(member.transform);
                members.Add(member);
            }
            return squadron;
        }

        private static void RunClassSetup(Squadron squadron)
        {
            InvokePrivate(squadron, "AssignShipClasses");
            InvokePrivate(squadron, "SetupEscorts");
        }

        private static void InvokePrivate(Squadron squadron, string methodName)
        {
            typeof(Squadron).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(squadron, null);
        }

        private static void DestroyAll(GameObject root, List<GameObject> members)
        {
            if (root != null) Object.DestroyImmediate(root);
            foreach (var member in members)
                if (member != null) Object.DestroyImmediate(member);
        }
    }
}
