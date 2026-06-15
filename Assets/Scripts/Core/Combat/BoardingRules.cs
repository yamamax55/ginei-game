using UnityEngine;

namespace Ginei
{
    /// <summary>白兵戦（移乗攻撃）の結末。</summary>
    public enum BoardingOutcome
    {
        撃退,   // 防御側が移乗部隊を退けた
        制圧,   // 攻撃側が艦を奪取した
        膠着    // 決着つかず＝艦内で戦闘継続（再判定へ）
    }

    /// <summary>白兵戦の調整係数（ローゼンリッター型の装甲擲弾兵による移乗攻撃）。</summary>
    public readonly struct BoardingParams
    {
        /// <summary>制圧が成立する戦力比（攻撃側/防御側）の閾値。</summary>
        public readonly float captureRatio;
        /// <summary>撃退が成立する戦力比の閾値（これ未満で攻撃側が押し返される）。</summary>
        public readonly float repelRatio;
        /// <summary>1回の白兵戦で双方が失う兵力の基礎割合。</summary>
        public readonly float casualtyRate;

        public BoardingParams(float captureRatio, float repelRatio, float casualtyRate)
        {
            this.repelRatio = Mathf.Max(0.01f, repelRatio);
            this.captureRatio = Mathf.Max(this.repelRatio, captureRatio);
            this.casualtyRate = Mathf.Clamp01(casualtyRate);
        }

        /// <summary>既定＝制圧比1.5・撃退比0.7・損耗率0.2。</summary>
        public static BoardingParams Default => new BoardingParams(1.5f, 0.7f, 0.2f);
    }

    /// <summary>
    /// 白兵戦の純ロジック（移乗攻撃＝装甲擲弾兵の艦内戦闘）。艦砲では落とせない要塞・拿捕したい艦には
    /// 兵を送り込んで艦内で制圧する。実効戦力＝兵数×白兵技量で、戦力比が制圧比を超えれば奪取、
    /// 撃退比を割れば押し返され、間は膠着（消耗しつつ再判定）。艦の拿捕は撃沈と違い**戦力がそのまま手に入る**。
    /// 乱数なし・決定論（比のみで解決）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BoardingRules
    {
        /// <summary>白兵実効戦力＝兵数×白兵技量(0..1超可＝精鋭は1超)。</summary>
        public static float AssaultPower(float troops, float meleeSkill)
        {
            return Mathf.Max(0f, troops) * Mathf.Max(0f, meleeSkill);
        }

        /// <summary>
        /// 防御実効戦力＝乗員×白兵技量×艦内健全度 integrity(0..1)。損傷した艦は隔壁が破れて守りにくい。
        /// </summary>
        public static float DefensePower(float crew, float meleeSkill, float integrity)
        {
            return Mathf.Max(0f, crew) * Mathf.Max(0f, meleeSkill) * Mathf.Clamp01(integrity);
        }

        /// <summary>攻撃側/防御側の実効戦力比。防御0は攻撃側がいれば無限大（無人艦は無血制圧）。</summary>
        public static float PowerRatio(float assaultPower, float defensePower)
        {
            float a = Mathf.Max(0f, assaultPower);
            float d = Mathf.Max(0f, defensePower);
            if (a <= 0f) return 0f;
            if (d <= 0f) return float.PositiveInfinity;
            return a / d;
        }

        /// <summary>白兵戦の解決＝比が captureRatio 以上で制圧／repelRatio 未満で撃退／間は膠着。</summary>
        public static BoardingOutcome Resolve(float assaultPower, float defensePower, BoardingParams p)
        {
            float ratio = PowerRatio(assaultPower, defensePower);
            if (float.IsPositiveInfinity(ratio) || ratio >= p.captureRatio) return BoardingOutcome.制圧;
            if (ratio < p.repelRatio) return BoardingOutcome.撃退;
            return BoardingOutcome.膠着;
        }

        public static BoardingOutcome Resolve(float assaultPower, float defensePower)
            => Resolve(assaultPower, defensePower, BoardingParams.Default);

        /// <summary>
        /// 1回の白兵戦での攻撃側の損耗兵数。基礎損耗率を、劣勢なほど重く（実効比の逆数に比例）受ける。
        /// 防御0（無人）なら損耗なし。
        /// </summary>
        public static float AttackerCasualties(float troops, float assaultPower, float defensePower, BoardingParams p)
        {
            float ratio = PowerRatio(assaultPower, defensePower);
            if (float.IsPositiveInfinity(ratio)) return 0f;
            if (ratio <= 0f) return Mathf.Max(0f, troops) * p.casualtyRate;
            float severity = Mathf.Clamp01(1f / ratio);
            return Mathf.Max(0f, troops) * p.casualtyRate * severity;
        }

        public static float AttackerCasualties(float troops, float assaultPower, float defensePower)
            => AttackerCasualties(troops, assaultPower, defensePower, BoardingParams.Default);

        /// <summary>1回の白兵戦での防御側の損耗乗員数。優勢な攻撃側ほど重く削る（実効比に比例・上限1）。</summary>
        public static float DefenderCasualties(float crew, float assaultPower, float defensePower, BoardingParams p)
        {
            float ratio = PowerRatio(assaultPower, defensePower);
            float severity = float.IsPositiveInfinity(ratio) ? 1f : Mathf.Clamp01(ratio);
            return Mathf.Max(0f, crew) * p.casualtyRate * severity;
        }

        public static float DefenderCasualties(float crew, float assaultPower, float defensePower)
            => DefenderCasualties(crew, assaultPower, defensePower, BoardingParams.Default);

        /// <summary>拿捕の戦利戦力＝制圧した艦の残存戦力×健全度（壊しすぎた艦は値打ちが落ちる）。</summary>
        public static float PrizeValue(float shipStrength, float integrity)
        {
            return Mathf.Max(0f, shipStrength) * Mathf.Clamp01(integrity);
        }
    }
}
