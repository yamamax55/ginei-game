using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 星雲戦パラメータ（ガス・塵の濃い宙域での会戦の係数）。基準値非破壊・全項目 Clamp。
    /// </summary>
    public readonly struct NebulaCombatParams
    {
        /// <summary>センサー減衰→隠密有利への係数。</summary>
        public readonly float stealthImpact;
        /// <summary>ビーム拡散減衰の基礎係数。</summary>
        public readonly float dispersionImpact;
        /// <summary>濃度→交戦距離縮小の係数。</summary>
        public readonly float rangeReductionImpact;
        /// <summary>通信障害→指揮低下への係数。</summary>
        public readonly float commandImpact;
        /// <summary>隠密×近接→伏兵好機の係数。</summary>
        public readonly float ambushImpact;
        /// <summary>星雲を自軍有利と判定する既定しきい値（隠密の利が指揮低下を上回る差）。</summary>
        public readonly float favorableThreshold;

        public NebulaCombatParams(
            float stealthImpact,
            float dispersionImpact,
            float rangeReductionImpact,
            float commandImpact,
            float ambushImpact,
            float favorableThreshold)
        {
            this.stealthImpact = Mathf.Clamp01(stealthImpact);
            this.dispersionImpact = Mathf.Clamp01(dispersionImpact);
            this.rangeReductionImpact = Mathf.Clamp01(rangeReductionImpact);
            this.commandImpact = Mathf.Clamp01(commandImpact);
            this.ambushImpact = Mathf.Clamp01(ambushImpact);
            this.favorableThreshold = Mathf.Clamp01(favorableThreshold);
        }

        public static NebulaCombatParams Default =>
            new NebulaCombatParams(
                stealthImpact: 0.8f,
                dispersionImpact: 0.6f,
                rangeReductionImpact: 0.7f,
                commandImpact: 0.8f,
                ambushImpact: 0.9f,
                favorableThreshold: 0.0f);
    }

    /// <summary>
    /// 星雲戦（ガス・塵の濃い宙域内の会戦）の純ロジック（盤面非依存・plain引数）。
    /// 濃い星雲はセンサーを減衰させ（視界不良）、ビームを拡散させ、通信を妨げて指揮を落とすが、
    /// 同時に隠密接近と近接戦・伏兵の好機を生む。各効果は星雲濃度でスケールする。
    /// 分担：<see cref="NebulaCombatRules"/> は「ガス星雲という環境そのものが
    /// センサー/ビーム/通信/交戦距離へ効く特殊環境レイヤー」。
    /// 視界制限戦闘の解像度低下を扱う NightBattleRules（夜戦）とは別物
    /// （あちらは暗黒・密度依存の同士討ち、こちらはガス減衰・拡散・通信障害）。
    /// 実効値パターン（基準値を破壊せず減衰/有利を返す）・乱数なし。
    /// </summary>
    public static class NebulaCombatRules
    {
        /// <summary>
        /// センサー減衰度（0..1）。星雲が濃いほど見えず、センサー出力が高いほど見通す。
        /// = nebulaDensity ÷（nebulaDensity ＋ sensorPower）。出力0なら減衰なし。
        /// </summary>
        public static float SensorAttenuation(float nebulaDensity, float sensorPower, NebulaCombatParams p)
        {
            float d = Mathf.Clamp01(nebulaDensity);
            float s = Mathf.Max(0f, sensorPower);
            float denom = d + s;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(d / denom);
        }

        public static float SensorAttenuation(float nebulaDensity, float sensorPower) =>
            SensorAttenuation(nebulaDensity, sensorPower, NebulaCombatParams.Default);

        /// <summary>
        /// ビームの拡散減衰（0..1）。濃いガス中を遠くまで撃つほど拡散して威力が落ちる。
        /// = nebulaDensity × range × dispersionImpact（range は 0..1 正規化距離）。
        /// </summary>
        public static float BeamDispersion(float nebulaDensity, float range, NebulaCombatParams p)
        {
            float d = Mathf.Clamp01(nebulaDensity);
            float r = Mathf.Clamp01(range);
            return Mathf.Clamp01(d * r * p.dispersionImpact);
        }

        public static float BeamDispersion(float nebulaDensity, float range) =>
            BeamDispersion(nebulaDensity, range, NebulaCombatParams.Default);

        /// <summary>
        /// 隠密接近の有利（0..1）。敵センサーが減衰しているほど気付かれず近づける。
        /// = sensorAttenuation × stealthImpact。
        /// </summary>
        public static float StealthBonus(float sensorAttenuation, NebulaCombatParams p)
        {
            float a = Mathf.Clamp01(sensorAttenuation);
            return Mathf.Clamp01(a * p.stealthImpact);
        }

        public static float StealthBonus(float sensorAttenuation) =>
            StealthBonus(sensorAttenuation, NebulaCombatParams.Default);

        /// <summary>
        /// 交戦距離の縮小度（0..1）。濃い星雲では遠距離で捉えられず近接戦を強いられる。
        /// = nebulaDensity × rangeReductionImpact。
        /// </summary>
        public static float EngagementRangeReduction(float nebulaDensity, NebulaCombatParams p)
        {
            float d = Mathf.Clamp01(nebulaDensity);
            return Mathf.Clamp01(d * p.rangeReductionImpact);
        }

        public static float EngagementRangeReduction(float nebulaDensity) =>
            EngagementRangeReduction(nebulaDensity, NebulaCombatParams.Default);

        /// <summary>
        /// 通信障害度（0..1）。星雲が濃いほど通信が乱れ、信号強度が高いほど貫通する。
        /// = nebulaDensity ÷（nebulaDensity ＋ signalStrength）。信号0なら影響なし。
        /// </summary>
        public static float CommunicationDegradation(float nebulaDensity, float signalStrength, NebulaCombatParams p)
        {
            float d = Mathf.Clamp01(nebulaDensity);
            float sig = Mathf.Max(0f, signalStrength);
            float denom = d + sig;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(d / denom);
        }

        public static float CommunicationDegradation(float nebulaDensity, float signalStrength) =>
            CommunicationDegradation(nebulaDensity, signalStrength, NebulaCombatParams.Default);

        /// <summary>
        /// 指揮低下度（0..1）。通信が乱れるほど艦隊統制が効かなくなる。
        /// = communicationDegradation × commandImpact。
        /// </summary>
        public static float CommandPenalty(float communicationDegradation, NebulaCombatParams p)
        {
            float c = Mathf.Clamp01(communicationDegradation);
            return Mathf.Clamp01(c * p.commandImpact);
        }

        public static float CommandPenalty(float communicationDegradation) =>
            CommandPenalty(communicationDegradation, NebulaCombatParams.Default);

        /// <summary>
        /// 伏兵の好機（0..1）。隠密接近と近接戦の誘発が噛み合うほど待ち伏せが刺さる。
        /// = stealthBonus × engagementRangeReduction × ambushImpact。
        /// </summary>
        public static float AmbushOpportunity(float stealthBonus, float engagementRangeReduction, NebulaCombatParams p)
        {
            float st = Mathf.Clamp01(stealthBonus);
            float rr = Mathf.Clamp01(engagementRangeReduction);
            return Mathf.Clamp01(st * rr * p.ambushImpact);
        }

        public static float AmbushOpportunity(float stealthBonus, float engagementRangeReduction) =>
            AmbushOpportunity(stealthBonus, engagementRangeReduction, NebulaCombatParams.Default);

        /// <summary>
        /// 星雲が自軍に有利か。隠密接近の利が通信障害による指揮低下を上回れば有利と判定。
        /// = (stealthBonus − commandPenalty) ＞ threshold。
        /// </summary>
        public static bool IsNebulaFavorable(float stealthBonus, float commandPenalty, float threshold)
        {
            float st = Mathf.Clamp01(stealthBonus);
            float cp = Mathf.Clamp01(commandPenalty);
            float t = Mathf.Clamp01(threshold);
            return (st - cp) > t;
        }

        public static bool IsNebulaFavorable(float stealthBonus, float commandPenalty) =>
            IsNebulaFavorable(stealthBonus, commandPenalty, NebulaCombatParams.Default.favorableThreshold);
    }
}
