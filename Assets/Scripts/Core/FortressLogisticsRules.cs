using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 要塞兵站の調整値（#要塞兵站）＝宇宙要塞の補給備蓄と持久力のパラメータ。
    /// 備蓄容量の係数・人員あたりの基礎消費・主砲運用の消費上乗せ・活動度の感度・主砲エネルギー係数・人員あたり生活維持コスト。
    /// すべて ctor で安全側へクランプ。盤面非依存・決定論。
    /// </summary>
    public readonly struct FortressLogisticsParams
    {
        /// <summary>貯蔵容積×兵站インフラ→備蓄容量の係数（大きいほど多く貯め込める）。</summary>
        public readonly float capacityScale;
        /// <summary>駐留人員1単位あたりの基礎消費（生活・整備の最低消費）。</summary>
        public readonly float baseConsumptionPerCapita;
        /// <summary>主砲運用（0..1）が満載のとき人員1単位あたりへ上乗せする消費。</summary>
        public readonly float mainGunConsumption;
        /// <summary>活動度（0..1）が消費へ効く感度（1.0＝満載で消費2倍）。</summary>
        public readonly float tempoScale;
        /// <summary>発射頻度×威力→主砲エネルギー消費の係数。</summary>
        public readonly float energyScale;
        /// <summary>駐留人員1単位あたりの生活維持コスト（生命維持の消費単価）。</summary>
        public readonly float lifeSupportPerCapita;

        public FortressLogisticsParams(float capacityScale, float baseConsumptionPerCapita, float mainGunConsumption,
            float tempoScale, float energyScale, float lifeSupportPerCapita)
        {
            this.capacityScale = Mathf.Clamp(capacityScale, 0.01f, 100f);
            this.baseConsumptionPerCapita = Mathf.Clamp(baseConsumptionPerCapita, 0.0001f, 100f);
            this.mainGunConsumption = Mathf.Clamp(mainGunConsumption, 0f, 100f);
            this.tempoScale = Mathf.Clamp(tempoScale, 0f, 10f);
            this.energyScale = Mathf.Clamp(energyScale, 0.0001f, 100f);
            this.lifeSupportPerCapita = Mathf.Clamp(lifeSupportPerCapita, 0.0001f, 100f);
        }

        /// <summary>既定：容量係数1.0・基礎消費0.01/人・主砲上乗せ0.02/人・活動度感度1.0・エネルギー係数1.0・生活維持0.005/人。</summary>
        public static FortressLogisticsParams Default => new FortressLogisticsParams(
            DefaultCapacityScale, DefaultBaseConsumptionPerCapita, DefaultMainGunConsumption,
            DefaultTempoScale, DefaultEnergyScale, DefaultLifeSupportPerCapita);

        public const float DefaultCapacityScale = 1.0f;
        public const float DefaultBaseConsumptionPerCapita = 0.01f;
        public const float DefaultMainGunConsumption = 0.02f;
        public const float DefaultTempoScale = 1.0f;
        public const float DefaultEnergyScale = 1.0f;
        public const float DefaultLifeSupportPerCapita = 0.005f;
    }

    /// <summary>
    /// 要塞兵站の純ロジック（#要塞兵站・唯一の窓口）＝<b>宇宙要塞の補給備蓄と持久力</b>。
    /// 貯蔵容積×兵站インフラで<b>備蓄容量</b>（<see cref="StockpileCapacity"/>）を持ち、駐留人員・主砲運用・活動度で<b>消費</b>（<see cref="ConsumptionRate"/>）が決まる。
    /// 備蓄÷消費で<b>攻囲下の持久日数</b>（<see cref="SiegeEndurance"/>）＝補給が断たれてもこの日数は持ちこたえる（イゼルローン要塞の持久戦）。
    /// 封鎖は補給線を断ち（<see cref="SupplyInterdiction"/>）、外部から使える<b>実効備蓄</b>（<see cref="EffectiveStockpile"/>）を削る。
    /// 主砲（トゥール・ハンマー型）は撃つたびに膨大なエネルギーを食い（<see cref="MainGunEnergyDrain"/>）、駐留人員の<b>生活維持</b>（<see cref="LifeSupportSustainment"/>）も備蓄を消費する。
    /// <b>分担</b>：会戦の損耗そのものは別系統＝こちらは<b>要塞の備蓄と持久</b>の数値のみ。補給線の維持/突破の戦闘は呼び出し側で扱う（ここは封鎖比から途絶度だけ出す）。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・test-first。
    /// </summary>
    public static class FortressLogisticsRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 備蓄容量 ──────────────────────────────────────────────

        /// <summary>備蓄容量＝貯蔵容積×兵站インフラ×capacityScale（負入力は0扱い・非負）。</summary>
        public static float StockpileCapacity(float storageVolume, float logisticsInfrastructure)
            => StockpileCapacity(storageVolume, logisticsInfrastructure, FortressLogisticsParams.Default);

        /// <summary>備蓄容量＝貯蔵容積×兵站インフラ×capacityScale。負は0。</summary>
        public static float StockpileCapacity(float storageVolume, float logisticsInfrastructure, FortressLogisticsParams p)
        {
            float v = Mathf.Max(0f, storageVolume);
            float infra = Mathf.Max(0f, logisticsInfrastructure);
            return v * infra * p.capacityScale;
        }

        // ── 消費率 ────────────────────────────────────────────────

        /// <summary>消費率＝人員×(基礎消費＋主砲運用×主砲消費)×(1＋活動度×感度)（負入力は0扱い）。</summary>
        public static float ConsumptionRate(float garrisonSize, float mainGunUsage, float operationalTempo)
            => ConsumptionRate(garrisonSize, mainGunUsage, operationalTempo, FortressLogisticsParams.Default);

        /// <summary>消費率＝人員×(baseConsumptionPerCapita＋clamp01(主砲運用)×mainGunConsumption)×(1＋clamp01(活動度)×tempoScale)。</summary>
        public static float ConsumptionRate(float garrisonSize, float mainGunUsage, float operationalTempo, FortressLogisticsParams p)
        {
            float g = Mathf.Max(0f, garrisonSize);
            float gun = Mathf.Clamp01(mainGunUsage);
            float tempo = Mathf.Clamp01(operationalTempo);
            float perCapita = p.baseConsumptionPerCapita + gun * p.mainGunConsumption;
            float tempoFactor = 1f + tempo * p.tempoScale;
            return g * perCapita * tempoFactor;
        }

        // ── 攻囲下の持久日数 ──────────────────────────────────────

        /// <summary>攻囲下の持久日数＝備蓄容量÷消費率（補給断でも持ちこたえる日数）。消費0は実質無限＝大きな値。</summary>
        public static float SiegeEndurance(float stockpileCapacity, float consumptionRate)
            => SiegeEndurance(stockpileCapacity, consumptionRate, FortressLogisticsParams.Default);

        /// <summary>攻囲下の持久日数＝備蓄÷消費（非負）。消費が微小以下なら備蓄÷Epsilon＝極大（事実上無限）。</summary>
        public static float SiegeEndurance(float stockpileCapacity, float consumptionRate, FortressLogisticsParams p)
        {
            float stock = Mathf.Max(0f, stockpileCapacity);
            float consume = Mathf.Max(Epsilon, consumptionRate);
            return stock / consume;
        }

        // ── 主砲エネルギー消費 ────────────────────────────────────

        /// <summary>主砲のエネルギー消費＝発射頻度×威力×energyScale（撃つほど・強いほど食う・非負）。</summary>
        public static float MainGunEnergyDrain(float firingFrequency, float burstYield)
            => MainGunEnergyDrain(firingFrequency, burstYield, FortressLogisticsParams.Default);

        /// <summary>主砲のエネルギー消費＝発射頻度×威力×energyScale。負入力は0扱い。</summary>
        public static float MainGunEnergyDrain(float firingFrequency, float burstYield, FortressLogisticsParams p)
        {
            float freq = Mathf.Max(0f, firingFrequency);
            float yield = Mathf.Max(0f, burstYield);
            return freq * yield * p.energyScale;
        }

        // ── 補給途絶度 ────────────────────────────────────────────

        /// <summary>補給途絶度（0..1）＝封鎖力÷(封鎖力＋補給突破力)。封鎖が勝るほど1へ。</summary>
        public static float SupplyInterdiction(float blockadeStrength, float supplyRunCapacity)
            => SupplyInterdiction(blockadeStrength, supplyRunCapacity, FortressLogisticsParams.Default);

        /// <summary>補給途絶度（0..1）＝封鎖/(封鎖+突破)。両者0なら途絶なし＝0。</summary>
        public static float SupplyInterdiction(float blockadeStrength, float supplyRunCapacity, FortressLogisticsParams p)
        {
            float block = Mathf.Max(0f, blockadeStrength);
            float run = Mathf.Max(0f, supplyRunCapacity);
            float denom = block + run;
            if (denom <= Epsilon) return 0f;
            return Mathf.Clamp01(block / denom);
        }

        // ── 実効備蓄 ──────────────────────────────────────────────

        /// <summary>実効的に使える備蓄＝備蓄容量×(1−途絶度)（封鎖されるほど外部補給分が削られる・非負）。</summary>
        public static float EffectiveStockpile(float stockpileCapacity, float supplyInterdiction)
            => EffectiveStockpile(stockpileCapacity, supplyInterdiction, FortressLogisticsParams.Default);

        /// <summary>実効備蓄＝備蓄×(1−clamp01(途絶度))。備蓄の負入力は0扱い。</summary>
        public static float EffectiveStockpile(float stockpileCapacity, float supplyInterdiction, FortressLogisticsParams p)
        {
            float stock = Mathf.Max(0f, stockpileCapacity);
            float interdiction = Mathf.Clamp01(supplyInterdiction);
            return stock * (1f - interdiction);
        }

        // ── 生活維持の持続 ────────────────────────────────────────

        /// <summary>生活維持の持続＝備蓄÷(人員×生活維持単価)（人員が多いほど早く尽きる）。人員0は実質無限＝大きな値。</summary>
        public static float LifeSupportSustainment(float stockpileCapacity, float garrisonSize)
            => LifeSupportSustainment(stockpileCapacity, garrisonSize, FortressLogisticsParams.Default);

        /// <summary>生活維持の持続＝備蓄÷(人員×lifeSupportPerCapita)（非負）。需要が微小以下なら備蓄÷Epsilon＝極大。</summary>
        public static float LifeSupportSustainment(float stockpileCapacity, float garrisonSize, FortressLogisticsParams p)
        {
            float stock = Mathf.Max(0f, stockpileCapacity);
            float g = Mathf.Max(0f, garrisonSize);
            float demand = Mathf.Max(Epsilon, g * p.lifeSupportPerCapita);
            return stock / demand;
        }

        // ── 自力持久の判定 ────────────────────────────────────────

        /// <summary>自力で持久できるか＝持久日数が既定閾値（<see cref="SelfSustainingThreshold"/>=180日）以上（bool）。</summary>
        public static bool IsSelfSustaining(float siegeEndurance)
            => IsSelfSustaining(siegeEndurance, SelfSustainingThreshold);

        /// <summary>自力で持久できるか＝持久日数が閾値以上（bool）。</summary>
        public static bool IsSelfSustaining(float siegeEndurance, float threshold)
            => siegeEndurance >= threshold;

        /// <summary>自力持久とみなす持久日数の既定閾値（180日＝半年の攻囲に耐える）。</summary>
        public const float SelfSustainingThreshold = 180f;
    }
}
