using UnityEngine;

namespace Ginei
{
    /// <summary>士気伝播の調整係数（勝ち戦の高揚・敗報の動揺が隣接部隊へ伝染する強さ）。</summary>
    public readonly struct MoraleContagionParams
    {
        /// <summary>距離減衰率（大きいほど近接でしか伝播しない・代数式 1/(1+falloff×距離)）。</summary>
        public readonly float distanceFalloff;
        /// <summary>勝利の高揚スケール（局所勝利の大きさ1で生む正の士気源）。</summary>
        public readonly float victoryScale;
        /// <summary>敗報の動揺スケール（局所敗北の大きさ1で生む負の士気源の絶対値）。</summary>
        public readonly float defeatScale;
        /// <summary>結束1のときの動揺伝染の最大減衰幅（固い部隊ほど負の伝染に抗う）。</summary>
        public readonly float cohesionDampen;
        /// <summary>威信1のときの増幅/抑制幅（高揚を増し動揺を抑える）。</summary>
        public readonly float prestigeAmplify;

        public MoraleContagionParams(float distanceFalloff, float victoryScale, float defeatScale,
                                     float cohesionDampen, float prestigeAmplify)
        {
            this.distanceFalloff = Mathf.Max(0f, distanceFalloff);
            this.victoryScale = Mathf.Clamp01(victoryScale);
            this.defeatScale = Mathf.Clamp01(defeatScale);
            this.cohesionDampen = Mathf.Clamp01(cohesionDampen);
            this.prestigeAmplify = Mathf.Clamp01(prestigeAmplify);
        }

        /// <summary>既定＝距離減衰0.5・高揚0.6・動揺0.7・結束抗体0.8・威信増幅0.5。</summary>
        public static MoraleContagionParams Default => new MoraleContagionParams(0.5f, 0.6f, 0.7f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 士気伝播の純ロジック（勝ち戦の高揚と敗報の動揺が隣接部隊へ伝染する）。一角が勝てば高揚が広がり、
    /// 一角が崩れれば動揺が伝播する＝距離で減衰し、結束の固い部隊は動揺に抗い、威信ある指揮官は
    /// 高揚を増幅し動揺を抑える。符号付き（正＝高揚／負＝動揺）の汎用士気伝播。
    /// <para>
    /// 分担：<c>PanicCascadeRules</c>（恐慌の空間カスケード＝敗走の負方向の伝染・将来実装/別系統）とは別＝
    /// こちらは<b>勝敗両方向</b>の汎用士気伝播。<see cref="FleetMorale"/>（士気の実体）は read-only 相当で、
    /// ここは伝播ぶりの係数算出に徹する（盤面非依存の plain 引数・実効値パターン）。
    /// </para>
    /// 乱数は持たない（必要なら呼び出し側が roll を渡す）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MoraleContagionRules
    {
        /// <summary>
        /// 伝播強度（-1..1）＝士気変化 sourceMoraleDelta(-1..1) が距離で代数減衰して隣へ届くぶん。
        /// 減衰は 1/(1+distanceFalloff×distance)（Exp 不使用）。距離0で源そのまま・離れるほど薄れる。
        /// </summary>
        public static float ContagionStrength(float sourceMoraleDelta, float distance, MoraleContagionParams p)
        {
            float delta = Mathf.Clamp(sourceMoraleDelta, -1f, 1f);
            float d = Mathf.Max(0f, distance);
            float falloff = 1f / (1f + p.distanceFalloff * d);
            return Mathf.Clamp(delta * falloff, -1f, 1f);
        }

        public static float ContagionStrength(float sourceMoraleDelta, float distance)
            => ContagionStrength(sourceMoraleDelta, distance, MoraleContagionParams.Default);

        /// <summary>
        /// 勝利の高揚（0..victoryScale）＝局所勝利の大きさ localVictoryMagnitude(0..1)×係数。正の士気源。
        /// </summary>
        public static float VictoryElation(float localVictoryMagnitude, MoraleContagionParams p)
        {
            return Mathf.Clamp01(localVictoryMagnitude) * p.victoryScale;
        }

        public static float VictoryElation(float localVictoryMagnitude)
            => VictoryElation(localVictoryMagnitude, MoraleContagionParams.Default);

        /// <summary>
        /// 敗報の動揺（-defeatScale..0）＝局所敗北の大きさ localDefeatMagnitude(0..1)×係数の負値。負の士気源。
        /// </summary>
        public static float DefeatDismay(float localDefeatMagnitude, MoraleContagionParams p)
        {
            return -Mathf.Clamp01(localDefeatMagnitude) * p.defeatScale;
        }

        public static float DefeatDismay(float localDefeatMagnitude)
            => DefeatDismay(localDefeatMagnitude, MoraleContagionParams.Default);

        /// <summary>
        /// 隣接部隊の士気へ伝播ぶんを足した結果（0..1）＝neighborMorale + contagionStrength を士気域へクランプ。
        /// contagion が正なら高揚で上がり、負なら動揺で下がる（両方向）。
        /// </summary>
        public static float Propagate(float neighborMorale, float contagionStrength)
        {
            return Mathf.Clamp01(Mathf.Clamp01(neighborMorale) + Mathf.Clamp(contagionStrength, -1f, 1f));
        }

        /// <summary>
        /// 結束による抵抗後の伝播（-1..1）＝固い部隊は<b>動揺（負）</b>の伝染を弱める。
        /// 負の contagion は (1−unitCohesion×cohesionDampen) 倍に減衰、正（高揚）はそのまま通す
        /// ＝結束は崩れを防ぐが浮かれは止めない。
        /// </summary>
        public static float CohesionResistance(float unitCohesion, float contagion, MoraleContagionParams p)
        {
            float c = Mathf.Clamp(contagion, -1f, 1f);
            if (c >= 0f) return c;
            float damp = 1f - Mathf.Clamp01(unitCohesion) * p.cohesionDampen;
            return Mathf.Clamp(c * damp, -1f, 1f);
        }

        public static float CohesionResistance(float unitCohesion, float contagion)
            => CohesionResistance(unitCohesion, contagion, MoraleContagionParams.Default);

        /// <summary>
        /// 指揮官の威信による調整後の伝播（-1..1）＝威信ある指揮官は<b>高揚（正）を増幅</b>し
        /// <b>動揺（負）を抑える</b>。正は (1+commanderPrestige×prestigeAmplify) 倍、
        /// 負は (1−commanderPrestige×prestigeAmplify) 倍。
        /// </summary>
        public static float CommanderAmplification(float commanderPrestige, float contagion, MoraleContagionParams p)
        {
            float c = Mathf.Clamp(contagion, -1f, 1f);
            float prestige = Mathf.Clamp01(commanderPrestige);
            if (c >= 0f)
                return Mathf.Clamp(c * (1f + prestige * p.prestigeAmplify), -1f, 1f);
            return Mathf.Clamp(c * (1f - prestige * p.prestigeAmplify), -1f, 1f);
        }

        public static float CommanderAmplification(float commanderPrestige, float contagion)
            => CommanderAmplification(commanderPrestige, contagion, MoraleContagionParams.Default);

        /// <summary>
        /// 震源から同心円状に広がる伝播の波（-1..1）＝震源の士気変化 epicenterDelta(-1..1) が
        /// 環の距離 ringDistance で減衰して届く強さ（<see cref="ContagionStrength"/> と同じ代数減衰）。
        /// 外側の環ほど薄い波が届く。
        /// </summary>
        public static float WaveSpread(float epicenterDelta, float ringDistance, MoraleContagionParams p)
        {
            return ContagionStrength(epicenterDelta, ringDistance, p);
        }

        public static float WaveSpread(float epicenterDelta, float ringDistance)
            => WaveSpread(epicenterDelta, ringDistance, MoraleContagionParams.Default);

        /// <summary>
        /// 伝播が無視できない大きさか＝伝播の絶対値が閾値 threshold(0..1) を超える（正負どちらの方向でも）。
        /// 薄れて消えた波は伝染として扱わない。
        /// </summary>
        public static bool IsContagionSignificant(float contagionStrength, float threshold)
        {
            return Mathf.Abs(Mathf.Clamp(contagionStrength, -1f, 1f)) > Mathf.Clamp01(threshold);
        }
    }
}
