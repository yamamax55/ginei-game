using NUnit.Framework;
using Ginei;
using Prm = Ginei.PoliticianArchetypeRules.PoliticianArchetypeParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 政治家類型（<see cref="PoliticianArchetypeRules"/>）を固定する：素養（人気/弁舌/党内基盤/清廉さ）から
    /// 決定論で類型（ポピュリスト/技術官僚/改革者/守旧派/折衷）を分類し、類型から票訴求の補正と連立協調性を導く。
    /// 素養そのものの算出は <see cref="PoliticianRules"/> へ委譲する（二重実装しない）。
    /// </summary>
    public class PoliticianArchetypeRulesTests
    {
        // --- 類型を狙って作るヘルパ（appeal 式を満たす素養を組む） ---

        // 大衆高×党内低×清廉低 → ポピュリスト
        static PoliticianProfile MakePopulist() => new PoliticianProfile(1)
        { popularity = 0.95f, oratory = 95, partyStanding = 0.1f, integrity = 20 };

        // 清廉高×大衆低 → 技術官僚
        static PoliticianProfile MakeTechnocrat() => new PoliticianProfile(2)
        { popularity = 0.2f, oratory = 20, partyStanding = 0.7f, integrity = 95 };

        // 清廉高×大衆高×党内低 → 改革者
        static PoliticianProfile MakeReformer() => new PoliticianProfile(3)
        { popularity = 0.95f, oratory = 95, partyStanding = 0.1f, integrity = 95 };

        // 党内高×大衆低×不浄 → 守旧派
        static PoliticianProfile MakeOldGuard() => new PoliticianProfile(4)
        { popularity = 0.15f, oratory = 30, partyStanding = 0.95f, integrity = 25 };

        // どれも中庸 → 折衷
        static PoliticianProfile MakeBalanced() => new PoliticianProfile(5)
        { popularity = 0.5f, oratory = 50, partyStanding = 0.6f, integrity = 55 };

        // --- 分類 ---

        [Test]
        public void Classify_大衆人気で党内基盤が薄ければポピュリスト()
        {
            Assert.AreEqual(PoliticianArchetype.ポピュリスト, PoliticianArchetypeRules.Classify(MakePopulist()));
        }

        [Test]
        public void Classify_清廉で大衆訴求が弱ければ技術官僚()
        {
            Assert.AreEqual(PoliticianArchetype.技術官僚, PoliticianArchetypeRules.Classify(MakeTechnocrat()));
        }

        [Test]
        public void Classify_清廉かつ大衆人気で党内非依存なら改革者()
        {
            Assert.AreEqual(PoliticianArchetype.改革者, PoliticianArchetypeRules.Classify(MakeReformer()));
        }

        [Test]
        public void Classify_厚い党内基盤で不浄なら守旧派()
        {
            Assert.AreEqual(PoliticianArchetype.守旧派, PoliticianArchetypeRules.Classify(MakeOldGuard()));
        }

        [Test]
        public void Classify_突出なしなら折衷()
        {
            Assert.AreEqual(PoliticianArchetype.折衷, PoliticianArchetypeRules.Classify(MakeBalanced()));
        }

        [Test]
        public void Classify_nullは折衷()
        {
            Assert.AreEqual(PoliticianArchetype.折衷, PoliticianArchetypeRules.Classify((PoliticianProfile)null));
        }

        // --- 委譲の正しさ（PoliticianRules と一致） ---

        [Test]
        public void MassAppeal_PoliticianRulesへ委譲()
        {
            var p = MakeReformer();
            Assert.AreEqual(PoliticianRules.MemberVoteAppeal(p), PoliticianArchetypeRules.MassAppeal(p), 1e-6f);
        }

        [Test]
        public void InsiderGrip_PoliticianRulesへ委譲()
        {
            var p = MakeOldGuard();
            Assert.AreEqual(PoliticianRules.LegislatorVoteAppeal(p), PoliticianArchetypeRules.InsiderGrip(p), 1e-6f);
        }

        [Test]
        public void CleanScore_integrityを正規化()
        {
            var p = new PoliticianProfile(9) { integrity = 80 };
            Assert.AreEqual(0.8f, PoliticianArchetypeRules.CleanScore(p), 1e-6f);
            Assert.AreEqual(0f, PoliticianArchetypeRules.CleanScore(null), 1e-6f);
        }

        // --- 訴求補正の方向性・境界 ---

        [Test]
        public void MassVoteModifier_ポピュリストは大衆票に強く守旧派は弱い()
        {
            Assert.Greater(PoliticianArchetypeRules.MassVoteModifier(PoliticianArchetype.ポピュリスト), 1f);
            Assert.Less(PoliticianArchetypeRules.MassVoteModifier(PoliticianArchetype.守旧派), 1f);
            Assert.AreEqual(1f, PoliticianArchetypeRules.MassVoteModifier(PoliticianArchetype.折衷), 1e-6f);
        }

        [Test]
        public void InsiderVoteModifier_守旧派は議員票に強くポピュリストは弱い()
        {
            Assert.Greater(PoliticianArchetypeRules.InsiderVoteModifier(PoliticianArchetype.守旧派), 1f);
            Assert.Less(PoliticianArchetypeRules.InsiderVoteModifier(PoliticianArchetype.ポピュリスト), 1f);
            Assert.AreEqual(1f, PoliticianArchetypeRules.InsiderVoteModifier(PoliticianArchetype.折衷), 1e-6f);
        }

        [Test]
        public void VoteModifier_対称的に振れる()
        {
            var prm = Prm.Default;
            // ポピュリストの大衆+振れ と 守旧派の大衆-振れ は 1.0 を挟んで対称。
            float up = PoliticianArchetypeRules.MassVoteModifier(PoliticianArchetype.ポピュリスト, prm) - 1f;
            float down = 1f - PoliticianArchetypeRules.MassVoteModifier(PoliticianArchetype.守旧派, prm);
            Assert.AreEqual(up, down, 1e-6f);
        }

        // --- 連立協調性：方向・境界・null ---

        [Test]
        public void CoalitionCooperativeness_技術官僚が最協調でポピュリストが最非協調()
        {
            float tech = PoliticianArchetypeRules.CoalitionCooperativeness(PoliticianArchetype.技術官僚);
            float pop = PoliticianArchetypeRules.CoalitionCooperativeness(PoliticianArchetype.ポピュリスト);
            float bal = PoliticianArchetypeRules.CoalitionCooperativeness(PoliticianArchetype.折衷);
            Assert.Greater(tech, bal);
            Assert.Less(pop, bal);
        }

        [Test]
        public void CoalitionCooperativeness_0to1におさまる()
        {
            foreach (PoliticianArchetype a in System.Enum.GetValues(typeof(PoliticianArchetype)))
            {
                float c = PoliticianArchetypeRules.CoalitionCooperativeness(a);
                Assert.GreaterOrEqual(c, 0f);
                Assert.LessOrEqual(c, 1f);
            }
        }

        [Test]
        public void CoalitionCooperativeness_政治家版はnull安全で折衷扱い()
        {
            float viaNull = PoliticianArchetypeRules.CoalitionCooperativeness((PoliticianProfile)null, Prm.Default);
            float viaBalanced = PoliticianArchetypeRules.CoalitionCooperativeness(PoliticianArchetype.折衷, Prm.Default);
            Assert.AreEqual(viaBalanced, viaNull, 1e-6f);
        }

        // --- 類型反映の実効選挙力 ---

        [Test]
        public void ArchetypeElectoralStrength_ポピュリストは無補正より大衆寄りで高い()
        {
            var p = MakePopulist();
            // ポピュリストは大衆票へ +振れ・議員票へ -振れ。素養上は大衆票が圧倒的なので合成は無補正以上。
            float plain = PoliticianRules.ElectoralStrength(p);
            float arch = PoliticianArchetypeRules.ArchetypeElectoralStrength(p);
            Assert.Greater(arch, plain);
        }

        [Test]
        public void ArchetypeElectoralStrength_0to1かつnull安全()
        {
            Assert.AreEqual(0f, PoliticianArchetypeRules.ArchetypeElectoralStrength(null), 1e-6f);
            float v = PoliticianArchetypeRules.ArchetypeElectoralStrength(MakeReformer());
            Assert.GreaterOrEqual(v, 0f);
            Assert.LessOrEqual(v, 1f);
        }

        // --- 決定論 ---

        [Test]
        public void Classify_決定論_同入力は同結果()
        {
            var p = MakeOldGuard();
            var a1 = PoliticianArchetypeRules.Classify(p);
            var a2 = PoliticianArchetypeRules.Classify(p);
            Assert.AreEqual(a1, a2);
        }

        [Test]
        public void ArchetypeElectoralStrength_決定論()
        {
            var p = MakePopulist();
            Assert.AreEqual(
                PoliticianArchetypeRules.ArchetypeElectoralStrength(p),
                PoliticianArchetypeRules.ArchetypeElectoralStrength(p), 1e-6f);
        }

        // --- Params の堅牢性 ---

        [Test]
        public void Params_高低しきい値の逆転入力でも低≤高を保証()
        {
            var prm = new Prm(0.3f, 0.7f, 0.3f, 0.5f, 0.3f); // 高<低 で渡す
            Assert.LessOrEqual(prm.lowThreshold, prm.highThreshold);
        }

        [Test]
        public void Params_既定値()
        {
            var prm = Prm.Default;
            Assert.AreEqual(PoliticianArchetypeRules.HighThreshold, prm.highThreshold, 1e-6f);
            Assert.AreEqual(PoliticianArchetypeRules.LowThreshold, prm.lowThreshold, 1e-6f);
            Assert.AreEqual(0.3f, prm.appealSwing, 1e-6f);
        }

        [Test]
        public void Params_補正値はクランプされる()
        {
            var prm = new Prm(0.6f, 0.4f, 5f, 5f, 5f); // 過大入力
            Assert.LessOrEqual(prm.appealSwing, 1f);
            Assert.LessOrEqual(prm.cooperBase, 1f);
            Assert.LessOrEqual(prm.cooperSwing, 1f);
        }
    }
}
