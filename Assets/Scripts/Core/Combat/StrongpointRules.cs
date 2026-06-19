using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 拠点防御（防御陣地）の調整値（#拠点）。築城/地形/守備の効き・縦深の段数寄与・相互支援の射程逓減・
    /// 遅滞の基底・逆襲の即応寄与・孤立化の浸透重み・弾力性の合成重み・保持判定の既定閾値。
    /// すべて ctor で安全域へ Clamp する（不正値で破綻させない）。
    /// </summary>
    public readonly struct StrongpointParams
    {
        /// <summary>地形補正の下限（地形が最悪でもこれだけは残る・0..1）。</summary>
        public readonly float terrainFloor;
        /// <summary>地形補正の上限（要害でこの倍率まで＝1以上）。</summary>
        public readonly float terrainCeil;
        /// <summary>守備兵の寄与下限（守兵が薄くてもこれだけは残る・0..1）。</summary>
        public readonly float garrisonFloor;
        /// <summary>1線あたりの縦深寄与（線が増えるごとの上積み）。</summary>
        public readonly float depthPerLine;
        /// <summary>縦深に効く間隔の基準（これでフル・狭いと逓減）。</summary>
        public readonly float referenceSpacing;
        /// <summary>相互支援の隣接1火点あたり基礎寄与。</summary>
        public readonly float supportPerPosition;
        /// <summary>相互支援に効く射程の基準（これでフル・短いと逓減）。</summary>
        public readonly float referenceRange;
        /// <summary>突破時に残る遅滞の基底（防御力ゼロでも縦深ぶんは粘る・0..1）。</summary>
        public readonly float delayBase;
        /// <summary>逆襲に効く予備戦力の重み（0..1）。</summary>
        public readonly float reserveWeight;
        /// <summary>逆襲に効く即応（動員）の重み（0..1）。</summary>
        public readonly float mobilizationWeight;
        /// <summary>弾力性に効く縦深の重み（0..1）。</summary>
        public readonly float elasticityDepthWeight;
        /// <summary>弾力性に効く逆襲の重み（0..1）。</summary>
        public readonly float elasticityReserveWeight;
        /// <summary>保持判定の既定閾値（防御力−孤立リスクがこれ以上なら保持）。</summary>
        public readonly float holdThreshold;

        public StrongpointParams(
            float terrainFloor,
            float terrainCeil,
            float garrisonFloor,
            float depthPerLine,
            float referenceSpacing,
            float supportPerPosition,
            float referenceRange,
            float delayBase,
            float reserveWeight,
            float mobilizationWeight,
            float elasticityDepthWeight,
            float elasticityReserveWeight,
            float holdThreshold)
        {
            this.terrainFloor = Mathf.Clamp01(terrainFloor);
            this.terrainCeil = Mathf.Max(1f, terrainCeil);
            this.garrisonFloor = Mathf.Clamp01(garrisonFloor);
            this.depthPerLine = Mathf.Max(0f, depthPerLine);
            this.referenceSpacing = Mathf.Max(0.01f, referenceSpacing);
            this.supportPerPosition = Mathf.Max(0f, supportPerPosition);
            this.referenceRange = Mathf.Max(0.01f, referenceRange);
            this.delayBase = Mathf.Clamp01(delayBase);
            this.reserveWeight = Mathf.Clamp01(reserveWeight);
            this.mobilizationWeight = Mathf.Clamp01(mobilizationWeight);
            this.elasticityDepthWeight = Mathf.Clamp01(elasticityDepthWeight);
            this.elasticityReserveWeight = Mathf.Clamp01(elasticityReserveWeight);
            this.holdThreshold = Mathf.Clamp01(holdThreshold);
        }

        public const float DefaultTerrainFloor = 0.5f;
        public const float DefaultTerrainCeil = 1.5f;
        public const float DefaultGarrisonFloor = 0.3f;
        public const float DefaultDepthPerLine = 0.25f;
        public const float DefaultReferenceSpacing = 1.0f;
        public const float DefaultSupportPerPosition = 0.25f;
        public const float DefaultReferenceRange = 1.0f;
        public const float DefaultDelayBase = 0.2f;
        public const float DefaultReserveWeight = 0.5f;
        public const float DefaultMobilizationWeight = 0.5f;
        public const float DefaultElasticityDepthWeight = 0.5f;
        public const float DefaultElasticityReserveWeight = 0.5f;
        public const float DefaultHoldThreshold = 0.3f;

        /// <summary>
        /// 既定：地形0.5〜1.5・守備下限0.3・縦深0.25/線（基準間隔1.0）・相互支援0.25/火点（基準射程1.0）・
        /// 遅滞基底0.2・逆襲は予備/即応0.5ずつ・弾力性は縦深/逆襲0.5ずつ・保持閾値0.3。
        /// </summary>
        public static StrongpointParams Default => new StrongpointParams(
            DefaultTerrainFloor,
            DefaultTerrainCeil,
            DefaultGarrisonFloor,
            DefaultDepthPerLine,
            DefaultReferenceSpacing,
            DefaultSupportPerPosition,
            DefaultReferenceRange,
            DefaultDelayBase,
            DefaultReserveWeight,
            DefaultMobilizationWeight,
            DefaultElasticityDepthWeight,
            DefaultElasticityReserveWeight,
            DefaultHoldThreshold);
    }

    /// <summary>
    /// 拠点防御（防御陣地）の純ロジック（#拠点・test-first・実効値パターン）。
    /// 惑星・宙域の防御拠点＝<b>築城された陣地</b>のダイナミクス。要塞や前哨が単独で立つのでなく、
    /// <b>縦深（複数線）で突破に手間取らせ、相互支援する火点で互いを掩護し、突破されても予備の逆襲で押し戻す</b>。
    /// 相互支援を断たれた拠点は孤立して各個に潰される。
    /// <para>
    /// 役割分担：<see cref="PlanetSiegeRules"/>（惑星そのものの攻囲進行）・<see cref="FortressRules"/>（要塞単体の砲戦）とは<b>別</b>＝
    /// こちらは複数陣地の縦深・相互支援・遅滞・逆襲・孤立化のダイナミクス。係数合成は <see cref="CombatModifiers"/> が、
    /// 局所火力比は <see cref="LanchesterRules"/> が担う（二重実装しない）。
    /// </para>
    /// 盤面非依存の plain 引数のみ。各メソッドは Params 明示版＋Default 委譲版を持つ。LINQ/乱数/Log/Exp 不使用・決定論。
    /// </summary>
    public static class StrongpointRules
    {
        // ── 陣地の防御力（築城×地形×守備兵） ────────────────────────────
        public static float PositionStrength(float fortification, float terrain, float garrison)
            => PositionStrength(fortification, terrain, garrison, StrongpointParams.Default);

        /// <summary>
        /// 陣地の防御力（0..terrainCeil）。築城度 <paramref name="fortification"/>（0..1）に、地形補正
        /// （<paramref name="terrain"/> 0..1 を terrainFloor..terrainCeil へ写像）と守備兵寄与
        /// （<paramref name="garrison"/> 0..1 を garrisonFloor..1 へ写像）を掛ける。要害＋満守備でフル。
        /// </summary>
        public static float PositionStrength(float fortification, float terrain, float garrison, StrongpointParams p)
        {
            float fort = Mathf.Clamp01(fortification);
            float terrainFactor = Mathf.Lerp(p.terrainFloor, p.terrainCeil, Mathf.Clamp01(terrain));
            float garrisonFactor = Mathf.Lerp(p.garrisonFloor, 1f, Mathf.Clamp01(garrison));
            return Mathf.Max(0f, fort * terrainFactor * garrisonFactor);
        }

        // ── 縦深防御（防御線の数×間隔） ──────────────────────────────────
        public static float DefenseInDepth(int lineCount, float lineSpacing)
            => DefenseInDepth(lineCount, lineSpacing, StrongpointParams.Default);

        /// <summary>
        /// 縦深（0..1）。防御線の数 <paramref name="lineCount"/>（負は0）が増えるほど突破に手間取る＝
        /// 1線ごとに depthPerLine を上積み。線間隔 <paramref name="lineSpacing"/> が基準より狭いと縦深が逓減
        /// （ベタ置きは縦深にならない）。Clamp01。
        /// </summary>
        public static float DefenseInDepth(int lineCount, float lineSpacing, StrongpointParams p)
        {
            int lines = (int)Mathf.Max(0f, lineCount);
            float spacingFactor = Mathf.Clamp01(Mathf.Max(0f, lineSpacing) / p.referenceSpacing);
            return Mathf.Clamp01(lines * p.depthPerLine * spacingFactor);
        }

        // ── 相互支援（隣接火点×支援射程） ────────────────────────────────
        public static float MutualSupport(int adjacentPositions, float supportRange)
            => MutualSupport(adjacentPositions, supportRange, StrongpointParams.Default);

        /// <summary>
        /// 相互支援（0..1）。隣接する火点の数 <paramref name="adjacentPositions"/>（負は0）が多く、支援射程
        /// <paramref name="supportRange"/> が基準に届くほど互いを掩護できる。射程は基準で頭打ち（届かないと寄与減）。Clamp01。
        /// </summary>
        public static float MutualSupport(int adjacentPositions, float supportRange, StrongpointParams p)
        {
            int adjacent = (int)Mathf.Max(0f, adjacentPositions);
            float rangeFactor = Mathf.Clamp01(Mathf.Max(0f, supportRange) / p.referenceRange);
            return Mathf.Clamp01(adjacent * p.supportPerPosition * rangeFactor);
        }

        // ── 遅滞（突破されても粘る） ──────────────────────────────────────
        public static float DelayingAction(float positionStrength, float defenseInDepth)
            => DelayingAction(positionStrength, defenseInDepth, StrongpointParams.Default);

        /// <summary>
        /// 突破時の遅滞（0..1）。防御力 <paramref name="positionStrength"/> と縦深 <paramref name="defenseInDepth"/>（0..1）が
        /// 高いほど突破後も敵を引きつけて時間を稼ぐ。防御力が無くても縦深ぶんは delayBase で粘る。Clamp01。
        /// </summary>
        public static float DelayingAction(float positionStrength, float defenseInDepth, StrongpointParams p)
        {
            float strength = Mathf.Clamp01(positionStrength);
            float depth = Mathf.Clamp01(defenseInDepth);
            float fromDepth = Mathf.Lerp(0f, 1f, depth); // 縦深そのもの
            float core = strength * depth;
            return Mathf.Clamp01(core + p.delayBase * fromDepth * (1f - core));
        }

        // ── 逆襲（予備戦力×即応） ────────────────────────────────────────
        public static float CounterAttackReserve(float reserveStrength, float mobilization)
            => CounterAttackReserve(reserveStrength, mobilization, StrongpointParams.Default);

        /// <summary>
        /// 逆襲能力（0..1）。控置の予備戦力 <paramref name="reserveStrength"/>（0..1）と即応（動員速度）
        /// <paramref name="mobilization"/>（0..1）の重み付き積＝予備があっても動けねば逆襲にならず、動けても兵が無ければ撃てない。
        /// 両重みが効く乗算的合成（どちらか欠けると伸びない）。Clamp01。
        /// </summary>
        public static float CounterAttackReserve(float reserveStrength, float mobilization, StrongpointParams p)
        {
            float reserve = Mathf.Lerp(1f - p.reserveWeight, 1f, Mathf.Clamp01(reserveStrength));
            float mob = Mathf.Lerp(1f - p.mobilizationWeight, 1f, Mathf.Clamp01(mobilization));
            return Mathf.Clamp01(reserve * mob - (1f - p.reserveWeight) * (1f - p.mobilizationWeight));
        }

        // ── 孤立化リスク（相互支援を断たれ敵浸透） ────────────────────────
        public static float IsolationRisk(float mutualSupport, float enemyInfiltration)
            => IsolationRisk(mutualSupport, enemyInfiltration, StrongpointParams.Default);

        /// <summary>
        /// 拠点の孤立リスク（0..1）。相互支援 <paramref name="mutualSupport"/>（0..1）が薄いほど、また敵浸透
        /// <paramref name="enemyInfiltration"/>（0..1）が深いほど後背を断たれ各個撃破される。(1-相互支援)×敵浸透。
        /// </summary>
        public static float IsolationRisk(float mutualSupport, float enemyInfiltration, StrongpointParams p)
        {
            float exposure = 1f - Mathf.Clamp01(mutualSupport);
            float infiltration = Mathf.Clamp01(enemyInfiltration);
            return Mathf.Clamp01(exposure * infiltration);
        }

        // ── 防御の弾力性（押し込まれても戻す） ──────────────────────────
        public static float DefensiveElasticity(float defenseInDepth, float counterAttackReserve)
            => DefensiveElasticity(defenseInDepth, counterAttackReserve, StrongpointParams.Default);

        /// <summary>
        /// 防御の弾力性（0..1）。縦深 <paramref name="defenseInDepth"/>（押し込まれても吸収）と逆襲
        /// <paramref name="counterAttackReserve"/>（押し返す）の重み付き和＝弾力的防御は「下げて／溜めて／突く」。
        /// 縦深だけでも逆襲だけでも弾力にならず両輪。Clamp01。
        /// </summary>
        public static float DefensiveElasticity(float defenseInDepth, float counterAttackReserve, StrongpointParams p)
        {
            float depth = Mathf.Clamp01(defenseInDepth);
            float counter = Mathf.Clamp01(counterAttackReserve);
            float total = p.elasticityDepthWeight + p.elasticityReserveWeight;
            if (total <= 0f) return 0f;
            return Mathf.Clamp01((p.elasticityDepthWeight * depth + p.elasticityReserveWeight * counter) / total);
        }

        // ── 拠点を保持できるか ──────────────────────────────────────────
        public static bool IsPositionHeld(float positionStrength, float isolationRisk)
            => IsPositionHeld(positionStrength, isolationRisk, StrongpointParams.DefaultHoldThreshold);

        /// <summary>
        /// 陣地を保持できるか。防御力 <paramref name="positionStrength"/>（0..1相当）から孤立リスク
        /// <paramref name="isolationRisk"/>（0..1）を引いた実効保持力が <paramref name="threshold"/> 以上なら保持＝
        /// いくら固くても後背を断たれれば（孤立）持ちこたえられない。
        /// </summary>
        public static bool IsPositionHeld(float positionStrength, float isolationRisk, float threshold)
        {
            float strength = Mathf.Max(0f, positionStrength);
            float risk = Mathf.Clamp01(isolationRisk);
            float effective = strength - risk;
            return effective >= Mathf.Clamp01(threshold);
        }
    }
}
