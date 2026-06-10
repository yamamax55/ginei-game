using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内政（#109 P-1/P-2 最小ループ）の純ロジックを固定する：
    /// 安定度は目標値へ収束／思想一致で安定・不一致で不安定／占領直後は不安定→統合で回復／
    /// 産出は安定度比例（支配≠即産出）／低安定で反乱リスク。
    /// </summary>
    public class GovernanceTests
    {
        private static FactionData Faction(string ideology)
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ideology = ideology;
            return f;
        }

        private Province NativeProvince(string ideology = "民主") => new Province(1, ideology, 100f);

        // --- EquilibriumStability（目標値の純関数） ---

        [Test]
        public void Equilibrium_IdeologyMatch_RaisesAboveBase()
        {
            // 統合済み・思想一致・補給OK・非戦時 → 基準50＋一致25 = 75
            float t = GovernanceRules.EquilibriumStability(1f, GovernanceRules.IdeologyMatchBonus, true, false);
            Assert.AreEqual(75f, t, 1e-4f);
        }

        [Test]
        public void Equilibrium_Mismatch_War_Supply_StackPenalties()
        {
            // 統合済み・不一致(-20)・補給不足(-20)・戦時(-15) → 50-20-20-15 = -5 → clamp 0
            float t = GovernanceRules.EquilibriumStability(1f, -GovernanceRules.IdeologyMismatchPenalty, false, true);
            Assert.AreEqual(0f, t, 1e-4f);
        }

        [Test]
        public void Equilibrium_FreshlyOccupied_OccupationUnrestDominates()
        {
            // 未統合(integration=0)・不一致 → 50-20-(1-0)*40 = -10 → clamp 0
            float t0 = GovernanceRules.EquilibriumStability(0f, -GovernanceRules.IdeologyMismatchPenalty, true, false);
            Assert.AreEqual(0f, t0, 1e-4f);

            // 半分統合 → 50-20-(1-0.5)*40 = 10
            float t05 = GovernanceRules.EquilibriumStability(0.5f, -GovernanceRules.IdeologyMismatchPenalty, true, false);
            Assert.AreEqual(10f, t05, 1e-4f);

            // 完全統合 → 50-20 = 30（占領不満は消えるが思想不一致は残る）
            float t1 = GovernanceRules.EquilibriumStability(1f, -GovernanceRules.IdeologyMismatchPenalty, true, false);
            Assert.AreEqual(30f, t1, 1e-4f);
        }

        // --- IdeologyModifier（3状態） ---

        [Test]
        public void IdeologyModifier_Match_Mismatch_Unknown()
        {
            Assert.AreEqual(GovernanceRules.IdeologyMatchBonus,
                GovernanceRules.IdeologyModifier(Faction("民主"), "民主"), 1e-4f);
            Assert.AreEqual(-GovernanceRules.IdeologyMismatchPenalty,
                GovernanceRules.IdeologyModifier(Faction("専制"), "民主"), 1e-4f);
            // どちらか空＝中立(0)
            Assert.AreEqual(0f, GovernanceRules.IdeologyModifier(Faction(""), "民主"), 1e-4f);
            Assert.AreEqual(0f, GovernanceRules.IdeologyModifier(Faction("専制"), ""), 1e-4f);
            Assert.AreEqual(0f, GovernanceRules.IdeologyModifier(null, "民主"), 1e-4f);
        }

        // --- Tick（収束・統合の進行） ---

        [Test]
        public void Tick_RaisesIntegrationOverTime()
        {
            var p = NativeProvince();
            p.integration = 0f;
            GovernanceRules.Tick(p, Faction("民主"), supplyOk: true, atWar: false, deltaTime: 1f);
            Assert.AreEqual(GovernanceRules.IntegrationRate, p.integration, 1e-4f);
        }

        [Test]
        public void Tick_MovesStabilityTowardTarget()
        {
            // 思想一致・統合済み → 目標75。基準50から上昇する。
            var p = NativeProvince("民主");
            float before = p.stability; // = BaseStability(50)
            GovernanceRules.Tick(p, Faction("民主"), true, false, 1f);
            Assert.Greater(p.stability, before);
            Assert.LessOrEqual(p.stability, 75f);
            // 1tickで StabilitySpeed ぶんだけ寄る（50→56）
            Assert.AreEqual(before + GovernanceRules.StabilitySpeed, p.stability, 1e-4f);
        }

        [Test]
        public void Tick_ZeroOrNegativeDelta_NoChange()
        {
            var p = NativeProvince();
            float s = p.stability; float ig = p.integration;
            GovernanceRules.Tick(p, Faction("民主"), true, false, 0f);
            Assert.AreEqual(s, p.stability, 1e-4f);
            Assert.AreEqual(ig, p.integration, 1e-4f);
        }

        // --- OnOccupied（占領で不安定化） ---

        [Test]
        public void OnOccupied_ResetsIntegrationAndLowersStability()
        {
            var p = NativeProvince();
            p.stability = 80f; p.integration = 1f;
            GovernanceRules.OnOccupied(p);
            Assert.AreEqual(0f, p.integration, 1e-4f);
            Assert.AreEqual(GovernanceRules.OccupiedInitialStability, p.stability, 1e-4f);
            Assert.IsTrue(GovernanceRules.IsUnrest(p)); // 占領直後は反乱リスク域
        }

        // --- OutputFactor（支配≠即産出） ---

        [Test]
        public void OutputFactor_ScalesWithStability_Monotonic()
        {
            var low = NativeProvince(); low.stability = 0f;
            var mid = NativeProvince(); mid.stability = 50f;
            var high = NativeProvince(); high.stability = 100f;

            Assert.AreEqual(GovernanceRules.MinOutputFactor, GovernanceRules.OutputFactor(low), 1e-4f);
            Assert.AreEqual(1f, GovernanceRules.OutputFactor(high), 1e-4f);
            Assert.Less(GovernanceRules.OutputFactor(low), GovernanceRules.OutputFactor(mid));
            Assert.Less(GovernanceRules.OutputFactor(mid), GovernanceRules.OutputFactor(high));
        }

        // --- 反乱圧 ---

        [Test]
        public void RebelPressure_ZeroAboveThreshold_RisesBelow()
        {
            var p = NativeProvince();
            p.stability = GovernanceRules.RebelThreshold; // 境界＝0
            Assert.AreEqual(0f, GovernanceRules.RebelPressure(p), 1e-4f);
            Assert.IsFalse(GovernanceRules.IsUnrest(p));

            p.stability = 0f; // 最大圧＝1
            Assert.AreEqual(1f, GovernanceRules.RebelPressure(p), 1e-4f);
            Assert.IsTrue(GovernanceRules.IsUnrest(p));
        }

        // --- 統合シナリオ：占領→放置で統合し安定が回復（思想一致なら高安定） ---

        [Test]
        public void Scenario_OccupiedFriendlyIdeology_IntegratesToHighStability()
        {
            var p = NativeProvince("民主");
            GovernanceRules.OnOccupied(p);              // 占領直後：integration0・stability15
            var owner = Faction("民主");                // 解放＝思想一致
            // 十分長く統治（統合が 1 に達するまで回す）
            for (int i = 0; i < 200; i++)
                GovernanceRules.Tick(p, owner, supplyOk: true, atWar: false, deltaTime: 1f);

            Assert.AreEqual(1f, p.integration, 1e-3f);
            Assert.AreEqual(75f, p.stability, 0.5f);    // 50+25（一致）へ収束
            Assert.IsFalse(GovernanceRules.IsUnrest(p));
        }

        [Test]
        public void Scenario_OccupiedHostileIdeology_StaysRestless()
        {
            var p = NativeProvince("民主");
            GovernanceRules.OnOccupied(p);
            var owner = Faction("専制");                // 占領＝思想不一致
            for (int i = 0; i < 200; i++)
                GovernanceRules.Tick(p, owner, supplyOk: true, atWar: false, deltaTime: 1f);

            Assert.AreEqual(1f, p.integration, 1e-3f);
            Assert.AreEqual(30f, p.stability, 0.5f);    // 50-20 止まり＝統合しても燻る
        }

        // =====================================================================
        // 敵対的エッジケース追加（境界/クランプ/分岐/null/集約の不変条件）
        // =====================================================================

        // --- EquilibriumStability：上限クランプ（大きすぎる思想補正で100超を100へ） ---

        [Test]
        public void Equilibrium_UpperClamp_AtHundred()
        {
            // 統合済み・補給OK・非戦時・巨大な思想補正(+60) → 50+60=110 → clamp 100
            float t = GovernanceRules.EquilibriumStability(1f, 60f, true, false);
            Assert.AreEqual(100f, t, 1e-4f);
        }

        // --- EquilibriumStability：integration<0 と >1 の Clamp01（占領不満は0..1で頭打ち） ---

        [Test]
        public void Equilibrium_IntegrationClampedTo01_NegativeBehavesAsZero()
        {
            // integration=-5 は Clamp01 で 0 扱い → 50 + 0 - (1-0)*40 = 10
            float tNeg = GovernanceRules.EquilibriumStability(-5f, 0f, true, false);
            Assert.AreEqual(10f, tNeg, 1e-4f);

            // integration=5 は Clamp01 で 1 扱い → 50 + 0 - 0 = 50（占領不満なし）
            float tOver = GovernanceRules.EquilibriumStability(5f, 0f, true, false);
            Assert.AreEqual(50f, tOver, 1e-4f);
        }

        // --- 統治政策(#112) 各分岐：PolicyStabilityModifier ---

        [Test]
        public void PolicyStabilityModifier_AllBranches()
        {
            Assert.AreEqual(GovernanceRules.PolicyCivilStability,
                GovernanceRules.PolicyStabilityModifier(GovernancePolicy.民生), 1e-4f);
            Assert.AreEqual(GovernanceRules.PolicyMobilizeStability,
                GovernanceRules.PolicyStabilityModifier(GovernancePolicy.動員), 1e-4f);
            Assert.AreEqual(GovernanceRules.PolicySuppressStability,
                GovernanceRules.PolicyStabilityModifier(GovernancePolicy.弾圧), 1e-4f);
            Assert.AreEqual(GovernanceRules.PolicyLiberateStability,
                GovernanceRules.PolicyStabilityModifier(GovernancePolicy.解放), 1e-4f);
        }

        // --- 統治政策(#112) 各分岐：PolicyIntegrationMultiplier（弾圧=遅い/解放=速い/他=等倍） ---

        [Test]
        public void PolicyIntegrationMultiplier_AllBranches()
        {
            Assert.AreEqual(1f,
                GovernanceRules.PolicyIntegrationMultiplier(GovernancePolicy.民生), 1e-4f);
            Assert.AreEqual(1f,
                GovernanceRules.PolicyIntegrationMultiplier(GovernancePolicy.動員), 1e-4f);
            Assert.AreEqual(GovernanceRules.PolicySuppressIntegrationMul,
                GovernanceRules.PolicyIntegrationMultiplier(GovernancePolicy.弾圧), 1e-4f);
            Assert.AreEqual(GovernanceRules.PolicyLiberateIntegrationMul,
                GovernanceRules.PolicyIntegrationMultiplier(GovernancePolicy.解放), 1e-4f);
        }

        // --- EquilibriumStability with policy：弾圧は安定+12 を上乗せ ---

        [Test]
        public void Equilibrium_SuppressPolicy_AddsStability()
        {
            // 統合済み・思想中立(0)・補給OK・非戦時・弾圧(+12) → 50+12 = 62
            float t = GovernanceRules.EquilibriumStability(1f, 0f, true, false, GovernancePolicy.弾圧);
            Assert.AreEqual(50f + GovernanceRules.PolicySuppressStability, t, 1e-4f);
        }

        // --- Tick：integration の上限クランプ（既に1なら1で頭打ち、解放倍率でも超えない） ---

        [Test]
        public void Tick_IntegrationClampsAtOne_NoOverflow()
        {
            var p = NativeProvince("民主");
            p.integration = 1f; // 既に完全統合
            // 解放政策(1.8倍)で大きな dt を入れても 1 を超えない
            GovernanceRules.Tick(p, Faction("民主"), true, false, 100f, GovernancePolicy.解放);
            Assert.AreEqual(1f, p.integration, 1e-4f);
        }

        // --- Tick：解放政策は統合速度が速い（民生より大きく進む） ---

        [Test]
        public void Tick_LiberatePolicy_IntegratesFasterThanCivil()
        {
            var pCivil = NativeProvince("民主"); pCivil.integration = 0f;
            var pLib = NativeProvince("民主"); pLib.integration = 0f;
            GovernanceRules.Tick(pCivil, Faction("民主"), true, false, 1f, GovernancePolicy.民生);
            GovernanceRules.Tick(pLib, Faction("民主"), true, false, 1f, GovernancePolicy.解放);
            // 解放=1.8倍 → IntegrationRate*1.8
            Assert.AreEqual(GovernanceRules.IntegrationRate, pCivil.integration, 1e-4f);
            Assert.AreEqual(GovernanceRules.IntegrationRate * GovernanceRules.PolicyLiberateIntegrationMul,
                pLib.integration, 1e-4f);
            Assert.Greater(pLib.integration, pCivil.integration);
        }

        // --- Tick：null province は例外を出さず無変化 ---

        [Test]
        public void Tick_NullProvince_NoThrow()
        {
            Assert.DoesNotThrow(() => GovernanceRules.Tick(null, Faction("民主"), true, false, 1f));
        }

        // --- Tick：負の delta は無変化（ゼロ同様にガード） ---

        [Test]
        public void Tick_NegativeDelta_NoChange()
        {
            var p = NativeProvince();
            float s = p.stability; float ig = p.integration;
            GovernanceRules.Tick(p, Faction("民主"), true, false, -5f);
            Assert.AreEqual(s, p.stability, 1e-4f);
            Assert.AreEqual(ig, p.integration, 1e-4f);
        }

        // --- OnOccupied：null は例外を出さない ---

        [Test]
        public void OnOccupied_Null_NoThrow()
        {
            Assert.DoesNotThrow(() => GovernanceRules.OnOccupied(null));
        }

        // --- OutputFactor：null→0、stability>100 や <0 でも 0.3..1 にクランプ ---

        [Test]
        public void OutputFactor_NullAndOutOfRangeStability()
        {
            Assert.AreEqual(0f, GovernanceRules.OutputFactor(null), 1e-4f);

            var over = NativeProvince(); over.stability = 250f;     // >100 → t=1 → 1.0
            Assert.AreEqual(1f, GovernanceRules.OutputFactor(over), 1e-4f);

            var neg = NativeProvince(); neg.stability = -50f;       // <0 → t=0 → MinOutputFactor
            Assert.AreEqual(GovernanceRules.MinOutputFactor, GovernanceRules.OutputFactor(neg), 1e-4f);
        }

        // --- RebelPressure：null→0、ちょうど閾値直下は単調に上がる ---

        [Test]
        public void RebelPressure_NullAndJustBelowThreshold()
        {
            Assert.AreEqual(0f, GovernanceRules.RebelPressure(null), 1e-4f);

            var p = NativeProvince();
            // RebelThreshold(25)の半分=12.5 → (25-12.5)/25 = 0.5
            p.stability = GovernanceRules.RebelThreshold * 0.5f;
            Assert.AreEqual(0.5f, GovernanceRules.RebelPressure(p), 1e-4f);
            Assert.IsTrue(GovernanceRules.IsUnrest(p));
        }

        // --- IsUnrest：null は false（反乱なし扱い） ---

        [Test]
        public void IsUnrest_Null_IsFalse()
        {
            Assert.IsFalse(GovernanceRules.IsUnrest(null));
        }

        // --- AggregateSystem：null と 空リストは既定(planetCount=0) ---

        [Test]
        public void Aggregate_NullAndEmpty_ReturnsDefault()
        {
            var fromNull = GovernanceRules.AggregateSystem(null);
            Assert.AreEqual(0, fromNull.planetCount);
            Assert.AreEqual("", fromNull.dominantIdeology); // ctor で null→"" 正規化

            var fromEmpty = GovernanceRules.AggregateSystem(new System.Collections.Generic.List<Province>());
            Assert.AreEqual(0, fromEmpty.planetCount);
        }

        // --- AggregateSystem：全要素 null は count=0 で既定 ---

        [Test]
        public void Aggregate_AllNullEntries_ReturnsDefault()
        {
            var list = new System.Collections.Generic.List<Province> { null, null, null };
            var g = GovernanceRules.AggregateSystem(list);
            Assert.AreEqual(0, g.planetCount);
            Assert.AreEqual(0f, g.totalPopulation, 1e-4f);
        }

        // --- AggregateSystem：人口加重平均が正しい（合計保存則・手計算） ---

        [Test]
        public void Aggregate_WeightedAverages_Correct()
        {
            // 惑星A: pop=100, stab=80, int=1.0
            // 惑星B: pop=300, stab=40, int=0.5
            var a = new Province(1, "民主", 100f); a.stability = 80f; a.integration = 1f;
            var b = new Province(2, "専制", 300f); b.stability = 40f; b.integration = 0.5f;
            var g = GovernanceRules.AggregateSystem(
                new System.Collections.Generic.List<Province> { a, b });

            Assert.AreEqual(2, g.planetCount);
            Assert.AreEqual(400f, g.totalPopulation, 1e-4f);
            // 加重安定 = (80*100 + 40*300)/400 = (8000+12000)/400 = 50
            Assert.AreEqual(50f, g.weightedStability, 1e-4f);
            // 加重統合 = (1.0*100 + 0.5*300)/400 = (100+150)/400 = 0.625
            Assert.AreEqual(0.625f, g.weightedIntegration, 1e-4f);
            // 支配思想 = 人口最多の専制(300>100)
            Assert.AreEqual("専制", g.dominantIdeology);
            // totalOutput = Σ OutputFactor×pop
            float expectedOutput = GovernanceRules.OutputFactor(a) * 100f
                                 + GovernanceRules.OutputFactor(b) * 300f;
            Assert.AreEqual(expectedOutput, g.totalOutput, 1e-3f);
        }

        // --- AggregateSystem：全 pop=0 はゼロ割回避＝単純平均にフォールバック ---

        [Test]
        public void Aggregate_AllZeroPopulation_SimpleAverageFallback()
        {
            var a = new Province(1, "民主", 0f); a.stability = 30f; a.integration = 0.2f;
            var b = new Province(2, "民主", 0f); b.stability = 70f; b.integration = 0.8f;
            var g = GovernanceRules.AggregateSystem(
                new System.Collections.Generic.List<Province> { a, b });

            Assert.AreEqual(2, g.planetCount);
            Assert.AreEqual(0f, g.totalPopulation, 1e-4f);
            // 単純平均 stab=(30+70)/2=50、int=(0.2+0.8)/2=0.5
            Assert.AreEqual(50f, g.weightedStability, 1e-4f);
            Assert.AreEqual(0.5f, g.weightedIntegration, 1e-4f);
            // pop=0 でも totalOutput は OutputFactor×0=0
            Assert.AreEqual(0f, g.totalOutput, 1e-4f);
        }

        // --- AggregateSystem：negative population は Max(0,..) でクランプされ加重に影響しない ---

        [Test]
        public void Aggregate_NegativePopulation_ClampedToZero()
        {
            // Province ctor が population を Max(0,..) する。フィールド直書きで負を強制しても集約側 Max(0)で守る。
            var a = new Province(1, "民主", 100f); a.stability = 60f; a.integration = 1f;
            var b = new Province(2, "専制", 100f); b.stability = 20f; b.integration = 0f;
            b.population = -100f; // 集約側 Mathf.Max(0,pop) で 0 になる想定

            var g = GovernanceRules.AggregateSystem(
                new System.Collections.Generic.List<Province> { a, b });

            // 有効人口 = 100(a) + 0(b) = 100。加重は a のみ。
            Assert.AreEqual(100f, g.totalPopulation, 1e-4f);
            Assert.AreEqual(60f, g.weightedStability, 1e-4f);
            Assert.AreEqual(1f, g.weightedIntegration, 1e-4f);
            Assert.AreEqual("民主", g.dominantIdeology); // 専制は pop=0 で 0票
        }

        // --- AggregateSystem：anyUnrest と maxRebelPressure（最悪値）が拾われる ---

        [Test]
        public void Aggregate_UnrestAndMaxRebelPressure()
        {
            var calm = new Province(1, "民主", 100f); calm.stability = 80f;     // 平穏
            var riot = new Province(2, "民主", 100f); riot.stability = 0f;       // 反乱圧=1
            var g = GovernanceRules.AggregateSystem(
                new System.Collections.Generic.List<Province> { calm, riot });

            Assert.IsTrue(g.anyUnrest);
            Assert.AreEqual(1f, g.maxRebelPressure, 1e-4f);
        }

        // --- AggregateSystem：null 混在でも有効惑星だけ集約（count は非null数） ---

        [Test]
        public void Aggregate_SkipsNullEntries_CountsOnlyValid()
        {
            var a = new Province(1, "民主", 100f); a.stability = 50f; a.integration = 1f;
            var g = GovernanceRules.AggregateSystem(
                new System.Collections.Generic.List<Province> { null, a, null });

            Assert.AreEqual(1, g.planetCount);
            Assert.AreEqual(100f, g.totalPopulation, 1e-4f);
            Assert.AreEqual(50f, g.weightedStability, 1e-4f);
        }

        // --- AggregateSystem：空思想(nativeIdeology="")は支配思想集計に入らない ---

        [Test]
        public void Aggregate_EmptyIdeology_NotCounted_DominantStaysEmpty()
        {
            var a = new Province(1, "", 100f);  // 思想不明
            var b = new Province(2, "", 200f);  // 思想不明
            var g = GovernanceRules.AggregateSystem(
                new System.Collections.Generic.List<Province> { a, b });

            Assert.AreEqual(2, g.planetCount);
            Assert.AreEqual("", g.dominantIdeology); // 空思想は除外＝支配思想なし
        }
    }
}
