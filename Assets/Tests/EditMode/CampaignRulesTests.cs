using UnityEngine;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦役の世界状態（社会シミュ ↔ 地理盤面の統合）を固定する：勢力ごとの国家状態の用意・時間進行、
    /// 版図の一体化度が実効安定度を割り引くこと、暫定優勢勢力。
    /// </summary>
    public class CampaignRulesTests
    {
        private static StarSystem Sys(int id, Faction owner) => new StarSystem(id, "S" + id, Vector2.zero, owner);

        [Test]
        public void EnsureStates_CreatesPerOwningFaction()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.同盟));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);

            Assert.AreEqual(2, c.states.Count);
            Assert.IsNotNull(CampaignRules.GetState(c, Faction.帝国));
            Assert.IsNotNull(CampaignRules.GetState(c, Faction.同盟));

            CampaignRules.EnsureStates(c); // 冪等：再呼び出しで増えない
            Assert.AreEqual(2, c.states.Count);
        }

        [Test]
        public void Fragmentation_DiscountsEffectiveStability()
        {
            // 帝国は {0,1} と {2,3} の2塊に分断（回廊 0-1 と 2-3 のみ）＝一体化度 0.5
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddSystem(Sys(2, Faction.帝国));
            m.AddSystem(Sys(3, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 1f));
            m.AddCorridor(new Corridor(2, 3, 1f));

            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c); // 帝国の国家状態（既定＝安定度1.0）

            // 安定度1.0 × 一体化度0.5 = 0.5
            Assert.AreEqual(0.5f, CampaignRules.EffectiveStability(c, Faction.帝国), 1e-4f);
        }

        [Test]
        public void Tick_AdvancesAllStates()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);
            var s = CampaignRules.GetState(c, Faction.帝国);
            s.regime.virtue = 0f;

            CampaignRules.Tick(c, 1f);
            Assert.Greater(s.regime.corruption, 0f); // 腐敗が進んだ
        }

        [Test]
        public void LeadingFaction_HighestEffectiveStability()
        {
            // 帝国＝連結（一体化1.0）／同盟＝分断（一体化0.5）。両方とも国家安定度は既定1.0。
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddSystem(Sys(2, Faction.同盟));
            m.AddSystem(Sys(3, Faction.同盟));
            m.AddSystem(Sys(4, Faction.同盟));
            m.AddCorridor(new Corridor(0, 1, 1f)); // 帝国連結
            m.AddCorridor(new Corridor(2, 3, 1f)); // 同盟 {2,3} と {4} に分断（一体化 2/3）

            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);

            // 帝国 1.0×1.0=1.0 ＞ 同盟 1.0×(2/3)≈0.667
            Assert.AreEqual(Faction.帝国, CampaignRules.LeadingFaction(c));
        }

        // ===== 以下、敵対的エッジケース（境界・クランプ・分岐・異常入力）=====

        /// <summary>null 入力でクラッシュしない（EnsureStates/Tick）。EffectiveStability(null)=0、LeadingFaction(null)=帝国（既定）。</summary>
        [Test]
        public void NullInputs_AreSafe()
        {
            Assert.DoesNotThrow(() => CampaignRules.EnsureStates(null));
            Assert.DoesNotThrow(() => CampaignRules.Tick(null, 1f));
            Assert.IsNull(CampaignRules.GetState(null, Faction.帝国));
            // GetState が null → EffectiveStability は 0
            Assert.AreEqual(0f, CampaignRules.EffectiveStability(null, Faction.帝国), 1e-5f);
            // states が無い → LeadingFaction は既定の帝国
            Assert.AreEqual(Faction.帝国, CampaignRules.LeadingFaction(null));
        }

        /// <summary>map が null の CampaignState：EnsureStates は早期 return（状態を作らない）。EffectiveStability は cohesion=1 にフォールバック。</summary>
        [Test]
        public void NullMap_EnsureStatesNoop_EffectiveStabilityUsesUnitCohesion()
        {
            var c = new CampaignState(); // map == null
            CampaignRules.EnsureStates(c);
            // map が null なので所有勢力を列挙できず、状態は作られない（仕様：map null は早期 return）
            Assert.AreEqual(0, c.states.Count);

            // 状態を手動で足す（既定＝安定度1.0）
            c.states.Add(new FactionState(Faction.帝国));
            // 実装：map==null のとき cohesion=1f にフォールバック → 1.0×1.0=1.0
            Assert.AreEqual(1f, CampaignRules.EffectiveStability(c, Faction.帝国), 1e-5f);
        }

        /// <summary>所有星系が0の勢力（状態だけ存在）：CohesionFactor は空コレクション→0 ⇒ EffectiveStability=0。</summary>
        [Test]
        public void FactionWithNoOwnedSystems_EffectiveStabilityIsZero()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国)); // 同盟は1星系も持たない
            var c = new CampaignState(m);
            // 同盟の状態を手で足す（盤面には同盟星系が無い）
            c.states.Add(new FactionState(Faction.同盟));
            // 同盟の OwnedSystemIds は空 → CohesionFactor=0 → 安定度1.0×0=0
            Assert.AreEqual(0f, CampaignRules.EffectiveStability(c, Faction.同盟), 1e-5f);
        }

        /// <summary>EnsureStates は null 星系をスキップし、重複所有勢力を1つに畳む（distinct owner 数）。</summary>
        [Test]
        public void EnsureStates_SkipsNullSystems_AndDedupesOwners()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国)); // 同じ帝国＝重複所有
            m.AddSystem(null);                 // null はスキップされる（カウントしない）
            m.AddSystem(Sys(2, Faction.同盟));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);
            // distinct owner = {帝国, 同盟} = 2（null は無視・帝国重複は1つ）
            Assert.AreEqual(2, c.states.Count);
            Assert.IsNotNull(CampaignRules.GetState(c, Faction.帝国));
            Assert.IsNotNull(CampaignRules.GetState(c, Faction.同盟));
        }

        /// <summary>EnsureStates は既存状態を上書きしない（カスタム値が保持される＝冪等の非破壊性）。</summary>
        [Test]
        public void EnsureStates_DoesNotOverwriteExistingState()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            var c = new CampaignState(m);
            // 先にカスタム状態を入れておく（legitimacy を 0.42 に）
            var custom = new FactionState(Faction.帝国);
            custom.regime.legitimacy = 0.42f;
            c.states.Add(custom);

            CampaignRules.EnsureStates(c);
            // 増えない（既に帝国状態がある）し、上書きもされない
            Assert.AreEqual(1, c.states.Count);
            Assert.AreSame(custom, CampaignRules.GetState(c, Faction.帝国));
            Assert.AreEqual(0.42f, CampaignRules.GetState(c, Faction.帝国).regime.legitimacy, 1e-5f);
        }

        /// <summary>Tick の境界：dt<=0 は no-op（dt=0 と dt=-1 で状態が一切変化しない）。</summary>
        [Test]
        public void Tick_NonPositiveDt_IsNoop()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);
            var s = CampaignRules.GetState(c, Faction.帝国);

            CampaignRules.Tick(c, 0f);
            Assert.AreEqual(0f, s.regime.corruption, 1e-5f);
            Assert.AreEqual(1f, s.regime.legitimacy, 1e-5f);

            CampaignRules.Tick(c, -1f);
            Assert.AreEqual(0f, s.regime.corruption, 1e-5f);
            Assert.AreEqual(1f, s.regime.legitimacy, 1e-5f);
        }

        /// <summary>Tick(dt=1) の数値を手計算で固定：corruption rise = 0.1×(1-virtue0.5)×1 = 0.05 → legitimacy=0.95。</summary>
        [Test]
        public void Tick_OneSecond_ExactCorruptionAndLegitimacy()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);
            var s = CampaignRules.GetState(c, Faction.帝国);
            // 既定 virtue=0.5、corruptionRate=0.1 → rise = 0.1*(1-0.5)*1 = 0.05
            CampaignRules.Tick(c, 1f);
            Assert.AreEqual(0.05f, s.regime.corruption, 1e-5f);
            Assert.AreEqual(0.95f, s.regime.legitimacy, 1e-5f);
        }

        /// <summary>Tick は null 状態を含む states でもクラッシュしない（FactionStateRules.Tick が null no-op）。</summary>
        [Test]
        public void Tick_WithNullStateEntry_DoesNotThrow()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            var c = new CampaignState(m);
            c.states.Add(null);                       // 異常：null エントリ
            c.states.Add(new FactionState(Faction.帝国));
            Assert.DoesNotThrow(() => CampaignRules.Tick(c, 1f));
            // 正常な帝国状態はちゃんと進む
            Assert.Greater(CampaignRules.GetState(c, Faction.帝国).regime.corruption, 0f);
        }

        /// <summary>LeadingFaction のタイブレーク：完全同点（両方 1.0×1.0=1.0）なら厳密 ">" により最初の状態勢力を返す。</summary>
        [Test]
        public void LeadingFaction_ExactTie_ReturnsFirstState()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.同盟)); // states[0] が同盟になるよう同盟を先に
            m.AddSystem(Sys(1, Faction.帝国));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);
            // 両者とも単一星系＝一体化1.0、安定度1.0 → 完全同点。厳密 ">" なので states[0]=同盟を維持。
            Assert.AreEqual(c.states[0].faction, CampaignRules.LeadingFaction(c));
            Assert.AreEqual(Faction.同盟, CampaignRules.LeadingFaction(c));
        }

        /// <summary>LeadingFaction は実効安定度（安定度×一体化度）で比べる：安定度が低くても一体化で逆転しうる。</summary>
        [Test]
        public void LeadingFaction_ComparesEffectiveNotRawStability()
        {
            // 帝国＝分断（一体化0.5）だが安定度1.0 → 実効0.5。
            // 同盟＝連結（一体化1.0）だが安定度を 0.4 に落とす → 実効0.4。
            // ⇒ 帝国(0.5) ＞ 同盟(0.4) で帝国が勝つ。
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddCorridor(new Corridor(0, 2, 1f)); // 帝国0 は敵星系2経由でしか帝国1へ繋がらない＝分断
            m.AddSystem(Sys(2, Faction.同盟));
            m.AddCorridor(new Corridor(2, 1, 1f));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);

            // 同盟の安定度を下げる（legitimacy/cooperation/cohesion/hope の平均=安定度。全部0.4に）
            var alliance = CampaignRules.GetState(c, Faction.同盟);
            alliance.regime.legitimacy = 0.4f;
            alliance.polity.cooperation = 0.4f;
            alliance.organization.cohesion = 0.4f;
            alliance.community.hope = 0.4f;

            // 帝国：所有{0,1}、回廊は 0-2 と 2-1 のみ＝帝国のみを通る連結成分は最大1 → 一体化 1/2=0.5、安定度1.0 → 実効0.5
            Assert.AreEqual(0.5f, CampaignRules.EffectiveStability(c, Faction.帝国), 1e-5f);
            // 同盟：所有{2}単独＝一体化1.0、安定度0.4 → 実効0.4
            Assert.AreEqual(0.4f, CampaignRules.EffectiveStability(c, Faction.同盟), 1e-5f);
            Assert.AreEqual(Faction.帝国, CampaignRules.LeadingFaction(c));
        }

        /// <summary>EffectiveStability の単調性：同じ盤面・同じ国家状態なら、一体化度が高い方が実効安定度も高い（割引の単調性）。</summary>
        [Test]
        public void EffectiveStability_MonotonicInCohesion()
        {
            // 連結盤面（一体化1.0）
            var mConnected = new GalaxyMap();
            mConnected.AddSystem(Sys(0, Faction.帝国));
            mConnected.AddSystem(Sys(1, Faction.帝国));
            mConnected.AddCorridor(new Corridor(0, 1, 1f));
            var cConnected = new CampaignState(mConnected);
            CampaignRules.EnsureStates(cConnected);

            // 分断盤面（一体化0.5）
            var mSplit = new GalaxyMap();
            mSplit.AddSystem(Sys(0, Faction.帝国));
            mSplit.AddSystem(Sys(1, Faction.帝国)); // 回廊なし＝完全分断
            var cSplit = new CampaignState(mSplit);
            CampaignRules.EnsureStates(cSplit);

            float connected = CampaignRules.EffectiveStability(cConnected, Faction.帝国);
            float split = CampaignRules.EffectiveStability(cSplit, Faction.帝国);
            Assert.Greater(connected, split);
            Assert.AreEqual(1.0f, connected, 1e-5f); // 1.0×1.0
            Assert.AreEqual(0.5f, split, 1e-5f);     // 1.0×0.5
        }

        // ───────── S5：税収・国庫・税負担（TickEconomy）─────────

        private static CampaignState OneFaction(out FactionState s)
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);
            s = CampaignRules.GetState(c, Faction.帝国);
            return c;
        }

        [Test]
        public void TickEconomy_CollectsTaxIntoTreasury()
        {
            var c = OneFaction(out var s);
            s.taxRate = 0.5f;
            float baseEco = CampaignRules.EconomyBase(s); // pop×係数×安定度
            float before = s.treasury;
            CampaignRules.TickEconomy(c, 1f);
            // 税収＝課税ベース×税率（dt=1）
            Assert.AreEqual(before + baseEco * 0.5f, s.treasury, 1e-4f);
            Assert.Greater(s.treasury, before);
        }

        [Test]
        public void TickEconomy_HigherTaxYieldsMoreRevenue_AndErodesHopeMore()
        {
            var cLow = OneFaction(out var sLow);  sLow.taxRate = 0.2f;
            var cHigh = OneFaction(out var sHigh); sHigh.taxRate = 0.8f;
            CampaignRules.TickEconomy(cLow, 1f);
            CampaignRules.TickEconomy(cHigh, 1f);
            // 高税ほど税収が多い
            Assert.Greater(sHigh.treasury, sLow.treasury);
            // 高税ほど民心(希望)が下がる
            Assert.Less(sHigh.community.hope, sLow.community.hope);
        }

        [Test]
        public void TickEconomy_ZeroTax_NoRevenue_NoBurden()
        {
            var c = OneFaction(out var s);
            s.taxRate = 0f;
            float hopeBefore = s.community.hope;
            CampaignRules.TickEconomy(c, 1f);
            Assert.AreEqual(0f, s.treasury, 1e-5f);          // 税率0＝税収0
            Assert.AreEqual(hopeBefore, s.community.hope, 1e-5f); // 負担0＝希望不変
        }

        [Test]
        public void TickEconomy_TaxBurden_DropsHope_ClampedAtZero()
        {
            var c = OneFaction(out var s);
            s.taxRate = 1f;
            s.community.hope = 0.001f; // 既に下限近く
            CampaignRules.TickEconomy(c, 100f); // 大 dt でも 0 未満にならない
            Assert.GreaterOrEqual(s.community.hope, 0f);
            Assert.AreEqual(0f, s.community.hope, 1e-5f);
        }

        [Test]
        public void TickEconomy_NullAndNonPositiveDt_Safe()
        {
            var c = OneFaction(out var s);
            float t = s.treasury;
            CampaignRules.TickEconomy(null, 1f);  // null 安全
            CampaignRules.TickEconomy(c, 0f);     // dt=0 で無変化
            CampaignRules.TickEconomy(c, -1f);    // 負 dt で無変化
            Assert.AreEqual(t, s.treasury, 1e-5f);
            Assert.AreEqual(0f, CampaignRules.EconomyBase(null), 1e-5f); // null 課税ベース=0
        }

        // ───────── TIME-6：暦の日次経済（TickEconomyDay）─────────

        [Test]
        public void TickEconomyDay_EqualsContinuousOverOneDay()
        {
            // 日次1回 == 連続版を1日の秒数(60)で積分＝総量一致（離散化しても暦比で同じ帰結）
            const float spd = 60f;
            var cDay = OneFaction(out var sDay);   sDay.taxRate = 0.5f;
            var cCont = OneFaction(out var sCont); sCont.taxRate = 0.5f;
            CampaignRules.TickEconomyDay(cDay, spd);
            CampaignRules.TickEconomy(cCont, spd);
            Assert.AreEqual(sCont.treasury, sDay.treasury, 1e-4f);
            Assert.AreEqual(sCont.community.hope, sDay.community.hope, 1e-5f);
        }

        [Test]
        public void TickEconomyDay_NonPositiveSecondsPerDay_Safe()
        {
            var c = OneFaction(out var s);
            s.taxRate = 0.5f;
            float t = s.treasury;
            CampaignRules.TickEconomyDay(c, 0f);   // 0 は無変化
            CampaignRules.TickEconomyDay(c, -60f); // 負も無変化
            CampaignRules.TickEconomyDay(null, 60f); // null 安全
            Assert.AreEqual(t, s.treasury, 1e-5f);
        }

        // ───────── 国家予算の基盤：歳出（TickBudget）─────────

        [Test]
        public void TickBudget_DeductsBudgetTotalFromTreasury()
        {
            var c = OneFaction(out var s);
            s.treasury = 500f;
            s.budget = new NationalBudget(military: 40f, shipbuilding: 20f, administration: 15f,
                                          welfare: 15f, research: 6f, diplomacy: 4f); // 合計100
            CampaignRules.TickBudget(c, 1f);
            Assert.AreEqual(400f, s.treasury, 1e-4f); // 500−100
        }

        [Test]
        public void TickBudget_OverspendingDrivesTreasuryNegative()
        {
            var c = OneFaction(out var s);
            s.treasury = 50f;
            s.budget = new NationalBudget(military: 100f, shipbuilding: 0f, administration: 0f,
                                          welfare: 0f, research: 0f, diplomacy: 0f);
            CampaignRules.TickBudget(c, 1f);
            Assert.Less(s.treasury, 0f); // 国庫超過＝赤字（国債相当）が可視化される
            Assert.AreEqual(-50f, s.treasury, 1e-4f);
        }

        [Test]
        public void TickBudget_EmptyOrNullBudget_NoChange_BackwardCompatible()
        {
            var c = OneFaction(out var s);
            s.treasury = 100f;
            // 既定＝空予算（歳出0）
            CampaignRules.TickBudget(c, 5f);
            Assert.AreEqual(100f, s.treasury, 1e-5f);
            // budget が null でも安全
            s.budget = null;
            Assert.DoesNotThrow(() => CampaignRules.TickBudget(c, 5f));
            Assert.AreEqual(100f, s.treasury, 1e-5f);
        }

        [Test]
        public void TickBudgetDay_EqualsContinuousOverOneDay()
        {
            const float spd = 60f;
            var cDay = OneFaction(out var sDay);
            var cCont = OneFaction(out var sCont);
            sDay.treasury = sCont.treasury = 1000f;
            sDay.budget = new NationalBudget(10f, 0f, 0f, 0f, 0f, 0f);
            sCont.budget = new NationalBudget(10f, 0f, 0f, 0f, 0f, 0f);
            CampaignRules.TickBudgetDay(cDay, spd);
            CampaignRules.TickBudget(cCont, spd);
            Assert.AreEqual(sCont.treasury, sDay.treasury, 1e-4f);
            Assert.AreEqual(400f, sDay.treasury, 1e-3f); // 1000 − 10×60
        }

        [Test]
        public void TickBudget_NullAndNonPositiveDt_Safe()
        {
            var c = OneFaction(out var s);
            s.treasury = 100f;
            s.budget = new NationalBudget(10f, 0f, 0f, 0f, 0f, 0f);
            CampaignRules.TickBudget(null, 1f);   // null 安全
            CampaignRules.TickBudget(c, 0f);      // dt=0 無変化
            CampaignRules.TickBudget(c, -1f);     // 負 dt 無変化
            CampaignRules.TickBudgetDay(c, 0f);   // 日次0 無変化
            CampaignRules.TickBudgetDay(null, 60f);
            Assert.AreEqual(100f, s.treasury, 1e-5f);
        }
    }
}
