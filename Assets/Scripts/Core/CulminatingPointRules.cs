using UnityEngine;

namespace Ginei
{
    /// <summary>攻勢終末点の調整係数。</summary>
    public readonly struct CulminatingPointParams
    {
        /// <summary>補給距離による効率低下の急峻さ（距離/補給網到達力 の冪指数・1以上）。遠いほど加速して物資が細る。</summary>
        public readonly float supplyFalloffExponent;
        /// <summary>補給効率の下限（0..1・どれだけ遠くても最低限届く割合＝現地調達など）。</summary>
        public readonly float minSupplyEfficiency;
        /// <summary>損耗が戦力を削る重み（0..1・損耗1あたり実効戦力を何割落とすか）。</summary>
        public readonly float attritionWeight;
        /// <summary>補給効率が戦力に効く重み（0..1・補給細りが実効戦力を何割左右するか）。</summary>
        public readonly float supplyWeight;
        /// <summary>攻勢終末点を引き当てるための基準作戦距離（補給網到達力1のときに戦力比1になる距離スケール）。</summary>
        public readonly float distanceScale;
        /// <summary>終末点超過ペナルティの非線形度（超過距離の冪指数・1以上）。越えるほど加速して危険。</summary>
        public readonly float overreachExponent;
        /// <summary>終末点超過ペナルティの最大値（0..1・補給途絶・各個撃破の上限）。</summary>
        public readonly float maxOverreachPenalty;
        /// <summary>最適停止点の安全マージン（0..1・終末点距離に掛ける＝手前で止まる割合）。</summary>
        public readonly float haltSafetyMargin;

        public CulminatingPointParams(float supplyFalloffExponent, float minSupplyEfficiency,
            float attritionWeight, float supplyWeight, float distanceScale,
            float overreachExponent, float maxOverreachPenalty, float haltSafetyMargin)
        {
            this.supplyFalloffExponent = Mathf.Max(1f, supplyFalloffExponent);
            this.minSupplyEfficiency = Mathf.Clamp01(minSupplyEfficiency);
            this.attritionWeight = Mathf.Clamp01(attritionWeight);
            this.supplyWeight = Mathf.Clamp01(supplyWeight);
            this.distanceScale = Mathf.Max(0.0001f, distanceScale);
            this.overreachExponent = Mathf.Max(1f, overreachExponent);
            this.maxOverreachPenalty = Mathf.Clamp01(maxOverreachPenalty);
            this.haltSafetyMargin = Mathf.Clamp01(haltSafetyMargin);
        }

        /// <summary>既定＝補給減衰冪1.5・補給効率下限0.1・損耗重み0.6・補給重み0.5・距離スケール10・超過冪2・超過上限0.9・停止マージン0.8。</summary>
        public static CulminatingPointParams Default =>
            new CulminatingPointParams(1.5f, 0.1f, 0.6f, 0.5f, 10f, 2f, 0.9f, 0.8f);
    }

