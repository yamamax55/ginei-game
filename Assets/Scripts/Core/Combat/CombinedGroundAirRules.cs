using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 航空地上協同（近接航空支援・制空権）の調整係数。マジックナンバー禁止＝ここに集約。
    /// </summary>
    public readonly struct CombinedGroundAirParams
    {
        /// <summary>制空権が出撃頻度を介して近接航空支援へ寄与する重み。</summary>
        public readonly float sortieWeight;
        /// <summary>近接航空支援の威力上限（制空×出撃頻度が満点でこの値）。</summary>
        public readonly float casCeiling;
        /// <summary>前線統制官（FAC）が地空連携へ寄与する重み（通信品質との混合）。</summary>
        public readonly float controllerWeight;
        /// <summary>近接支援の実効化で連携が掛かる重み（連携が低いと誤爆/不発で実効が落ちる）。</summary>
        public readonly float coordinationWeight;
        /// <summary>敵防空による航空機損耗の最大率（制空0・防空満点でこの損耗）。</summary>
        public readonly float attritionScale;
        /// <summary>悪天候の影響上限（全天候能力0・悪天候満点でこの影響）。</summary>
        public readonly float weatherScale;
        /// <summary>航空阻止が敵補給線へ与える効果の上限（制空×補給線露出が満点でこの値）。</summary>
        public readonly float interdictionScale;
        /// <summary>近接支援が地上優位へ寄与する重み（航空阻止との混合）。</summary>
        public readonly float casAdvantageWeight;

        public CombinedGroundAirParams(
            float sortieWeight,
            float casCeiling,
            float controllerWeight,
            float coordinationWeight,
            float attritionScale,
            float weatherScale,
            float interdictionScale,
            float casAdvantageWeight)
        {
            this.sortieWeight = Mathf.Clamp01(sortieWeight);
            this.casCeiling = Mathf.Clamp(casCeiling, 0.1f, 1f);
            this.controllerWeight = Mathf.Clamp01(controllerWeight);
            this.coordinationWeight = Mathf.Clamp01(coordinationWeight);
            this.attritionScale = Mathf.Clamp01(attritionScale);
            this.weatherScale = Mathf.Clamp01(weatherScale);
            this.interdictionScale = Mathf.Clamp01(interdictionScale);
            this.casAdvantageWeight = Mathf.Clamp01(casAdvantageWeight);
        }

        /// <summary>既定の調整係数。</summary>
        public static CombinedGroundAirParams Default => new CombinedGroundAirParams(
            sortieWeight: 0.5f,
            casCeiling: 1f,
            controllerWeight: 0.5f,
            coordinationWeight: 0.5f,
            attritionScale: 0.6f,
            weatherScale: 0.8f,
            interdictionScale: 0.7f,
            casAdvantageWeight: 0.5f);
    }

    /// <summary>
    /// 航空地上協同＝近接航空支援（CAS）・制空権と地上部隊の協同の純ロジック（test-first・盤面非依存・唯一の窓口）。
    /// 制空権を取り（AirSuperiority）、出撃頻度で近接支援を出し（CloseAirSupport）、前線統制官と通信で
    /// 地空連携を確立し（GroundAirCoordination）、その実効（CasEffectiveness）で地上部隊を後押しする。
    /// 敵防空は航空機を損耗させ（AntiAirAttrition）、悪天候は支援を阻む（WeatherImpact）。制空は敵補給線を
    /// 断ち（InterdictionEffect）、これらが合わさって航空優勢による地上優位（CombinedArmsAdvantage）を生む。
    /// <para>分担（混同しない）：<see cref="CombinedArmsRules"/> は<b>戦闘艦種の組合せ相乗</b>を扱い、本窓口は
    /// <b>航空（CAS/制空）と地上の協同</b>を扱う＝別レイヤー。<see cref="GroundInvasionRules"/> は<b>地上侵攻の進捗</b>を
    /// 扱い、本窓口は<b>地上戦への航空寄与</b>を扱う。</para>
    /// 入力は全て 0..1 へクランプ。実効値パターン（基準は変えず倍率/寄与で効かせる）。決定論（乱数なし）。
    /// </summary>
    public static class CombinedGroundAirRules
    {
        /// <summary>
        /// 制空権（0..1）。航空戦力/(航空戦力+敵防空)＝双方0なら拮抗 0.5。多いほど制空に近づく。
        /// </summary>
        public static float AirSuperiority(float airForceStrength, float enemyAirDefense, CombinedGroundAirParams p)
        {
            float air = Mathf.Max(0f, airForceStrength);
            float def = Mathf.Max(0f, enemyAirDefense);
            float sum = air + def;
            if (sum <= 0f) return 0.5f;
            return Mathf.Clamp01(air / sum);
        }

        /// <summary>既定 Params 版。</summary>
        public static float AirSuperiority(float airForceStrength, float enemyAirDefense)
            => AirSuperiority(airForceStrength, enemyAirDefense, CombinedGroundAirParams.Default);

        /// <summary>
        /// 近接航空支援（0..casCeiling）。制空権×出撃頻度（出撃頻度は sortieWeight で混合し制空が主因）。
        /// </summary>
        public static float CloseAirSupport(float airSuperiority, float sortieRate, CombinedGroundAirParams p)
        {
            float air = Mathf.Clamp01(airSuperiority);
            float sortie = Mathf.Clamp01(sortieRate);
            // 制空が前提で、出撃頻度が高いほど押し上がる（出撃0でも制空ぶんは出る）。
            float drive = air * Mathf.Lerp(1f - p.sortieWeight, 1f, sortie);
            return Mathf.Clamp(drive * p.casCeiling, 0f, p.casCeiling);
        }

        /// <summary>既定 Params 版。</summary>
        public static float CloseAirSupport(float airSuperiority, float sortieRate)
            => CloseAirSupport(airSuperiority, sortieRate, CombinedGroundAirParams.Default);

        /// <summary>
        /// 地空連携（0..1）。通信品質×前線統制官（FAC）。FAC は controllerWeight で混合（FAC無しでも通信ぶんは効く）。
        /// </summary>
        public static float GroundAirCoordination(float communicationQuality, float forwardControllers, CombinedGroundAirParams p)
        {
            float comm = Mathf.Clamp01(communicationQuality);
            float fac = Mathf.Clamp01(forwardControllers);
            return Mathf.Clamp01(comm * Mathf.Lerp(1f - p.controllerWeight, 1f, fac));
        }

        /// <summary>既定 Params 版。</summary>
        public static float GroundAirCoordination(float communicationQuality, float forwardControllers)
            => GroundAirCoordination(communicationQuality, forwardControllers, CombinedGroundAirParams.Default);

        /// <summary>
        /// 近接支援の実効（0..1）。近接航空支援×地空連携（連携が低いと誤爆/不発で実効が落ちる）。
        /// 連携は coordinationWeight で混合（連携0でも一定は通る）。
        /// </summary>
        public static float CasEffectiveness(float closeAirSupport, float groundAirCoordination, CombinedGroundAirParams p)
        {
            float cas = Mathf.Clamp01(closeAirSupport);
            float coord = Mathf.Clamp01(groundAirCoordination);
            return Mathf.Clamp01(cas * Mathf.Lerp(1f - p.coordinationWeight, 1f, coord));
        }

        /// <summary>既定 Params 版。</summary>
        public static float CasEffectiveness(float closeAirSupport, float groundAirCoordination)
            => CasEffectiveness(closeAirSupport, groundAirCoordination, CombinedGroundAirParams.Default);

        /// <summary>
        /// 航空機の損耗（0..attritionScale）。敵防空×(1-制空)＝制空を取れば防空圏を抑え損耗が下がる。
        /// </summary>
        public static float AntiAirAttrition(float enemyAirDefense, float airSuperiority, CombinedGroundAirParams p)
        {
            float def = Mathf.Clamp01(enemyAirDefense);
            float air = Mathf.Clamp01(airSuperiority);
            return Mathf.Clamp01(def * (1f - air) * p.attritionScale);
        }

        /// <summary>既定 Params 版。</summary>
        public static float AntiAirAttrition(float enemyAirDefense, float airSuperiority)
            => AntiAirAttrition(enemyAirDefense, airSuperiority, CombinedGroundAirParams.Default);

        /// <summary>
        /// 悪天候の影響（0..weatherScale）。悪天候×(1-全天候能力)＝全天候装備があれば悪天候を打ち消す。
        /// </summary>
        public static float WeatherImpact(float weatherSeverity, float allWeatherCapability, CombinedGroundAirParams p)
        {
            float sev = Mathf.Clamp01(weatherSeverity);
            float cap = Mathf.Clamp01(allWeatherCapability);
            return Mathf.Clamp01(sev * (1f - cap) * p.weatherScale);
        }

        /// <summary>既定 Params 版。</summary>
        public static float WeatherImpact(float weatherSeverity, float allWeatherCapability)
            => WeatherImpact(weatherSeverity, allWeatherCapability, CombinedGroundAirParams.Default);

        /// <summary>
        /// 航空阻止（0..interdictionScale）。制空×敵補給線の露出＝制空下では敵の補給/増援を空爆で断てる。
        /// </summary>
        public static float InterdictionEffect(float airSuperiority, float enemySupplyLines, CombinedGroundAirParams p)
        {
            float air = Mathf.Clamp01(airSuperiority);
            float supply = Mathf.Clamp01(enemySupplyLines);
            return Mathf.Clamp01(air * supply * p.interdictionScale);
        }

        /// <summary>既定 Params 版。</summary>
        public static float InterdictionEffect(float airSuperiority, float enemySupplyLines)
            => InterdictionEffect(airSuperiority, enemySupplyLines, CombinedGroundAirParams.Default);

        /// <summary>
        /// 航空優勢による地上優位（0..1）。近接支援の実効×航空阻止を混合し、悪天候(1-天候)で減衰。
        /// 近接支援が主因（casAdvantageWeight）、阻止が補助。悪天候はその総量を割り引く。
        /// </summary>
        public static float CombinedArmsAdvantage(float casEffectiveness, float interdictionEffect, float weatherImpact, CombinedGroundAirParams p)
        {
            float cas = Mathf.Clamp01(casEffectiveness);
            float inter = Mathf.Clamp01(interdictionEffect);
            float weather = Mathf.Clamp01(weatherImpact);
            // 近接支援と阻止の重み付き和（合計1へ正規化）。
            float w = Mathf.Clamp01(p.casAdvantageWeight);
            float air = cas * w + inter * (1f - w);
            return Mathf.Clamp01(air * (1f - weather));
        }

        /// <summary>既定 Params 版。</summary>
        public static float CombinedArmsAdvantage(float casEffectiveness, float interdictionEffect, float weatherImpact)
            => CombinedArmsAdvantage(casEffectiveness, interdictionEffect, weatherImpact, CombinedGroundAirParams.Default);
    }
}
