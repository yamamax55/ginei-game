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

        // ═════════════════════════════════════════════════════════
        // 以下、敵対的エッジケース（境界・クランプ・null/空・不変条件）
        // 期待値は実装でなく「仕様として正しい値」を手計算して固定する。
        // ═════════════════════════════════════════════════════════

        // ───────── Momentum：ゼロ除算ガード・対称性・片側 ─────────

        [Test]
        public void Momentum_ZeroDivisionGuard_ReturnsZero()
        {
            // own+enemy==0 のときゼロ除算を避けて 0 を返す（仕様：趨勢中立）
            Assert.AreEqual(0f, LoyaltyRules.Momentum(0f, 0f), 1e-5f);
            // total <= 0（両方0・負側）でも 0（実効兵力は本来非負だがガードの両端を突く）
            Assert.AreEqual(0f, LoyaltyRules.Momentum(-100f, 100f), 1e-5f); // total=0
            Assert.AreEqual(0f, LoyaltyRules.Momentum(-50f, -50f), 1e-5f);  // total=-100<=0 → ガード
        }

        [Test]
        public void Momentum_OneSided_IsPlusMinusOne()
        {
            // 敵が0で自軍だけなら +1（完全優勢）、自軍が0なら -1（完全劣勢）
            Assert.AreEqual(1f, LoyaltyRules.Momentum(1000f, 0f), 1e-5f);
            Assert.AreEqual(-1f, LoyaltyRules.Momentum(0f, 1000f), 1e-5f);
        }

        [Test]
        public void Momentum_IsAntisymmetric()
        {
            // 不変条件：Momentum(a,b) == -Momentum(b,a)
            Assert.AreEqual(LoyaltyRules.Momentum(300f, 700f),
                            -LoyaltyRules.Momentum(700f, 300f), 1e-5f);
            // 具体値：(300-700)/1000 = -0.4
            Assert.AreEqual(-0.4f, LoyaltyRules.Momentum(300f, 700f), 1e-5f);
        }

        // ───────── ResolveStance：null・閾値の境界（オフバイワン）─────────

        [Test]
        public void ResolveStance_Null_IsWatch()
        {
            // null 入力は静観（クラッシュしない）
            Assert.AreEqual(Stance.静観, LoyaltyRules.ResolveStance(null, -0.9f, P));
        }

        [Test]
        public void ResolveStance_NetExactlyAtFightThreshold_Fights()
        {
            // 純忠誠 net = loyalty-intrigue = 0.6-0.1 = 0.5 == fightThreshold(0.5)。
            // 条件は net >= threshold ＝ 境界は「戦う」側に含む。
            var a = new Allegiance(1, Faction.同盟, 100, loyalty: 0.6f, intrigue: 0.1f);
            Assert.AreEqual(Stance.戦う, LoyaltyRules.ResolveStance(a, -0.9f, P));
        }

        [Test]
        public void ResolveStance_NetJustBelowFightThreshold_DoesNotFight()
        {
            // net = 0.59-0.1 = 0.49 < 0.5 → 戦わない。劣勢かつ intrigue<defect(0.5) なので静観。
            var a = new Allegiance(1, Faction.同盟, 100, loyalty: 0.59f, intrigue: 0.1f);
            Assert.AreEqual(Stance.静観, LoyaltyRules.ResolveStance(a, -0.9f, P));
        }

        [Test]
        public void ResolveStance_IntrigueAtThreshold_ButMomentumExactlyZero_Watches()
        {
            // intrigue=0.5==defect、ただし momentum==0（< 0 でない＝劣勢でない）→ 寝返り条件を満たさず静観。
            // net = 0.0-0.5 = -0.5 < fight なので戦うでもない。仕様：拮抗では寝返らない。
            var a = new Allegiance(1, Faction.同盟, 100, loyalty: 0.0f, intrigue: 0.5f);
            Assert.AreEqual(Stance.静観, LoyaltyRules.ResolveStance(a, 0f, P));
        }

        [Test]
        public void ResolveStance_IntrigueAtThreshold_AndLosing_Defects()
        {
            // intrigue=0.5==defect かつ momentum<0 → 寝返り（境界は寝返り側に含む）。
            var a = new Allegiance(1, Faction.同盟, 100, loyalty: 0.0f, intrigue: 0.5f);
            Assert.AreEqual(Stance.寝返り, LoyaltyRules.ResolveStance(a, -0.0001f, P));
        }

        [Test]
        public void ResolveStance_FightTakesPriorityOverDefect()
        {
            // 高忠誠かつ高調略：net=0.9-0.6=0.3<0.5 で戦わない…ではなく
            // net=1.0-0.5=0.5 で「戦う」が寝返りより優先されることを確認（分岐順序）。
            var a = new Allegiance(1, Faction.同盟, 100, loyalty: 1.0f, intrigue: 0.5f);
            // net=0.5>=fight → 戦う（intrigue>=defect かつ劣勢でも戦うが勝つ）
            Assert.AreEqual(Stance.戦う, LoyaltyRules.ResolveStance(a, -0.9f, P));
        }

        // ───────── EffectiveStrength / NominalStrength：null・空・null要素 ─────────

        [Test]
        public void EffectiveAndNominal_NullList_AreZero()
        {
            Assert.AreEqual(0, LoyaltyRules.EffectiveStrength(null, Faction.帝国));
            Assert.AreEqual(0, LoyaltyRules.NominalStrength(null, Faction.帝国));
        }

        [Test]
        public void EffectiveAndNominal_EmptyList_AreZero()
        {
            var list = new List<Allegiance>();
            Assert.AreEqual(0, LoyaltyRules.EffectiveStrength(list, Faction.帝国));
            Assert.AreEqual(0, LoyaltyRules.NominalStrength(list, Faction.帝国));
        }

        [Test]
        public void EffectiveStrength_SkipsNullElements()
        {
            var list = new List<Allegiance>
            {
                null,
                new Allegiance(1, Faction.帝国, 100) { stance = Stance.戦う },
                null,
            };
            Assert.AreEqual(100, LoyaltyRules.EffectiveStrength(list, Faction.帝国));
            Assert.AreEqual(100, LoyaltyRules.NominalStrength(list, Faction.帝国));
        }

        [Test]
        public void Fighting_ForWrongSide_CountsForNeither()
        {
            // stance=戦う だが side==帝国 の艦を「同盟」で数えてはいけない（FightsFor の AND 条件）。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 500) { stance = Stance.戦う },
            };
            Assert.AreEqual(500, LoyaltyRules.EffectiveStrength(list, Faction.帝国));
            Assert.AreEqual(0, LoyaltyRules.EffectiveStrength(list, Faction.同盟));
        }

        [Test]
        public void Defector_CountsOnlyForOppositeSide()
        {
            // 寝返り：side=同盟 の兵は「帝国」に数え、「同盟」には数えない（排他）。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.同盟, 700) { stance = Stance.寝返り },
            };
            Assert.AreEqual(700, LoyaltyRules.EffectiveStrength(list, Faction.帝国));
            Assert.AreEqual(0, LoyaltyRules.EffectiveStrength(list, Faction.同盟));
            // 名目は元の所属（同盟）にだけ計上される（寝返っても名目所属は不変）。
            Assert.AreEqual(700, LoyaltyRules.NominalStrength(list, Faction.同盟));
            Assert.AreEqual(0, LoyaltyRules.NominalStrength(list, Faction.帝国));
        }

        // ───────── ResolveCascade：null・locked の保護 ─────────

        [Test]
        public void ResolveCascade_NullList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoyaltyRules.ResolveCascade(null, Faction.帝国, Faction.同盟, P));
        }

        [Test]
        public void ResolveCascade_LockedAllegiance_IsNeverReassigned()
        {
            // locked=true の諸侯は、本来なら寝返るはずの条件でも stance を変えられない（既に動いた＝確定）。
            var locked = new Allegiance(3, Faction.同盟, 40000, loyalty: 0.1f, intrigue: 0.9f);
            locked.locked = true;
            locked.stance = Stance.戦う; // 確定済みとして戦う固定
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 30000, loyalty: 1.0f),
                new Allegiance(2, Faction.同盟, 10000, loyalty: 1.0f),
                locked,
            };
            LoyaltyRules.ResolveCascade(list, Faction.帝国, Faction.同盟, P);
            Assert.AreEqual(Stance.戦う, locked.stance); // locked は不変
        }

        // ───────── ResolveWinner：同数は sideA・合計保存則 ─────────

        [Test]
        public void ResolveWinner_Tie_FavorsSideA()
        {
            // 双方が忠誠で戦い、実効兵力が完全同数（500 vs 500）→ 同数は sideA(帝国)。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 500, loyalty: 1.0f),
                new Allegiance(2, Faction.同盟, 500, loyalty: 1.0f),
            };
            Faction w = LoyaltyRules.ResolveWinner(list, Faction.帝国, Faction.同盟, P, out int eA, out int eB);
            Assert.AreEqual(Faction.帝国, w);
            Assert.AreEqual(500, eA);
            Assert.AreEqual(500, eB);
        }

        [Test]
        public void EffectiveStrength_NeverExceedsTotalNominal_Conservation()
        {
            // 合計保存則：戦う/寝返りのみ数えるので effA+effB <= 全名目兵力（静観ぶんは漏れる）。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 30000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(2, Faction.同盟, 10000, loyalty: 1.0f, intrigue: 0.0f),
                new Allegiance(3, Faction.同盟, 40000, loyalty: 0.2f, intrigue: 0.9f),
                new Allegiance(4, Faction.同盟, 20000, loyalty: 0.3f, intrigue: 0.2f),
            };
            LoyaltyRules.ResolveWinner(list, Faction.帝国, Faction.同盟, P, out int eA, out int eB);
            int totalNominal = 30000 + 10000 + 40000 + 20000;
            Assert.LessOrEqual(eA + eB, totalNominal);
            // 日和見2万は静観で抜けるので合計は名目より厳密に小さい
            Assert.Less(eA + eB, totalNominal);
        }

        // ───────── ResolveEngagement：survivors クランプ・同数・損害局所化 ─────────

        [Test]
        public void ResolveEngagement_Tie_SurvivorsClampToZero()
        {
            // 実効同数 → 勝者 sideA、winnerSurvivors=max(0, winEff-loseEff)=0（負にならない）。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 500, loyalty: 1.0f),
                new Allegiance(2, Faction.同盟, 500, loyalty: 1.0f),
            };
            var r = LoyaltyRules.ResolveEngagement(list, Faction.帝国, Faction.同盟, P);
            Assert.AreEqual(Faction.帝国, r.winner);
            Assert.AreEqual(500, r.winnerEffective);
            Assert.AreEqual(500, r.loserEffective);
            Assert.AreEqual(0, r.winnerSurvivors);   // クランプ下端
            Assert.AreEqual(500, r.loserCasualties); // 敗者は全滅（実際に戦った分）
        }

        [Test]
        public void ResolveEngagement_AllWatch_NoFighters_ZeroEverything()
        {
            // 全員が静観（戦わず）→ 双方実効0、勝者は同数で sideA、損害も0。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, 1000, loyalty: 0.4f, intrigue: 0.1f),
                new Allegiance(2, Faction.同盟, 1000, loyalty: 0.4f, intrigue: 0.1f),
            };
            var r = LoyaltyRules.ResolveEngagement(list, Faction.帝国, Faction.同盟, P);
            Assert.AreEqual(Faction.帝国, r.winner); // 0==0 → sideA
            Assert.AreEqual(0, r.winnerEffective);
            Assert.AreEqual(0, r.loserEffective);
            Assert.AreEqual(0, r.winnerSurvivors);
            Assert.AreEqual(0, r.loserCasualties);
        }

        // ───────── ApplyIntrigue：クランプ両端・null ─────────

        [Test]
        public void ApplyIntrigue_ClampsAtUpperBound()
        {
            var a = new Allegiance(1, Faction.同盟, 100, loyalty: 0.5f, intrigue: 0.8f);
            LoyaltyRules.ApplyIntrigue(a, 0.9f); // 0.8+0.9=1.7 → 1.0 にクランプ
            Assert.AreEqual(1.0f, a.intrigue, 1e-5f);
        }

        [Test]
        public void ApplyIntrigue_NegativeAmount_ClampsAtZero()
        {
            // 仕様：intrigue は 0..1。負の amount でマイナスにはならない（調略の打ち消しは下端0で止まる）。
            var a = new Allegiance(1, Faction.同盟, 100, loyalty: 0.5f, intrigue: 0.2f);
            LoyaltyRules.ApplyIntrigue(a, -0.9f); // 0.2-0.9=-0.7 → 0.0
            Assert.AreEqual(0.0f, a.intrigue, 1e-5f);
        }

        [Test]
        public void ApplyIntrigue_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LoyaltyRules.ApplyIntrigue(null, 0.5f));
        }

        // ───────── 構築時クランプの結合（Allegiance ctor との整合）─────────

        [Test]
        public void Constructor_NegativeStrength_ClampedToZero_NotCountedNegatively()
        {
            // ctor は strength を Max(0,..) でクランプ。負兵力が実効/名目を負に引かないこと。
            var list = new List<Allegiance>
            {
                new Allegiance(1, Faction.帝国, -5000) { stance = Stance.戦う },
                new Allegiance(2, Faction.帝国, 100) { stance = Stance.戦う },
            };
            Assert.AreEqual(100, LoyaltyRules.EffectiveStrength(list, Faction.帝国)); // 0+100
            Assert.AreEqual(100, LoyaltyRules.NominalStrength(list, Faction.帝国));
        }
    }
}
