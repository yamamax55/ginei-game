using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦列（陣列）の維持/突破/崩壊の調整値（#戦列）。密度の適正幅・相互支援の効き・突破閾値・崩壊伝播・立て直しの基底値。
    /// すべて ctor で安全域へ Clamp する（不正値で破綻させない）。
    /// </summary>
    public readonly struct BattleLineParams
    {
        /// <summary>適正な艦間隔（密度の中心）。これに近いほど結束が高い。</summary>
        public readonly float idealSpacing;
        /// <summary>艦間隔の許容幅（これだけ外れると結束が下限へ）。</summary>
        public readonly float spacingTolerance;
        /// <summary>結束の下限係数（密度が崩れても最低これだけは残る・0..1）。</summary>
        public readonly float spacingFloor;
        /// <summary>突破が成立する圧力の閾値（圧力がこれ以上で戦列突破）。</summary>
        public readonly float breachThreshold;
        /// <summary>隙の基準密度（兵力/幅 がこれ以上なら隙なし＝薄く広げると脆い）。</summary>
        public readonly float referenceDensity;
        /// <summary>立て直しの基底成功率（予備兵力・統率に依らず最低これだけ・0..1）。</summary>
        public readonly float reformBase;
        /// <summary>立て直しに対する予備兵力の寄与重み（0..1）。</summary>
        public readonly float reformReserveWeight;
        /// <summary>立て直しに対する統率の寄与重み（0..1）。</summary>
        public readonly float reformLeadershipWeight;

        public BattleLineParams(
            float idealSpacing,
            float spacingTolerance,
            float spacingFloor,
            float breachThreshold,
            float referenceDensity,
            float reformBase,
            float reformReserveWeight,
            float reformLeadershipWeight)
        {
            this.idealSpacing = Mathf.Max(0.01f, idealSpacing);
            this.spacingTolerance = Mathf.Max(0.01f, spacingTolerance);
            this.spacingFloor = Mathf.Clamp01(spacingFloor);
            this.breachThreshold = Mathf.Max(0.01f, breachThreshold);
            this.referenceDensity = Mathf.Max(0.01f, referenceDensity);
            this.reformBase = Mathf.Clamp01(reformBase);
            this.reformReserveWeight = Mathf.Clamp01(reformReserveWeight);
            this.reformLeadershipWeight = Mathf.Clamp01(reformLeadershipWeight);
        }

        public const float DefaultIdealSpacing = 1.0f;
        public const float DefaultSpacingTolerance = 0.5f;
        public const float DefaultSpacingFloor = 0.5f;
        public const float DefaultBreachThreshold = 1.0f;
        public const float DefaultReferenceDensity = 1.0f;
        public const float DefaultReformBase = 0.2f;
        public const float DefaultReformReserveWeight = 0.4f;
        public const float DefaultReformLeadershipWeight = 0.4f;

        /// <summary>既定：適正間隔1.0・許容幅0.5・結束下限0.5・突破閾値1.0・基準密度1.0・立て直し基底0.2＋予備/統率0.4。</summary>
        public static BattleLineParams Default => new BattleLineParams(
            DefaultIdealSpacing,
            DefaultSpacingTolerance,
            DefaultSpacingFloor,
            DefaultBreachThreshold,
            DefaultReferenceDensity,
            DefaultReformBase,
            DefaultReformReserveWeight,
            DefaultReformLeadershipWeight);
    }

    /// <summary>
    /// 戦列（陣列）の維持と崩壊の純ロジック（#戦列・test-first・実効値パターン）。
    /// 艦隊は戦列を組んで戦う＝<b>戦列が保たれている間は相互支援で強い</b>が、<b>一点を突破されると崩れて各個撃破される</b>。
    /// 戦列の幅・密度・結束で耐久が決まり、薄く広げると隙だらけになる。
    /// 一点への敵集中が結束を上回ると突破が起き、結束が低いほど崩壊が連鎖（カスケード）する。
    /// <para>
    /// 役割分担：<see cref="FormationTraitRules"/>（陣形そのものの攻撃/防御/機動の特性倍率）とは<b>別</b>＝こちらは
    /// 戦列の維持/突破/崩壊伝播のダイナミクス。<see cref="BattleTempoRules"/>（会戦のテンポ）とも<b>別</b>。
    /// 局所火力比は <see cref="LanchesterRules"/> が、係数合成は <see cref="CombatModifiers"/> が担う（二重実装しない）。
    /// </para>
    /// 盤面非依存の plain 引数のみ。各メソッドは Params 明示版＋Default 委譲版を持つ。
    /// </summary>
    public static class BattleLineRules
    {
        // ── 戦列の結束（密度が適正なほど高い） ────────────────────────────
        public static float LineCohesion(float formationIntegrity, float spacing)
            => LineCohesion(formationIntegrity, spacing, BattleLineParams.Default);

        /// <summary>
        /// 戦列の結束（0..1）。陣列の保たれ具合 <paramref name="formationIntegrity"/>（0..1）に、艦間隔 <paramref name="spacing"/> が
        /// 適正密度から外れたぶんの低下を掛ける。適正間隔ぴったりで満点、許容幅ぶん外れると結束下限まで落ちる。
        /// </summary>
        public static float LineCohesion(float formationIntegrity, float spacing, BattleLineParams p)
        {
            float integrity = Mathf.Clamp01(formationIntegrity);
            float deviation = Mathf.Abs(Mathf.Max(0f, spacing) - p.idealSpacing) / p.spacingTolerance;
            float densityFactor = Mathf.Lerp(1f, p.spacingFloor, Mathf.Clamp01(deviation)); // 適正=1 → 外れるほど floor へ
            return Mathf.Clamp01(integrity * densityFactor);
        }

        // ── 戦列内の相互支援火力（隣接艦の援護） ──────────────────────────
        /// <summary>
        /// 戦列内の相互支援火力倍率。結束 <paramref name="lineCohesion"/>（0..1）が高く、戦列幅 <paramref name="lineWidth"/> が広いほど
        /// 援護し合えるが、幅は平方根で逓減（横に広げても線形には増えない）。
        /// </summary>
        public static float MutualSupport(float lineCohesion, float lineWidth)
        {
            float cohesion = Mathf.Clamp01(lineCohesion);
            float width = Mathf.Max(1f, lineWidth);
            return cohesion * Mathf.Sqrt(width);
        }

        // ── 一点への敵集中が戦列を破る圧力 ────────────────────────────────
        /// <summary>
        /// 突破圧力。一点に集中した敵戦力 <paramref name="localEnemyConcentration"/> を、戦列の結束 <paramref name="lineCohesion"/> で
        /// 割る＝結束が高いほど同じ集中でも圧力は小さい（相互支援で受け止める）。1.0 が拮抗の目安。
        /// </summary>
        public static float BreakthroughPressure(float localEnemyConcentration, float lineCohesion)
        {
            float concentration = Mathf.Max(0f, localEnemyConcentration);
            float cohesion = Mathf.Max(0.01f, Mathf.Clamp01(lineCohesion));
            return concentration / cohesion;
        }

        // ── 戦列突破が起きるか ────────────────────────────────────────────
        public static bool LineBreach(float breakthroughPressure)
            => LineBreach(breakthroughPressure, BattleLineParams.Default.breachThreshold);

        /// <summary>突破圧力が閾値 <paramref name="threshold"/> 以上なら戦列突破が成立（true）。</summary>
        public static bool LineBreach(float breakthroughPressure, float threshold)
            => Mathf.Max(0f, breakthroughPressure) >= Mathf.Max(0.01f, threshold);

        // ── 突破後に崩壊が伝播する度合い（結束が低いと連鎖） ──────────────
        /// <summary>
        /// 崩壊カスケード（0..1）。突破の深刻度 <paramref name="breachSeverity"/>（0..1）が大きく、結束 <paramref name="lineCohesion"/> が
        /// 低いほど、突破口から崩れが伝播する（各個撃破）。結束が満点なら連鎖しない。
        /// </summary>
        public static float CollapseCascade(float breachSeverity, float lineCohesion)
        {
            float severity = Mathf.Clamp01(breachSeverity);
            float cohesion = Mathf.Clamp01(lineCohesion);
            return Mathf.Clamp01(severity * (1f - cohesion));
        }

        // ── 薄く広げると隙だらけになる脆弱性 ──────────────────────────────
        public static float GapVulnerability(float lineWidth, float ownStrength)
            => GapVulnerability(lineWidth, ownStrength, BattleLineParams.Default);

        /// <summary>
        /// 隙の脆弱性（0..1）。戦列の密度＝兵力 <paramref name="ownStrength"/> ÷ 幅 <paramref name="lineWidth"/> が基準密度を下回るほど
        /// 隙だらけになる（薄く広げると脆い）。基準密度以上なら隙なし（0）。
        /// </summary>
        public static float GapVulnerability(float lineWidth, float ownStrength, BattleLineParams p)
        {
            float width = Mathf.Max(0.01f, lineWidth);
            float strength = Mathf.Max(0f, ownStrength);
            float density = strength / width;
            return Mathf.Clamp01(1f - density / p.referenceDensity);
        }

        // ── 崩れた戦列の立て直し ──────────────────────────────────────────
        public static float LineReformChance(float reserves, float leadership)
            => LineReformChance(reserves, leadership, BattleLineParams.Default);

        /// <summary>
        /// 戦列の立て直し成功率（0..1）。基底に、温存した予備兵力 <paramref name="reserves"/>（0..1）と指揮官の統率
        /// <paramref name="leadership"/>（0..100）の寄与を加える。予備と統率が揃えば崩れた戦列を再編できる。
        /// </summary>
        public static float LineReformChance(float reserves, float leadership, BattleLineParams p)
        {
            float res = Mathf.Clamp01(reserves);
            float ldr = Mathf.Clamp01(Mathf.Max(0f, leadership) / 100f);
            float chance = p.reformBase + res * p.reformReserveWeight + ldr * p.reformLeadershipWeight;
            return Mathf.Clamp01(chance);
        }

        // ── 戦列が保たれているか ──────────────────────────────────────────
        public static bool IsLineHolding(float lineCohesion, float breakthroughPressure)
            => IsLineHolding(lineCohesion, breakthroughPressure, BattleLineParams.Default.breachThreshold);

        /// <summary>
        /// 戦列が保たれているか（true）。結束が残り（&gt;0）、かつ突破圧力が閾値 <paramref name="threshold"/> 未満＝
        /// まだ突破されていない間は戦列維持。
        /// </summary>
        public static bool IsLineHolding(float lineCohesion, float breakthroughPressure, float threshold)
        {
            if (Mathf.Clamp01(lineCohesion) <= 0f) return false;
            return Mathf.Max(0f, breakthroughPressure) < Mathf.Max(0.01f, threshold);
        }
    }
}
