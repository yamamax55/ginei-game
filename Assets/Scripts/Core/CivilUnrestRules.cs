using UnityEngine;

namespace Ginei
{
    /// <summary>市民不安（社会の不穏度と治安悪化）の調整係数。生活苦・抑圧・不公正の重み、伝播・対応・激化のスケール。</summary>
    public readonly struct CivilUnrestParams
    {
        /// <summary>不満に占める生活苦の重み（0..1・抑圧/不公正と合算して正規化）。</summary>
        public readonly float hardshipWeight;
        /// <summary>不満に占める抑圧の重み（0..1）。</summary>
        public readonly float repressionWeight;
        /// <summary>不満に占める不公正の重み（0..1）。</summary>
        public readonly float injusticeWeight;
        /// <summary>つながり0でも残る伝播の下限比（0..1・社会が分断でも最低限は広がる）。</summary>
        public readonly float spreadFloor;
        /// <summary>当局対応に占める治安力の重み（0..1・残りが譲歩の重み）。</summary>
        public readonly float policingWeight;
        /// <summary>激化スケール（過剰鎮圧1のとき伝播に掛かる火に油の最大倍率0..1）。</summary>
        public readonly float escalationScale;
        /// <summary>安定1のとき許容閾値に加わる上限幅（0..1・安定した社会は不安に強い）。</summary>
        public readonly float stabilityBonus;
        /// <summary>許容閾値の基礎値（-1..1・安定ゼロでもこの水準までは秩序を保つ）。</summary>
        public readonly float baseTolerance;

        public CivilUnrestParams(float hardshipWeight, float repressionWeight, float injusticeWeight,
                                 float spreadFloor, float policingWeight, float escalationScale,
                                 float stabilityBonus, float baseTolerance)
        {
            this.hardshipWeight = Mathf.Clamp01(hardshipWeight);
            this.repressionWeight = Mathf.Clamp01(repressionWeight);
            this.injusticeWeight = Mathf.Clamp01(injusticeWeight);
            this.spreadFloor = Mathf.Clamp01(spreadFloor);
            this.policingWeight = Mathf.Clamp01(policingWeight);
            this.escalationScale = Mathf.Clamp01(escalationScale);
            this.stabilityBonus = Mathf.Clamp01(stabilityBonus);
            this.baseTolerance = Mathf.Clamp(baseTolerance, -1f, 1f);
        }

        /// <summary>既定＝生活苦0.4/抑圧0.3/不公正0.3・伝播下限0.2・治安力0.6・激化0.5・安定加算0.4・基礎閾値-0.1。</summary>
        public static CivilUnrestParams Default =>
            new CivilUnrestParams(0.4f, 0.3f, 0.3f, 0.2f, 0.6f, 0.5f, 0.4f, -0.1f);
    }

    /// <summary>
    /// 市民不安（社会の不穏度と治安悪化）の純ロジック。生活苦・抑圧・不公正が不満を生み、社会的つながりで
    /// 不安が伝播し、当局の治安力と譲歩がそれを抑える一方、過剰な弾圧は火に油を注いで激化させる。
    /// 安定した社会は高い許容閾値を持ち、不安水準がその閾値を超えると秩序が崩れ暴動へ転化する。
    /// <para>
    /// 分担：<c>MoraleContagionRules</c>（戦場の士気伝播）/<c>LawTickRules</c>（治安の犯罪圧力→公共秩序）とは別＝
    /// こちらは<b>社会の市民不安</b>の汎用モデル（盤面非依存の plain 引数・実効値パターン）。乱数は持たない。
    /// </para>
    /// 純ロジック（非 MonoBehaviour・test-first）。入力は全て clamp、符号付き出力は -1..1 にクランプ。
    /// </summary>
    public static class CivilUnrestRules
    {
        /// <summary>
        /// 不満（0..1）＝生活苦・抑圧・不公正の重み付き平均。重みは合計で正規化（全0なら不満0）。
        /// 生活苦が高く抑圧と不公正が重なるほど不満が募る。
        /// </summary>
        public static float Discontent(float economicHardship, float repression, float injustice, CivilUnrestParams p)
        {
            float h = Mathf.Clamp01(economicHardship);
            float r = Mathf.Clamp01(repression);
            float j = Mathf.Clamp01(injustice);
            float wSum = p.hardshipWeight + p.repressionWeight + p.injusticeWeight;
            if (wSum <= 0f) return 0f;
            float weighted = h * p.hardshipWeight + r * p.repressionWeight + j * p.injusticeWeight;
            return Mathf.Clamp01(weighted / wSum);
        }

        public static float Discontent(float economicHardship, float repression, float injustice)
            => Discontent(economicHardship, repression, injustice, CivilUnrestParams.Default);

        /// <summary>
        /// 不安の伝播（0..1）＝不満×社会的つながり。つながり0でも spreadFloor ぶんは広がり、
        /// つながり1で不満がそのまま伝わる（floor + (1−floor)×connectivity の線形）。
        /// </summary>
        public static float UnrestSpread(float discontent, float socialConnectivity, CivilUnrestParams p)
        {
            float d = Mathf.Clamp01(discontent);
            float c = Mathf.Clamp01(socialConnectivity);
            float reach = p.spreadFloor + (1f - p.spreadFloor) * c;
            return Mathf.Clamp01(d * reach);
        }

