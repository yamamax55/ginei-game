using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// AdmiralDevelopmentRules（提督の育成弧＝戦い方→伸びる能力面・#提督育成）の EditMode テスト。
    /// 経験配分の単調性・保存・委譲（GrowthRules/BattleMetaRules）・null安全・決定論を担保する。
    /// 基準フィールド非破壊（実効値パターン）も検証する。
    /// </summary>
    public class AdmiralDevelopmentRulesTests
    {
        private static AdmiralData MakeAdmiral(int atk = 60, int def = 60, int mob = 60, int lead = 60)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.staffOfficers = new AdmiralData[0];
            a.attack = atk; a.defense = def; a.mobility = mob; a.leadership = lead;
            return a;
        }

        // PrimaryFacet：各戦い方が想定どおりの主能力へ写る。
        [Test]
        public void PrimaryFacet_MapsKindToFacet()
        {
            Assert.AreEqual(DevelopmentFacet.攻撃, AdmiralDevelopmentRules.PrimaryFacet(CombatExperienceKind.攻勢));
            Assert.AreEqual(DevelopmentFacet.防御, AdmiralDevelopmentRules.PrimaryFacet(CombatExperienceKind.防勢));
            Assert.AreEqual(DevelopmentFacet.機動, AdmiralDevelopmentRules.PrimaryFacet(CombatExperienceKind.機動));
            Assert.AreEqual(DevelopmentFacet.統率, AdmiralDevelopmentRules.PrimaryFacet(CombatExperienceKind.統御));
        }

        // DistributeExperience：主能力が他面より多く配分される（偏り）。
        [Test]
        public void DistributeExperience_PrimaryGetsMost()
        {
            float[] d = AdmiralDevelopmentRules.DistributeExperience(100f, CombatExperienceKind.攻勢);
            int atk = (int)DevelopmentFacet.攻撃;
            for (int i = 0; i < 4; i++)
                if (i != atk) Assert.Greater(d[atk], d[i]);
        }

        // DistributeExperience：総和は入力経験を保存する（漏らさない）。
        [Test]
        public void DistributeExperience_ConservesTotal()
        {
            float total = 137.5f;
            float[] d = AdmiralDevelopmentRules.DistributeExperience(total, CombatExperienceKind.統御);
            float sum = d[0] + d[1] + d[2] + d[3];
            Assert.AreEqual(total, sum, 1e-3f);
        }

        // DistributeExperience：非主能力3面は均等に滲む。
        [Test]
        public void DistributeExperience_SpillIsEven()
        {
            float[] d = AdmiralDevelopmentRules.DistributeExperience(90f, CombatExperienceKind.防勢);
            int def = (int)DevelopmentFacet.防御;
            float? spill = null;
            for (int i = 0; i < 4; i++)
            {
                if (i == def) continue;
                if (spill == null) spill = d[i];
                else Assert.AreEqual(spill.Value, d[i], 1e-4f);
            }
        }

        // DistributeExperience：負の総経験は0扱い（全面0）。
        [Test]
        public void DistributeExperience_NegativeTotalIsZero()
        {
            float[] d = AdmiralDevelopmentRules.DistributeExperience(-50f, CombatExperienceKind.機動);
            for (int i = 0; i < 4; i++) Assert.AreEqual(0f, d[i], 1e-5f);
        }

        // primaryShare=1.0 なら主能力に全振り（他面0）。
        [Test]
        public void DistributeExperience_FullShareGoesOnlyToPrimary()
        {
            var p = new AdmiralDevelopmentRules.DevelopmentParams(1f);
            float[] d = AdmiralDevelopmentRules.DistributeExperience(80f, CombatExperienceKind.攻勢, p);
            int atk = (int)DevelopmentFacet.攻撃;
            Assert.AreEqual(80f, d[atk], 1e-3f);
            for (int i = 0; i < 4; i++)
                if (i != atk) Assert.AreEqual(0f, d[i], 1e-4f);
        }

        // DevelopmentParams：share は [0.25,1] にクランプされる（均等以上・主のみ以下）。
        [Test]
        public void DevelopmentParams_ClampsShare()
        {
            Assert.AreEqual(0.25f, new AdmiralDevelopmentRules.DevelopmentParams(0f).primaryShare, 1e-5f);
            Assert.AreEqual(1f, new AdmiralDevelopmentRules.DevelopmentParams(5f).primaryShare, 1e-5f);
            Assert.AreEqual(0.7f, AdmiralDevelopmentRules.DevelopmentParams.Default.primaryShare, 1e-5f);
        }

        // share=0.25（均等）なら全面に同量。
        [Test]
        public void DistributeExperience_EvenShareSplitsEqually()
        {
            var p = new AdmiralDevelopmentRules.DevelopmentParams(0.25f);
            float[] d = AdmiralDevelopmentRules.DistributeExperience(100f, CombatExperienceKind.統御);
            for (int i = 0; i < 4; i++) Assert.AreEqual(25f, d[i], 1e-3f);
        }

        // ApplyBattleExperience：主能力面の経験が他面より多く積まれる（戦果→偏り蓄積）。
        [Test]
        public void ApplyBattleExperience_GrowsPrimaryFacetMost()
        {
            var dev = new AdmiralDevelopment(GrowthArchetype.叩き上げ);
            AdmiralDevelopmentRules.ApplyBattleExperience(dev, damageDealt: 50000, kills: 10, isWinner: true, CombatExperienceKind.攻勢);
            Assert.Greater(dev.attack.experience, dev.defense.experience);
            Assert.Greater(dev.attack.experience, dev.mobility.experience);
            Assert.Greater(dev.attack.experience, dev.leadership.experience);
            Assert.Greater(dev.attack.experience, 0f);
        }

        // ApplyBattleExperience：BattleMetaRules.ExperienceFromBattle に委譲（敗者は獲得が少ない）。
        [Test]
        public void ApplyBattleExperience_DelegatesToBattleMeta_LoserGainsLess()
        {
            var win = new AdmiralDevelopment(GrowthArchetype.叩き上げ);
            var lose = new AdmiralDevelopment(GrowthArchetype.叩き上げ);
            AdmiralDevelopmentRules.ApplyBattleExperience(win, 30000, 5, true, CombatExperienceKind.統御);
            AdmiralDevelopmentRules.ApplyBattleExperience(lose, 30000, 5, false, CombatExperienceKind.統御);
            Assert.Greater(win.leadership.experience, lose.leadership.experience);
        }

        // ApplyBattleExperience：null dev でも例外を投げない（null安全）。
        [Test]
        public void ApplyBattleExperience_NullDevIsSafe()
        {
            Assert.DoesNotThrow(() =>
                AdmiralDevelopmentRules.ApplyBattleExperience(null, 1000, 1, true, CombatExperienceKind.防勢));
        }

        // FacetBonus：経験を積むほどボーナスが単調非減少（飽和カーブの単調性＝委譲先 GrowthRules）。
        [Test]
        public void FacetBonus_MonotonicInExperience()
        {
            var low = new AdmiralDevelopment(GrowthArchetype.老練型);
            var high = new AdmiralDevelopment(GrowthArchetype.老練型);
            GrowthRules.GainExperience(low.attack, 10f, 1f);
            GrowthRules.GainExperience(high.attack, 1000f, 1f);
            int bLow = AdmiralDevelopmentRules.FacetBonus(low, DevelopmentFacet.攻撃, 50);
            int bHigh = AdmiralDevelopmentRules.FacetBonus(high, DevelopmentFacet.攻撃, 50);
            Assert.GreaterOrEqual(bHigh, bLow);
            Assert.GreaterOrEqual(bLow, 0);
        }

        // FacetBonus：null dev は 0。
        [Test]
        public void FacetBonus_NullDevIsZero()
        {
            Assert.AreEqual(0, AdmiralDevelopmentRules.FacetBonus(null, DevelopmentFacet.機動, 60));
        }

        // FacetBonus：base+bonus は MaxStatValue を超えない（委譲先の上限抑制）。
        [Test]
        public void FacetBonus_RespectsStatCeiling()
        {
            var dev = new AdmiralDevelopment(GrowthArchetype.在野俊英型);
            GrowthRules.GainExperience(dev.defense, 100000f, 1f);
            int baseStat = 95;
            int bonus = AdmiralDevelopmentRules.FacetBonus(dev, DevelopmentFacet.防御, baseStat);
            Assert.LessOrEqual(baseStat + bonus, AdmiralData.MaxStatValue);
        }

        // BaseStatOf：各面が AdmiralData の対応フィールドを引く。
        [Test]
        public void BaseStatOf_ReadsCorrectField()
        {
            var a = MakeAdmiral(atk: 11, def: 22, mob: 33, lead: 44);
            Assert.AreEqual(11, AdmiralDevelopmentRules.BaseStatOf(a, DevelopmentFacet.攻撃));
            Assert.AreEqual(22, AdmiralDevelopmentRules.BaseStatOf(a, DevelopmentFacet.防御));
            Assert.AreEqual(33, AdmiralDevelopmentRules.BaseStatOf(a, DevelopmentFacet.機動));
            Assert.AreEqual(44, AdmiralDevelopmentRules.BaseStatOf(a, DevelopmentFacet.統率));
        }

        // BaseStatOf：null admiral は 0。
        [Test]
        public void BaseStatOf_NullAdmiralIsZero()
        {
            Assert.AreEqual(0, AdmiralDevelopmentRules.BaseStatOf(null, DevelopmentFacet.攻撃));
        }

        // GrownFacetStat：dev=null なら基準そのまま、経験ありなら基準以上（非破壊・上乗せ）。
        [Test]
        public void GrownFacetStat_BaseUnchangedWhenNoDev_RisesWithExp()
        {
            var a = MakeAdmiral(atk: 50);
            Assert.AreEqual(50, AdmiralDevelopmentRules.GrownFacetStat(a, null, DevelopmentFacet.攻撃));

            var dev = new AdmiralDevelopment(GrowthArchetype.首席型);
            GrowthRules.GainExperience(dev.attack, 500f, 1f);
            int grown = AdmiralDevelopmentRules.GrownFacetStat(a, dev, DevelopmentFacet.攻撃);
            Assert.GreaterOrEqual(grown, 50);
            // 基準フィールドは書き換えられていない（実効値パターン）。
            Assert.AreEqual(50, a.attack);
        }

        // GrownFacetStat：null admiral は 0。
        [Test]
        public void GrownFacetStat_NullAdmiralIsZero()
        {
            Assert.AreEqual(0, AdmiralDevelopmentRules.GrownFacetStat(null, null, DevelopmentFacet.統率));
        }

        // 決定論：同じ入力で繰り返しても結果が一致する。
        [Test]
        public void ApplyBattleExperience_IsDeterministic()
        {
            var d1 = new AdmiralDevelopment(GrowthArchetype.老練型);
            var d2 = new AdmiralDevelopment(GrowthArchetype.老練型);
            AdmiralDevelopmentRules.ApplyBattleExperience(d1, 12345, 7, true, CombatExperienceKind.機動);
            AdmiralDevelopmentRules.ApplyBattleExperience(d2, 12345, 7, true, CombatExperienceKind.機動);
            Assert.AreEqual(d1.mobility.experience, d2.mobility.experience, 1e-5f);
            Assert.AreEqual(d1.attack.experience, d2.attack.experience, 1e-5f);
        }

        // GetFacet：null フィールドでも新規生成して返す（null安全）。
        [Test]
        public void GetFacet_LazyInitializesNullFacet()
        {
            var dev = new AdmiralDevelopment();
            dev.mobility = null;
            Growth g = dev.GetFacet(DevelopmentFacet.機動);
            Assert.IsNotNull(g);
            Assert.IsNotNull(dev.mobility);
        }
    }
}
