using System.Collections.Generic;
using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>BattleAllegianceRules（#817 会戦配線＝旗幟の遷移収集・寝返りロック・静観退き）のテスト。</summary>
    public class BattleAllegianceRulesTests
    {
        private static LoyaltyParams P => LoyaltyParams.Default;

        /// <summary>忠実な諸侯だけなら、初回解決は全員「未定→戦う」になる。</summary>
        [Test]
        public void 初回解決_忠実な諸侯は全員戦うへ遷移する()
        {
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 100, loyalty: 1f),
                new Allegiance(2, Faction.帝国, 100, loyalty: 1f),
            };
            var changes = new List<StanceChange>();
            int n = BattleAllegianceRules.ResolveTransitions(list, Faction.同盟, Faction.帝国, P, changes);

            Assert.AreEqual(2, n);
            Assert.IsTrue(changes.TrueForAll(c => c.from == Stance.未定 && c.to == Stance.戦う));
        }

        /// <summary>調略済みの劣勢側諸侯は寝返り、寝返りは locked で確定する。</summary>
        [Test]
        public void 調略済みで劣勢なら寝返り_寝返りはロックされる()
        {
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 100, loyalty: 0.9f),
                new Allegiance(2, Faction.同盟, 80, loyalty: 0.3f, intrigue: 0.8f), // 小早川型
                new Allegiance(3, Faction.帝国, 300, loyalty: 0.9f),                 // 帝国が優勢
            };
            var changes = new List<StanceChange>();
            BattleAllegianceRules.ResolveTransitions(list, Faction.同盟, Faction.帝国, P, changes);

            Assert.AreEqual(Stance.寝返り, list[1].stance);
            Assert.IsTrue(list[1].locked, "寝返りは不可逆ロックされる");
            Assert.IsTrue(changes.Exists(c => c.id == 2 && c.to == Stance.寝返り));
        }

        /// <summary>ロック済みの寝返り諸侯は、趨勢が逆転しても寝返り返りしない。</summary>
        [Test]
        public void ロック済みは趨勢逆転でも再遷移しない()
        {
            var defector = new Allegiance(2, Faction.同盟, 80, loyalty: 0.3f, intrigue: 0.8f)
            { stance = Stance.寝返り, locked = true };
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 500, loyalty: 1f), // 同盟が大優勢になった
                defector,
                new Allegiance(3, Faction.帝国, 50, loyalty: 1f),
            };
            var changes = new List<StanceChange>();
            BattleAllegianceRules.ResolveTransitions(list, Faction.同盟, Faction.帝国, P, changes);

            Assert.AreEqual(Stance.寝返り, defector.stance, "寝返り返りはしない");
            Assert.IsFalse(changes.Exists(c => c.id == 2));
        }

        /// <summary>会戦中に戦力が削れて劣勢化すると、次の解決で寝返りが発火する（趨勢変化のフリップ）。</summary>
        [Test]
        public void 会戦中の戦力減で寝返りが発火する()
        {
            var waverer = new Allegiance(2, Faction.同盟, 100, loyalty: 0.3f, intrigue: 0.8f);
            var ally = new Allegiance(1, Faction.同盟, 200, loyalty: 1f);
            var enemy = new Allegiance(3, Faction.帝国, 150, loyalty: 1f);
            var list = new List<Allegiance> { ally, waverer, enemy };

            // 開戦時：同盟優勢（200+静観100 vs 150）→ 寝返りは起きない
            var changes = new List<StanceChange>();
            BattleAllegianceRules.ResolveTransitions(list, Faction.同盟, Faction.帝国, P, changes);
            Assert.AreNotEqual(Stance.寝返り, waverer.stance);

            // 会戦が進み同盟主力が大損耗（戦略＝現在戦力の同期）→ 劣勢化
            ally.strength = 50;
            changes.Clear();
            BattleAllegianceRules.ResolveTransitions(list, Faction.同盟, Faction.帝国, P, changes);

            Assert.AreEqual(Stance.寝返り, waverer.stance, "劣勢化で調略済み諸侯が寝返る");
            Assert.IsTrue(changes.Exists(c => c.id == 2 && c.to == Stance.寝返り));
        }

        /// <summary>戦う者が尽きた側の静観組は退き条件を満たす（敵側が残存している場合のみ）。</summary>
        [Test]
        public void 静観退き_自軍の戦う者が尽きたら退く()
        {
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 0, loyalty: 1f) { stance = Stance.戦う },    // 壊滅
                new Allegiance(2, Faction.同盟, 100, loyalty: 0.4f) { stance = Stance.静観 }, // 毛利型
                new Allegiance(3, Faction.帝国, 100, loyalty: 1f) { stance = Stance.戦う },
            };
            Assert.IsTrue(BattleAllegianceRules.ShouldWithdraw(list, Faction.同盟, Faction.帝国));
            Assert.IsFalse(BattleAllegianceRules.ShouldWithdraw(list, Faction.帝国, Faction.同盟), "敵側はまだ戦力がある");
        }

        /// <summary>両軍とも戦力ゼロなら退き条件は立たない（決着は BattleManager の領分）。</summary>
        [Test]
        public void 静観退き_双方ゼロなら退かない()
        {
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 0) { stance = Stance.戦う },
                new Allegiance(2, Faction.帝国, 0) { stance = Stance.戦う },
            };
            Assert.IsFalse(BattleAllegianceRules.ShouldWithdraw(list, Faction.同盟, Faction.帝国));
        }

        /// <summary>膠着打開：双方が全員静観で戦う者が居なければ、各陣営の最忠実な前衛が「戦う」へ転じる。</summary>
        [Test]
        public void 膠着打開_双方静観なら各陣営の前衛が開戦する()
        {
            // baseline 0.4 想定（intrigue=0）＝net<0.5 で全員静観＝両軍膠着
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 100, loyalty: 0.4f) { stance = Stance.静観 },
                new Allegiance(2, Faction.同盟, 120, loyalty: 0.45f) { stance = Stance.静観 }, // 同盟で最忠実
                new Allegiance(3, Faction.帝国, 90, loyalty: 0.35f) { stance = Stance.静観 },
                new Allegiance(4, Faction.帝国, 80, loyalty: 0.3f) { stance = Stance.静観 },
            };
            var changes = new List<StanceChange>();
            int n = BattleAllegianceRules.BreakStalemate(list, Faction.同盟, Faction.帝国, changes);

            Assert.AreEqual(2, n, "各陣営から1隊ずつ開戦する");
            Assert.AreEqual(Stance.戦う, list[1].stance, "同盟は最忠実な #2 が前衛に立つ");
            Assert.AreEqual(Stance.戦う, list[2].stance, "帝国は最忠実な #3 が前衛に立つ");
            Assert.IsTrue(changes.Exists(c => c.id == 2 && c.to == Stance.戦う));
            Assert.IsTrue(changes.Exists(c => c.id == 3 && c.to == Stance.戦う));
        }

        /// <summary>膠着打開：既にどちらかが戦っている（趨勢がある）なら何もしない。</summary>
        [Test]
        public void 膠着打開_片側が戦っていれば介入しない()
        {
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 100, loyalty: 1f) { stance = Stance.戦う },
                new Allegiance(2, Faction.帝国, 100, loyalty: 0.4f) { stance = Stance.静観 },
            };
            var changes = new List<StanceChange>();
            int n = BattleAllegianceRules.BreakStalemate(list, Faction.同盟, Faction.帝国, changes);

            Assert.AreEqual(0, n);
            Assert.AreEqual(Stance.静観, list[1].stance, "敵が戦っている側は静観退き(ShouldWithdraw)の領分");
        }

        /// <summary>null 安全：null リストは遷移0・退き false・膠着打開0。</summary>
        [Test]
        public void Null安全()
        {
            Assert.AreEqual(0, BattleAllegianceRules.ResolveTransitions(null, Faction.同盟, Faction.帝国, P, new List<StanceChange>()));
            Assert.IsFalse(BattleAllegianceRules.ShouldWithdraw(null, Faction.同盟, Faction.帝国));
            Assert.AreEqual(0, BattleAllegianceRules.BreakStalemate(null, Faction.同盟, Faction.帝国, new List<StanceChange>()));
        }
    }
}
