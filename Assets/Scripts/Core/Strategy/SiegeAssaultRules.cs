using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 攻城戦術の選択（強襲 vs 兵糧攻め）の調整係数。
    /// 強襲＝速いが防御施設に正面から当たり大損害／包囲＝遅いが損害小だが時間がかかる。
    /// </summary>
    public readonly struct SiegeAssaultParams
    {
        /// <summary>強襲の基準損害率（攻撃側兵力に対する割合の係数）。</summary>
        public readonly float assaultCasualtyRate;
        /// <summary>防御施設レベルが損害を増幅する強さ（0..）。固いほど強襲は血を流す。</summary>
        public readonly float fortificationCasualtyScale;
        /// <summary>強襲で落とす基準速度（兵力あたり）。</summary>
        public readonly float assaultSpeedRate;
        /// <summary>兵糧攻め＝封鎖戦力あたりの補給枯渇速度。</summary>
        public readonly float starvationRate;
        /// <summary>包囲側が時間で被る消耗・士気低下の速度（疫病・厭戦）。</summary>
        public readonly float blockadeAttritionRate;
        /// <summary>長期包囲中の敵の打って出る（出撃）逆襲リスクの増加速度。</summary>
        public readonly float sallyRiskRate;

        public SiegeAssaultParams(
            float assaultCasualtyRate, float fortificationCasualtyScale, float assaultSpeedRate,
            float starvationRate, float blockadeAttritionRate, float sallyRiskRate)
        {
            this.assaultCasualtyRate       = Mathf.Clamp01(assaultCasualtyRate);
            this.fortificationCasualtyScale = Mathf.Max(0f, fortificationCasualtyScale);
            this.assaultSpeedRate          = Mathf.Max(0f, assaultSpeedRate);
            this.starvationRate            = Mathf.Max(0f, starvationRate);
            this.blockadeAttritionRate     = Mathf.Max(0f, blockadeAttritionRate);
            this.sallyRiskRate             = Mathf.Max(0f, sallyRiskRate);
        }

        /// <summary>既定係数。強襲は損害大・速い／包囲は損害小・遅い、というトレードオフを担保する値。</summary>
        public static SiegeAssaultParams Default =>
            new SiegeAssaultParams(0.10f, 0.50f, 0.20f, 0.10f, 0.02f, 0.05f);
    }

    /// <summary>
    /// 攻城の強襲 vs 兵糧攻め（速攻と損害のトレードオフ）の純ロジック。
    /// 要塞・惑星の攻略には、強襲（速いが損害大）と包囲・兵糧攻め（遅いが損害小）の選択がある。
    /// 強襲は防御施設に正面から当たり大損害、包囲は時間で敵を消耗させる。
    ///
    /// 分担：<see cref="PlanetSiegeRules"/>（惑星攻城の制空権抑制→侵略の二段階・自動解決）とは別＝
    /// 本ルールは「強襲か包囲か」の戦術選択と損害収支を扱う。チョーク戦（隘路）等とも別物。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・乱数なし（必要なら roll を渡す）・test-first。
    /// </summary>
    public static class SiegeAssaultRules
    {
        /// <summary>突破口拡張で予備兵力が効く強さ（BreachExploitation 用の調整定数）。</summary>
        public const float BreachReserveScale = 0.5f;

        /// <summary>
        /// 強襲の損害（攻撃側の被る兵力損失）。防御施設(fortificationLevel 0..1)が固いほど大きい。
        /// = 兵力 × 基準損害率 ×（1 + 施設レベル × 施設増幅）。
        /// </summary>
        public static float AssaultCasualties(float attackerStrength, float fortificationLevel, SiegeAssaultParams p)
        {
            float s = Mathf.Max(0f, attackerStrength);
            float fort = Mathf.Clamp01(fortificationLevel);
            return s * p.assaultCasualtyRate * (1f + fort * p.fortificationCasualtyScale);
        }

        public static float AssaultCasualties(float attackerStrength, float fortificationLevel)
            => AssaultCasualties(attackerStrength, fortificationLevel, SiegeAssaultParams.Default);

        /// <summary>
        /// 強襲で落とす速さ（攻略進捗/秒の係数）。兵力が多いほど速く、防御施設が固いほど遅い。
        /// = 兵力 × 速度率 ×（1 − 施設レベル）。施設レベル1で停滞（正面強襲では抜けない）。
        /// </summary>
        public static float AssaultSpeed(float attackerStrength, float fortificationLevel, SiegeAssaultParams p)
        {
            float s = Mathf.Max(0f, attackerStrength);
            float fort = Mathf.Clamp01(fortificationLevel);
            return s * p.assaultSpeedRate * (1f - fort);
        }

        public static float AssaultSpeed(float attackerStrength, float fortificationLevel)
            => AssaultSpeed(attackerStrength, fortificationLevel, SiegeAssaultParams.Default);

        /// <summary>
        /// 兵糧攻めの進行＝この dt で削れる敵の補給残量。封鎖戦力が大きいほど速く干上がる。
        /// 残量 defenderSupplies を下回って削らない（0でクランプした実消費量を返す）。
        /// </summary>
        public static float StarvationProgress(float blockadeStrength, float defenderSupplies, float dt, SiegeAssaultParams p)
        {
            if (dt <= 0f) return 0f;
            float blockade = Mathf.Max(0f, blockadeStrength);
            float supplies = Mathf.Max(0f, defenderSupplies);
            float drain = blockade * p.starvationRate * dt;
            return Mathf.Min(drain, supplies); // 残量以上は削れない
        }

        public static float StarvationProgress(float blockadeStrength, float defenderSupplies, float dt)
            => StarvationProgress(blockadeStrength, defenderSupplies, dt, SiegeAssaultParams.Default);

        /// <summary>
        /// 包囲側の消耗（兵糧攻め中に時間で失う戦力比＝疫病・厭戦・脱走）。0..1。
        /// = clamp01(包囲日数 × 消耗率)。長引くほど包囲側も削れる（速攻の誘因）。
        /// </summary>
        public static float BlockadeAttrition(float siegeDuration, SiegeAssaultParams p)
        {
            float days = Mathf.Max(0f, siegeDuration);
            return Mathf.Clamp01(days * p.blockadeAttritionRate);
        }

        public static float BlockadeAttrition(float siegeDuration)
            => BlockadeAttrition(siegeDuration, SiegeAssaultParams.Default);

        /// <summary>
        /// 速攻が要るか損害を惜しむか（-1=包囲寄り／+1=強襲寄り）。
        /// urgency(0..1 急ぐほど強襲)・casualtyTolerance(0..1 損害を許容するほど強襲)。
        /// = clamp(urgency + casualtyTolerance − 1, −1, +1)。両方0.5で中立(0)。
        /// </summary>
        public static float AssaultVsSiegeTradeoff(float urgency, float casualtyTolerance)
        {
            float u = Mathf.Clamp01(urgency);
            float c = Mathf.Clamp01(casualtyTolerance);
            return Mathf.Clamp(u + c - 1f, -1f, 1f);
        }

        /// <summary>
        /// 長期包囲中に敵が打って出る（出撃＝逆襲）リスク 0..1。
        /// 包囲が長引くほど（追い詰められ）、守備兵力が厚いほど打って出やすい。
        /// = clamp01(包囲日数 × sally率 ×（1 + 守備兵力の規模係数））。
        /// </summary>
        public static float SallyRisk(float siegeDuration, float defenderStrength, SiegeAssaultParams p)
        {
            float days = Mathf.Max(0f, siegeDuration);
            float def = Mathf.Max(0f, defenderStrength);
            // 守備兵力は 100 を基準に 0..1 でならす（規模が大きいほど出撃の余力）。
            float defFactor = Mathf.Clamp01(def / 100f);
            return Mathf.Clamp01(days * p.sallyRiskRate * (1f + defFactor));
        }

        public static float SallyRisk(float siegeDuration, float defenderStrength)
            => SallyRisk(siegeDuration, defenderStrength, SiegeAssaultParams.Default);

        /// <summary>
        /// 開いた突破口を予備兵力で拡張する（このtickで広がる突破口量）。
        /// breachSize(0..1 既存の突破口)・assaultReserve(投入できる予備兵力)。
        /// = breachSize ×（予備の効き）。突破口が無ければ広げられない（0×＝0）。
        /// </summary>
        public static float BreachExploitation(float breachSize, float assaultReserve)
        {
            float breach = Mathf.Clamp01(breachSize);
            float reserve = Mathf.Max(0f, assaultReserve);
            // 予備兵力 100 で効きが飽和（1.0 へ漸近の線形クランプ）。
            float reserveFactor = Mathf.Clamp01(reserve / 100f);
            return breach * reserveFactor * (1f + BreachReserveScale);
        }

        /// <summary>
        /// 要塞が持たなくなったか（兵糧攻めの決着判定）。
        /// 補給が尽きかけ かつ 士気が閾値を割ったら陥落寸前。
        /// threshold は 0..1（既定 0.2 相当を呼び出し側が渡す想定）。
        /// </summary>
        public static bool IsFortressUntenable(float defenderSupplies, float defenderMorale, float threshold)
        {
            float supplies = Mathf.Max(0f, defenderSupplies);
            float morale = Mathf.Clamp01(defenderMorale);
            float th = Mathf.Clamp01(threshold);
            return supplies <= 0f || morale <= th;
        }
    }
}