        public static float UnrestSpread(float discontent, float socialConnectivity)
            => UnrestSpread(discontent, socialConnectivity, CivilUnrestParams.Default);

        /// <summary>
        /// 当局の対応（0..1）＝治安力と譲歩の重み付き平均（policingWeight が治安力・残りが譲歩）。
        /// 力で抑えるか譲歩で和らげるか、両者を合わせた当局の総合的な不安への対処力。
        /// </summary>
        public static float AuthorityResponse(float policingCapacity, float concessions, CivilUnrestParams p)
        {
            float pol = Mathf.Clamp01(policingCapacity);
            float con = Mathf.Clamp01(concessions);
            float w = p.policingWeight;
            return Mathf.Clamp01(pol * w + con * (1f - w));
        }

        public static float AuthorityResponse(float policingCapacity, float concessions)
            => AuthorityResponse(policingCapacity, concessions, CivilUnrestParams.Default);

        /// <summary>
        /// 沈静化（0..1）＝対応/(対応+伝播) の競合比。当局の対応が伝播を上回るほど 1 に近づき
        /// 不安は鎮まる。両者0なら沈静0（鎮める力も広がる勢いも無い）。
        /// </summary>
        public static float Suppression(float authorityResponse, float unrestSpread, CivilUnrestParams p)
        {
            float a = Mathf.Clamp01(authorityResponse);
            float s = Mathf.Clamp01(unrestSpread);
            float denom = a + s;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(a / denom);
        }

        public static float Suppression(float authorityResponse, float unrestSpread)
            => Suppression(authorityResponse, unrestSpread, CivilUnrestParams.Default);

        /// <summary>
        /// 激化（0..1）＝伝播×過剰鎮圧×escalationScale。弾圧が過剰なほど不安に火に油を注ぎ
        /// 伝播ぶんを増幅する＝抑え込もうとして却って燃え広がる。
        /// </summary>
        public static float Escalation(float unrestSpread, float heavyHandedness, CivilUnrestParams p)
        {
            float s = Mathf.Clamp01(unrestSpread);
            float hh = Mathf.Clamp01(heavyHandedness);
            return Mathf.Clamp01(s * hh * p.escalationScale);
        }

        public static float Escalation(float unrestSpread, float heavyHandedness)
            => Escalation(unrestSpread, heavyHandedness, CivilUnrestParams.Default);

        /// <summary>
        /// 許容閾値（-1..1）＝基礎値 + 社会の安定×stabilityBonus。安定した社会ほど高い不安まで
        /// 秩序を保てる（不安に強い）。不安水準がこの閾値を超えると秩序が崩れる基準。
        /// </summary>
        public static float ToleranceThreshold(float socialStability, CivilUnrestParams p)
        {
            float st = Mathf.Clamp01(socialStability);
            return Mathf.Clamp(p.baseTolerance + st * p.stabilityBonus, -1f, 1f);
        }

        public static float ToleranceThreshold(float socialStability)
            => ToleranceThreshold(socialStability, CivilUnrestParams.Default);

        /// <summary>
        /// 不安水準（-1..1）＝伝播 − 沈静 + 激化。負＝秩序の余裕／正＝不穏。沈静が伝播と激化を
        /// 上回れば負（落ち着き）、弾圧の激化を含む不穏が沈静を上回れば正（不穏）へ振れる。
        /// </summary>
        public static float UnrestLevel(float unrestSpread, float suppression, float escalation, CivilUnrestParams p)
        {
            float s = Mathf.Clamp01(unrestSpread);
            float sup = Mathf.Clamp01(suppression);
            float esc = Mathf.Clamp01(escalation);
            return Mathf.Clamp(s - sup + esc, -1f, 1f);
        }

        public static float UnrestLevel(float unrestSpread, float suppression, float escalation)
            => UnrestLevel(unrestSpread, suppression, escalation, CivilUnrestParams.Default);

        /// <summary>
        /// 秩序が保たれるか＝不安水準が許容閾値 + 余裕 threshold を超えていなければ true。
        /// 超えると暴動への転化点（秩序崩壊）。threshold は閾値への上乗せ余白（0..1）。
        /// </summary>
        public static bool IsOrderMaintained(float unrestLevel, float toleranceThreshold, float threshold)
        {
            float u = Mathf.Clamp(unrestLevel, -1f, 1f);
            float t = Mathf.Clamp(toleranceThreshold, -1f, 1f) + Mathf.Clamp01(threshold);
            return u <= t;
        }

        /// <summary>既定の余裕（0）で秩序判定（不安水準が許容閾値を超えたら崩壊）。</summary>
        public static bool IsOrderMaintained(float unrestLevel, float toleranceThreshold)
            => IsOrderMaintained(unrestLevel, toleranceThreshold, 0f);
    }
}
