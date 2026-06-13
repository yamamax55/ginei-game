using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 斬首戦法の調整値（#斬首戦法）。護衛突破の効き・旗艦の固さ・指揮麻痺の強さ・孤立リスクの係数。
    /// すべて 0 以上にクランプして保持（ctor で正規化）。
    /// </summary>
    public readonly struct DecapitationStrikeParams
    {
        /// <summary>護衛突破の効き（突撃戦力/護衛戦力 に対する指数。小さいほど護衛を貫きにくい）。</summary>
        public readonly float penetrationExponent;
        /// <summary>旗艦の固さ（被打撃の分母。大きいほど旗艦が固く討ちにくい）。</summary>
        public readonly float flagshipToughness;
        /// <summary>指揮集権が麻痺に効く強さ（集権的なほど中枢喪失で全軍が崩れる）。</summary>
        public readonly float paralysisStrength;
        /// <summary>突撃精鋭の孤立リスク係数（護衛戦力に対して突撃が小さいほど孤立して損耗）。</summary>
        public readonly float riskStrength;

        public DecapitationStrikeParams(float penetrationExponent, float flagshipToughness,
            float paralysisStrength, float riskStrength)
        {
            this.penetrationExponent = Mathf.Max(0f, penetrationExponent);
            this.flagshipToughness = Mathf.Max(0.0001f, flagshipToughness);
            this.paralysisStrength = Mathf.Max(0f, paralysisStrength);
            this.riskStrength = Mathf.Max(0f, riskStrength);
        }

        /// <summary>既定：突破指数0.5（平方根で穏やか）・旗艦の固さ100・麻痺強さ1.0・孤立リスク1.0。</summary>
        public static DecapitationStrikeParams Default => new DecapitationStrikeParams(
            DefaultPenetrationExponent, DefaultFlagshipToughness, DefaultParalysisStrength, DefaultRiskStrength);

        public const float DefaultPenetrationExponent = 0.5f;
        public const float DefaultFlagshipToughness = 100f;
        public const float DefaultParalysisStrength = 1.0f;
        public const float DefaultRiskStrength = 1.0f;
    }

    /// <summary>
    /// 斬首戦法（敵旗艦＝指揮中枢を狙い撃つ）の純ロジック（#斬首戦法・test-first・盤面非依存）。
    /// 敵の指揮中枢（旗艦・総旗艦）を集中攻撃して討つと指揮系統が麻痺し全軍が崩れる。
    /// だが旗艦は固く護衛も厚い＝護衛を貫いて初めて旗艦に届く＝ハイリスク・ハイリターン。
    /// 突撃した精鋭は護衛の中で孤立し損耗しうる（リスク）。集権的な敵ほど麻痺が大きいが、
    /// 次席継承・分権で麻痺は緩和される（連続性）。
    /// <b>分担</b>：<see cref="BattlefieldCommandRules"/>（戦死した指揮官の臨時継承＝守り手の人事）とは別＝
    /// こちらは旗艦を討つ「攻め手」のモデル。<see cref="ZoneOfControl"/>/<see cref="LanchesterRules"/>（火力集中）
    /// とも別。指揮中枢喪失で全軍が動揺する士気的な衝撃（MoraleShock 系）とは別＝こちらは指揮系統の麻痺
    /// （命令が降りない＝統制喪失）を表す。実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版。
    /// </summary>
    public static class DecapitationStrikeRules
    {
        // ── 護衛突破 ──────────────────────────────────────────────

        /// <summary>既定パラメータで護衛突破度を返す。</summary>
        public static float EscortPenetration(float strikeForce, float escortStrength)
            => EscortPenetration(strikeForce, escortStrength, DecapitationStrikeParams.Default);

        /// <summary>
        /// 突撃戦力が護衛を貫いて旗艦に届く度合い（0..1）。
        /// `pen = ratio / (ratio + 1)`（ただし ratio = pow(突撃/護衛, exponent)）。
        /// 護衛0＝必ず貫通(1)、突撃0＝届かない(0)。突撃が護衛と互角(ratio=1)で 0.5。
        /// </summary>
        public static float EscortPenetration(float strikeForce, float escortStrength, DecapitationStrikeParams p)
        {
            float strike = Mathf.Max(0f, strikeForce);
            float escort = Mathf.Max(0f, escortStrength);

            if (strike <= 0f) return 0f;        // 突撃なし＝届かない
            if (escort <= 0f) return 1f;        // 護衛皆無＝完全貫通

            float ratio = Mathf.Pow(strike / escort, p.penetrationExponent);
            return ratio / (ratio + 1f);
        }

        // ── 旗艦の被狙い度 ────────────────────────────────────────

        /// <summary>
        /// 護衛を抜いた後の旗艦の被狙い度（0..1）。護衛突破度をそのまま露出度として写す
        /// （護衛を貫くほど旗艦が裸になる）。基準非破壊の純関数。
        /// </summary>
        public static float FlagshipExposure(float escortPenetration)
            => Mathf.Clamp01(escortPenetration);

        // ── 旗艦への打撃 ──────────────────────────────────────────

        /// <summary>既定パラメータで旗艦への打撃を返す。</summary>
        public static float StrikeDamage(float strikeForce, float flagshipDefense, float exposure)
            => StrikeDamage(strikeForce, flagshipDefense, exposure, DecapitationStrikeParams.Default);

        /// <summary>
        /// 露出した旗艦への打撃（0..1 の正規化ダメージ）。
        /// `dmg = exposure * 突撃 / (突撃 + 旗艦の固さ*(1+防御))`。
        /// 露出0＝打撃0、旗艦防御が高い/固いほど打撃は小さい。突撃が大きいほど 1 に近づく。
        /// </summary>
        public static float StrikeDamage(float strikeForce, float flagshipDefense, float exposure, DecapitationStrikeParams p)
        {
            float strike = Mathf.Max(0f, strikeForce);
            float exp = Mathf.Clamp01(exposure);
            if (strike <= 0f || exp <= 0f) return 0f;

            float defense = Mathf.Max(0f, flagshipDefense);
            float denom = strike + p.flagshipToughness * (1f + defense);
            return exp * (strike / denom);
        }

        // ── 指揮麻痺 ──────────────────────────────────────────────

        /// <summary>既定パラメータで指揮麻痺度を返す。</summary>
        public static float CommandParalysis(float flagshipDamage, float commandCentralization)
            => CommandParalysis(flagshipDamage, commandCentralization, DecapitationStrikeParams.Default);

        /// <summary>
        /// 指揮中枢喪失で全軍が麻痺する度合い（0..1）。集権的（commandCentralization 高）なほど大きい。
        /// `paralysis = clamp01(旗艦打撃 * (1 + paralysisStrength*集権度))` を 0..1 で頭打ち。
        /// 旗艦打撃0＝麻痺0、分権的（集権度0）でも打撃ぶんは麻痺する。
        /// </summary>
        public static float CommandParalysis(float flagshipDamage, float commandCentralization, DecapitationStrikeParams p)
        {
            float dmg = Mathf.Clamp01(flagshipDamage);
            float central = Mathf.Clamp01(commandCentralization);
            float raw = dmg * (1f + p.paralysisStrength * central);
            return Mathf.Clamp01(raw);
        }

        // ── 指揮系統の冗長性（緩和） ──────────────────────────────

        /// <summary>
        /// 次席継承・分権による指揮系統の頑健さ（0..1）。麻痺を緩和する側。
        /// `resilience = clamp01((次席の質*0.5 + 分権度*0.5))`。
        /// 有能な次席と分権体制が揃うほど 1 に近づき、麻痺を打ち消す。
        /// </summary>
        public static float ChainOfCommandResilience(float successorQuality, float decentralization)
        {
            float succ = Mathf.Clamp01(successorQuality);
            float dec = Mathf.Clamp01(decentralization);
            return Mathf.Clamp01(succ * 0.5f + dec * 0.5f);
        }

        // ── 突撃精鋭の孤立リスク ──────────────────────────────────

        /// <summary>既定パラメータで突撃精鋭の孤立リスクを返す。</summary>
        public static float StrikeForceRisk(float strikeForce, float escortStrength)
            => StrikeForceRisk(strikeForce, escortStrength, DecapitationStrikeParams.Default);

        /// <summary>
        /// 突撃した精鋭が護衛の中で孤立し損耗するリスク（0..1）。
        /// 護衛が突撃に対して厚いほど高い：`risk = clamp01(riskStrength * 護衛/(護衛+突撃))`。
        /// 突撃なし＝リスク0扱い（突撃していない）、護衛皆無＝リスク0。
        /// </summary>
        public static float StrikeForceRisk(float strikeForce, float escortStrength, DecapitationStrikeParams p)
        {
            float strike = Mathf.Max(0f, strikeForce);
            float escort = Mathf.Max(0f, escortStrength);
            if (strike <= 0f) return 0f;        // 突撃していない
            if (escort <= 0f) return 0f;        // 守りが無い＝孤立しない

            float exposureToEscort = escort / (escort + strike);
            return Mathf.Clamp01(p.riskStrength * exposureToEscort);
        }

        // ── 正味価値 ──────────────────────────────────────────────

        /// <summary>
        /// 斬首の正味価値（-1..1）。指揮麻痺の利益から突撃精鋭の孤立リスクを差し引く。
        /// `value = clamp(指揮麻痺 - 孤立リスク, -1, 1)`。麻痺がリスクを上回れば正＝斬首が有利。
        /// </summary>
        public static float DecapitationValue(float commandParalysis, float strikeForceRisk)
        {
            float gain = Mathf.Clamp01(commandParalysis);
            float risk = Mathf.Clamp01(strikeForceRisk);
            return Mathf.Clamp(gain - risk, -1f, 1f);
        }

        // ── 討ったか ──────────────────────────────────────────────

        /// <summary>
        /// 指揮中枢を討ったか（旗艦打撃が閾値以上）。閾値は 0..1 にクランプ。
        /// </summary>
        public static bool IsCommandDecapitated(float flagshipDamage, float threshold)
        {
            float dmg = Mathf.Clamp01(flagshipDamage);
            float th = Mathf.Clamp01(threshold);
            return dmg >= th;
        }
    }
}