    /// <summary>
    /// 攻勢終末点・戦略的過伸張の純ロジック（クラウゼヴィッツ／孫子・#1129）。攻勢は補給距離が
    /// 伸びるほど戦力効率が落ち、ある点（攻勢終末点）で攻撃力が防御を割る＝これ以上進めない限界。
    /// 補給が細る前に止まる者が勝つ＝深追いせず終末点の手前で止まるのが上策。
    /// <see cref="OverextensionRules"/>（版図と国力の比＝国家規模の恒常的な過伸張）とは別系統＝
    /// こちらは1作戦の作戦距離による戦力減衰（伸びきる瞬間）。<see cref="SupplyRules"/>（補給線が
    /// 回廊で繋がるか＝面の到達）とも別＝こちらは距離による効率の逓減そのもの。
    /// <see cref="PursuitRules"/>（追撃＝振り切り・殿軍）とも別＝こちらは進撃側の終末点判定。
    /// 倍率は基準戦力に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CulminatingPointRules
    {
        /// <summary>
        /// 補給効率（minSupplyEfficiency..1）。作戦距離/補給網到達力 の比を冪で非線形に効かせ、
        /// 遠いほど物資が前線に届かない＝効率が落ちる。補給網到達力（0..1）が高いほど遠くまで保つ。
        /// 距離0なら1.0（基地直上は満杯）、無限遠でも下限までしか落ちない（現地調達）。
        /// </summary>
        public static float SupplyEfficiency(float distanceFromBase, float supplyRange, CulminatingPointParams p)
        {
            float d = Mathf.Max(0f, distanceFromBase);
            float range = Mathf.Clamp01(supplyRange);
            if (d <= 0f) return 1f;
            // 補給網到達力が低いほど実効距離が伸びる＝同じ距離でも効率が早く落ちる
            float effectiveReach = Mathf.Lerp(p.distanceScale * 0.25f, p.distanceScale, range);
            float ratio = d / effectiveReach; // 1で到達限界に達する目安
            float falloff = Mathf.Pow(ratio, p.supplyFalloffExponent);
            float eff = 1f / (1f + falloff); // なめらかに 1→0 へ逓減
            return Mathf.Max(p.minSupplyEfficiency, eff);
        }

        public static float SupplyEfficiency(float distanceFromBase, float supplyRange)
            => SupplyEfficiency(distanceFromBase, supplyRange, CulminatingPointParams.Default);

        /// <summary>
        /// 実効戦力倍率（0..1）＝補給細りと損耗で攻撃力が逓減。補給効率が低いほど（1−supplyWeight×(1−eff)）
        /// で削り、ここまでの損耗（0..1）を attritionWeight で削る。基準戦力に掛けて使う（基準非破壊）。
        /// 補給満杯・無損耗なら1.0。
        /// </summary>
        public static float CombatPowerFactor(float supplyEfficiency, float attritionSoFar, CulminatingPointParams p)
        {
            float eff = Mathf.Clamp01(supplyEfficiency);
            float attr = Mathf.Clamp01(attritionSoFar);
            float supplyFactor = 1f - p.supplyWeight * (1f - eff); // 補給細りで攻撃力↓
            float attritionFactor = 1f - p.attritionWeight * attr;  // 損耗で攻撃力↓
            return Mathf.Clamp01(supplyFactor * attritionFactor);
        }

        public static float CombatPowerFactor(float supplyEfficiency, float attritionSoFar)
            => CombatPowerFactor(supplyEfficiency, attritionSoFar, CulminatingPointParams.Default);

        /// <summary>
        /// 攻勢終末点の距離（0以上）＝攻撃側の実効戦力が防御側を下回る作戦距離。距離が伸びるほど
        /// 補給効率が落ちて実効戦力が減り、防御側戦力を割った地点がここ。これ以上は危険＝越えるな。
        /// 初期戦力が防御側以下なら0（最初から劣勢＝攻勢成立せず）、補給網が広いほど終末点は遠い。
        /// </summary>
        public static float CulminatingDistance(float initialStrength, float defenderStrength, float supplyRange)
            => CulminatingDistance(initialStrength, defenderStrength, supplyRange, CulminatingPointParams.Default);

        public static float CulminatingDistance(float initialStrength, float defenderStrength, float supplyRange,
            CulminatingPointParams p)
        {
            float init = Mathf.Max(0f, initialStrength);
            float def = Mathf.Max(0f, defenderStrength);
            if (init <= def) return 0f;            // 最初から防御を上回れない＝攻勢が成立しない
            if (def <= 0f) return float.PositiveInfinity; // 守る相手がいなければ終末点なし
            // SupplyEfficiency(d)=def/init となる距離を補給効率の逆関数から解く
            float targetEff = def / init;          // ここまで効率が落ちると戦力比が逆転
            // eff = 1/(1+falloff) → falloff = (1/eff)-1、falloff = (d/reach)^exp
            float falloff = (1f / targetEff) - 1f;
            float ratio = Mathf.Pow(Mathf.Max(0f, falloff), 1f / p.supplyFalloffExponent);
            float range = Mathf.Clamp01(supplyRange);
            float effectiveReach = Mathf.Lerp(p.distanceScale * 0.25f, p.distanceScale, range);
            return ratio * effectiveReach;
        }

        /// <summary>
        /// 終末点を越えたか＝攻撃側の実効攻撃力（基準戦力×実効戦力倍率）が防御側を下回ったら true。
        /// 攻撃力が防御を割った＝反撃に転じられる危険＝これ以上の進撃は身を滅ぼす。
        /// </summary>
        public static bool IsPastCulmination(float combatPowerFactor, float defenderStrength, float attackerNominal)
        {
            float factor = Mathf.Clamp01(combatPowerFactor);
            float def = Mathf.Max(0f, defenderStrength);
            float nominal = Mathf.Max(0f, attackerNominal);
            float effectiveAttack = nominal * factor;
            return effectiveAttack < def;
        }

        /// <summary>
        /// 終末点超過のペナルティ（0..maxOverreachPenalty）。終末点を越えた距離を冪で非線形に効かせる＝
        /// 限界を越えた進撃ほど補給途絶・各個撃破のリスクが加速。超過0以下なら0（まだ手前）。
        /// 係数に（1−これ）を掛けて使う。
        /// </summary>
        public static float OverreachPenalty(float distanceBeyondCulmination, CulminatingPointParams p)
        {
            float beyond = Mathf.Max(0f, distanceBeyondCulmination);
            if (beyond <= 0f) return 0f;
            // 距離スケールで正規化してから冪＝終末点をどれだけ越えたかの相対量で効かせる
            float norm = beyond / p.distanceScale;
            float raw = Mathf.Pow(norm, p.overreachExponent);
            return Mathf.Min(p.maxOverreachPenalty, raw);
        }

        public static float OverreachPenalty(float distanceBeyondCulmination)
            => OverreachPenalty(distanceBeyondCulmination, CulminatingPointParams.Default);

        /// <summary>
        /// 攻勢を止めるべき最適地点（0以上）＝攻勢終末点の手前（×haltSafetyMargin）で止める＝深追いしない。
        /// 終末点に達する前に攻勢を切り上げる者が勝つ（補給が細る前に止まる）。終末点が無限なら無限を返す。
        /// </summary>
        public static float OptimalHaltPoint(float supplyRange, float defenderStrength,
            float initialStrength, CulminatingPointParams p)
        {
            float culm = CulminatingDistance(initialStrength, defenderStrength, supplyRange, p);
            if (float.IsPositiveInfinity(culm)) return float.PositiveInfinity;
            return culm * p.haltSafetyMargin;
        }

        /// <summary>
        /// 攻勢を止めるべき最適地点（既定の攻撃側戦力＝防御側の2倍を仮定した簡易版）。
        /// 守るべき相手より優勢な攻勢が、終末点の手前のどこで止まるべきかを返す。
        /// </summary>
        public static float OptimalHaltPoint(float supplyRange, float defenderStrength)
        {
            var p = CulminatingPointParams.Default;
            float def = Mathf.Max(0f, defenderStrength);
            // 攻勢が成立する標準的な優勢（防御側の2倍）を仮定して終末点の手前を返す
            float assumedInitial = def * 2f;
            return OptimalHaltPoint(supplyRange, def, assumedInitial, p);
        }
    }
}
