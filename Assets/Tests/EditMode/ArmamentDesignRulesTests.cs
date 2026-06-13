using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦艇再設計（#1066）の純ロジック検証。搭載量/電力/スロットの制約・過積載の不正設計検出・
    /// 電力収支・特化ボーナスを既定 Params の具体値で固定する。
    /// </summary>
    public class ArmamentDesignRulesTests
    {
        // 種別ごとの典型モジュール（weight, powerDraw, cost, rating）
        static ShipModule Cannon() => new ShipModule(ModuleType.主砲, 10f, 8f, 30f, 20f);
        static ShipModule Armor() => new ShipModule(ModuleType.装甲, 12f, 0f, 25f, 15f);
        static ShipModule Engine() => new ShipModule(ModuleType.機関, 8f, -30f, 20f, 0f); // 供給30
        static ShipModule Shield() => new ShipModule(ModuleType.シールド, 6f, 10f, 28f, 12f);

        /// <summary>合計重量・コストはモジュールの単純和。</summary>
        [Test]
        public void 総重量とコストは合計()
        {
            var mods = new[] { Cannon(), Armor(), Engine() };
            Assert.AreEqual(30f, ArmamentDesignRules.TotalWeight(mods), 1e-4f);   // 10+12+8
            Assert.AreEqual(75f, ArmamentDesignRules.TotalCost(mods), 1e-4f);     // 30+25+20
        }

        /// <summary>総電力消費は正の powerDraw のみ（機関の供給は含めない）。供給は艦体+機関。</summary>
        [Test]
        public void 電力の消費と供給を分離して集計()
        {
            var hull = new HullSpec(200f, 5f, 6);
            var mods = new[] { Cannon(), Engine(), Shield() }; // 消費 8+10=18／供給 5(艦体)+30(機関)=35
            Assert.AreEqual(18f, ArmamentDesignRules.TotalPowerDraw(mods), 1e-4f);
            Assert.AreEqual(35f, ArmamentDesignRules.TotalPowerSupply(hull, mods), 1e-4f);
        }

        /// <summary>制約をすべて満たす設計は妥当。</summary>
        [Test]
        public void 制約内の設計は妥当()
        {
            var hull = new HullSpec(200f, 5f, 6);
            var mods = new[] { Cannon(), Engine(), Shield() }; // 重量24/200・消費18≤供給35・3枠≤6
            Assert.IsTrue(ArmamentDesignRules.IsValidDesign(hull, mods));
        }

        /// <summary>過積載（搭載量超過）は不正設計として弾く。</summary>
        [Test]
        public void 過積載は不正設計()
        {
            var hull = new HullSpec(15f, 100f, 6); // 搭載量15だけ
            var mods = new[] { Cannon(), Armor() }; // 重量22 > 15
            Assert.IsFalse(ArmamentDesignRules.IsValidDesign(hull, mods));
        }

        /// <summary>スロット数超過・電力不足も不正設計。</summary>
        [Test]
        public void スロット超過と電力不足は不正設計()
        {
            // スロット超過
            var hullSlots = new HullSpec(500f, 500f, 2);
            var many = new[] { Cannon(), Armor(), Shield() }; // 3個 > 2枠
            Assert.IsFalse(ArmamentDesignRules.IsValidDesign(hullSlots, many));

            // 電力不足（機関なし・艦体電力も少ない）
            var hullPower = new HullSpec(500f, 5f, 6);
            var hungry = new[] { Cannon(), Shield() }; // 消費18 > 供給5
            Assert.IsFalse(ArmamentDesignRules.IsValidDesign(hullPower, hungry));
        }

        /// <summary>搭載量利用率と電力収支。収支が負なら電力不足。</summary>
        [Test]
        public void 利用率と電力収支()
        {
            var hull = new HullSpec(100f, 5f, 6);
            var mods = new[] { Cannon(), Armor(), Engine() }; // 重量30/100=0.3・消費8・供給5+30=35
            Assert.AreEqual(0.3f, ArmamentDesignRules.WeightUtilization(hull, mods), 1e-4f);
            Assert.AreEqual(27f, ArmamentDesignRules.PowerBalance(hull, mods), 1e-4f); // 35-8

            // 機関を抜くと電力不足（負）
            var noEngine = new[] { Cannon(), Armor() }; // 消費8・供給5
            Assert.AreEqual(-3f, ArmamentDesignRules.PowerBalance(hull, noEngine), 1e-4f);
        }

        /// <summary>同種集中は特化ボーナスで戦闘力が伸びる（砲艦＝主砲特化）。均等分散はボーナスほぼ0。</summary>
        [Test]
        public void 特化ボーナスは同種集中で増える()
        {
            // 砲艦＝主砲4門（share=1.0）
            var gunship = new[]
            {
                new ShipModule(ModuleType.主砲, 10f, 0f, 30f, 20f),
                new ShipModule(ModuleType.主砲, 10f, 0f, 30f, 20f),
                new ShipModule(ModuleType.主砲, 10f, 0f, 30f, 20f),
                new ShipModule(ModuleType.主砲, 10f, 0f, 30f, 20f),
            };
            // share=1.0, even=1/6 → bonus = 1 + 0.5*(1-1/6) = 1.41667
            float bonus = ArmamentDesignRules.SpecializationBonus(gunship);
            Assert.AreEqual(1.41667f, bonus, 1e-3f);

            // CombatRating = offense(80)*1.0 * bonus
            float rating = ArmamentDesignRules.CombatRating(gunship);
            Assert.AreEqual(80f * 1.41667f, rating, 1e-2f);

            // 各種1個ずつの汎用艦：最大種別share=1/6=even → ボーナス0（倍率1.0）
            var generalist = new[]
            {
                new ShipModule(ModuleType.主砲, 0f, 0f, 0f, 10f),
                new ShipModule(ModuleType.装甲, 0f, 0f, 0f, 10f),
                new ShipModule(ModuleType.機関, 0f, 0f, 0f, 0f),
                new ShipModule(ModuleType.シールド, 0f, 0f, 0f, 10f),
                new ShipModule(ModuleType.電子機器, 0f, 0f, 0f, 10f),
                new ShipModule(ModuleType.格納庫, 0f, 0f, 0f, 10f),
            };
            Assert.AreEqual(1f, ArmamentDesignRules.SpecializationBonus(generalist), 1e-4f);
        }

        /// <summary>戦闘力は攻撃/防御/支援の重み付き合成。</summary>
        [Test]
        public void 戦闘力は重み付き合成()
        {
            // 主砲20・装甲15・電子機器10 を各1個（share=1/3, even=1/6 → bonus=1+0.5*(1/3-1/6)=1.08333）
            var mods = new[]
            {
                new ShipModule(ModuleType.主砲, 0f, 0f, 0f, 20f),
                new ShipModule(ModuleType.装甲, 0f, 0f, 0f, 15f),
                new ShipModule(ModuleType.電子機器, 0f, 0f, 0f, 10f),
            };
            // base = 20*1.0 + 15*0.8 + 10*0.5 = 20 + 12 + 5 = 37
            // bonus = 1 + 0.5*(1/3 - 1/6) = 1.083333
            float expected = 37f * 1.083333f;
            Assert.AreEqual(expected, ArmamentDesignRules.CombatRating(mods), 1e-2f);
        }
    }
}
