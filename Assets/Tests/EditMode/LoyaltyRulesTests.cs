using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 関ヶ原型「戦う前に決まる戦い」（#817 SEKI）の純ロジックを固定する：
    /// 旗幟判定（忠誠/調略/趨勢）・実効兵力（兵力≠実働＝フリーライダー）・寝返りカスケード
    /// （ナッシュ均衡崩壊）・調略（戦前プログラミング）で勝敗が決まること。
    /// 帝国＝東軍(家康)／同盟＝西軍(三成) に見立てる。
    /// </summary>
    public class LoyaltyRulesTests
    {
        private static readonly LoyaltyParams P = LoyaltyParams.Default; // fight 0.5 / defect 0.5

        // ───────── 旗幟判定 ResolveStance ─────────

        [Test]
        public void Loyal_Fights_RegardlessOfMomentum()
        {
            var a = new Allegiance(1, Faction.同盟, 1000, loyalty: 1.0f, intrigue: 0f);
            Assert.AreEqual(Stance.戦う, LoyaltyRules.ResolveStance(a, -0.9f, P)); // 劣勢でも忠誠なら戦う
        }

        [Test]
        public void Bribed_And_Losing_Defects()
        {
            var a = new Allegiance(1, Faction.同盟, 1000, loyalty: 0.3f, intrigue: 0.8f);
            Assert.AreEqual(Stance.寝返り, LoyaltyRules.ResolveStance(a, -0.5f, P)); // 調略済み＋自軍劣勢＝寝返り
        }

        [Test]
        public void Bribed_But_Winning_Watches()
        {
            var a = new Allegiance(1, Faction.同盟, 1000, loyalty: 0.3f, intrigue: 0.8f);
            Assert.AreEqual(Stance.静観, LoyaltyRules.ResolveStance(a, +0.5f, P)); // 自軍優勢なら寝返らず様子見
        }

        [Test]
        public void Uncertain_FreeRides()
        {
            var a = new Allegiance(1, Faction.同盟, 1000, loyalty: 0.4f, intrigue: 0.1f);
            Assert.AreEqual(Stance.静観, LoyaltyRules.ResolveStance(a, -0.3f, P)); // 忠誠も調略も中途半端＝静観
        }

        // ───────── 実効兵力（兵力≠実働）─────────

        [Test]
        public void EffectiveStrength_CountsFightersAndDefectorsOnly()
        {
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 100) { stance = Stance.戦う },   // 帝国に+100
                new Allegiance(2, Faction.同盟, 200) { stance = Stance.寝返り }, // 同盟→帝国へ寝返り＝帝国に+200
                new Allegiance(3, Faction.同盟, 500) { stance = Stance.静観 },   // 静観＝どちらにも数えない
                new Allegiance(4, Faction.同盟, 300) { stance = Stance.戦う },   // 同盟に+300
            };
            Assert.AreEqual(300, LoyaltyRules.EffectiveStrength(list, Faction.帝国)); // 100+200
            Assert.AreEqual(300, LoyaltyRules.EffectiveStrength(list, Faction.同盟)); // 300（500は静観で除外）
            Assert.AreEqual(1000, LoyaltyRules.NominalStrength(list, Faction.同盟));  // 名目は 200+500+300
        }

        // ───────── 寝返りカスケードで「戦う前に決まる」─────────

        [Test]
        public void Sekigahara_NominalMajorityWest_LosesToDefections()
        {
            // 東軍(帝国)：家康本隊3万＝忠誠の核
            // 西軍(同盟)：三成・大谷1万(命がけ)＋小早川ら4万(大調略)＋日和見2万
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 30000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(2, Faction.同盟, 10000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(3, Faction.同盟, 40000, loyalty: 0.2f, intrigue: 0.9f), // 小早川型＝大調略
                new Allegiance(4, Faction.同盟, 20000, loyalty: 0.3f, intrigue: 0.2f), // 日和見
            };
            // 名目では西軍が圧倒（7万 vs 3万）
            Assert.Greater(LoyaltyRules.NominalStrength(list, Faction.同盟),
                           LoyaltyRules.NominalStrength(list, Faction.帝国));

            Faction winner = LoyaltyRules.ResolveWinner(list, Faction.帝国, Faction.同盟, P, out int effE, out int effW);

            Assert.AreEqual(Faction.帝国, winner);           // 実効では東軍が勝つ
            Assert.AreEqual(Stance.寝返り, list[2].stance);  // 小早川は寝返り
            Assert.AreEqual(Stance.静観, list[3].stance);    // 日和見は静観
            Assert.AreEqual(70000, effE);                    // 3万＋寝返り4万
            Assert.AreEqual(10000, effW);                    // 命がけの1万だけ
        }

        // ───────── 調略（戦前プログラミング）が勝敗を覆す ─────────

        [Test]
        public void Intrigue_Decides_BeforeBattle()
        {
            // 東軍(帝国)2万。西軍(同盟)＝核1万＋大兵力4万(忠誠0.7・未調略)。
            List<Allegiance> Build() => new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 20000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(2, Faction.同盟, 10000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(3, Faction.同盟, 40000, loyalty: 0.7f, intrigue: 0.0f),
            };

            // 調略なし：大兵力(0.7)は戦う → 西軍5万 vs 東軍2万 → 西軍勝利
            var noIntrigue = Build();
            Faction w1 = LoyaltyRules.ResolveWinner(noIntrigue, Faction.帝国, Faction.同盟, P, out _, out _);
            Assert.AreEqual(Faction.同盟, w1);

            // 戦前に調略を仕込む（家康の手紙 #819）：大兵力の intrigue を +0.5
            var bribed = Build();
            LoyaltyRules.ApplyIntrigue(bribed[2], 0.5f); // 0.0→0.5、純忠誠 0.7-0.5=0.2 で戦わなくなる
            Faction w2 = LoyaltyRules.ResolveWinner(bribed, Faction.帝国, Faction.同盟, P, out int e, out int we);

            Assert.AreEqual(Faction.帝国, w2);              // 調略で勝敗が覆る＝戦う前に決まる
            Assert.AreEqual(Stance.寝返り, bribed[2].stance); // 劣勢を見て寝返る
            Assert.AreEqual(60000, e);                      // 2万＋寝返り4万
            Assert.AreEqual(10000, we);
        }

        [Test]
        public void ResolveEngagement_CasualtiesConcentrateOnLosingFighters()
        {
            // 関ヶ原：損害は実際に戦った少数（三成・大谷の1万）に集中。寝返り4万・日和見2万は無傷で残る。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 30000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(2, Faction.同盟, 10000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(3, Faction.同盟, 40000, loyalty: 0.2f, intrigue: 0.9f),
                new Allegiance(4, Faction.同盟, 20000, loyalty: 0.3f, intrigue: 0.2f),
            };
            var r = LoyaltyRules.ResolveEngagement(list, Faction.帝国, Faction.同盟, P);

            Assert.AreEqual(Faction.帝国, r.winner);
            Assert.AreEqual(70000, r.winnerEffective);   // 3万＋寝返り4万
            Assert.AreEqual(10000, r.loserEffective);    // 命がけの1万のみ
            Assert.AreEqual(60000, r.winnerSurvivors);   // 勝者の消耗（70000-10000）
            Assert.AreEqual(10000, r.loserCasualties);   // 損害は戦った1万に集中（残り6万は無傷）
        }

        [Test]
        public void Cascade_ReachesStableEquilibrium()
        {
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 5000, loyalty: 1.0f),
                new Allegiance(2, Faction.同盟, 5000, loyalty: 1.0f),
                new Allegiance(3, Faction.同盟, 8000, loyalty: 0.1f, intrigue: 0.9f),
            };
            LoyaltyRules.ResolveCascade(list, Faction.帝国, Faction.同盟, P);
            // 再度解決しても結果が変わらない（不動点）
            Stance s1 = list[1].stance, s2 = list[2].stance;
            LoyaltyRules.ResolveCascade(list, Faction.帝国, Faction.同盟, P);
            Assert.AreEqual(s1, list[1].stance);
            Assert.AreEqual(s2, list[2].stance);
        }
    }
}
