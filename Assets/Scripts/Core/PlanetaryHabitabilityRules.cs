using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星居住性の調整値（マジックナンバー禁止＝集約）。大気/重力/温度の適正帯と各補正の強さ。
    /// 全フィールド ctor で Clamp（非負・割合は Clamp01）＝負値や暴れ値を入れても破綻しない。
    /// </summary>
    public readonly struct PlanetaryHabitabilityParams
    {
        public readonly float idealPressure;       // 適正気圧（1.0=標準1気圧想定）
        public readonly float pressureTolerance;   // 気圧の許容幅（これだけ外れると適性が大きく落ちる）
        public readonly float idealGravity;        // 適正重力（1.0=1G想定）
        public readonly float gravityTolerance;    // 重力の許容幅
        public readonly float idealTemperature;    // 適温（0.5=快適帯の中央・温度入力は0..1正規化）
        public readonly float thermalTolerance;    // 温度の許容幅
        public readonly float iceWeight;           // 氷床が水利用に効く割合（地表水より割引）
        public readonly float synergyExponent;     // 4要素総合の相乗指数（>1で「どれか欠けると総崩れ」を強める）
        public readonly float terraformPeak;       // テラフォーミング余地が最大になる居住性（中間ほど伸びしろ大）
        public readonly float habitatWeight;       // 人工環境が実効収容力に寄与する割合

        public PlanetaryHabitabilityParams(
            float idealPressure, float pressureTolerance,
            float idealGravity, float gravityTolerance,
            float idealTemperature, float thermalTolerance,
            float iceWeight, float synergyExponent,
            float terraformPeak, float habitatWeight)
        {
            this.idealPressure = Mathf.Max(0f, idealPressure);
            this.pressureTolerance = Mathf.Max(0.01f, pressureTolerance);
            this.idealGravity = Mathf.Max(0f, idealGravity);
            this.gravityTolerance = Mathf.Max(0.01f, gravityTolerance);
            this.idealTemperature = Mathf.Clamp01(idealTemperature);
            this.thermalTolerance = Mathf.Max(0.01f, thermalTolerance);
            this.iceWeight = Mathf.Clamp01(iceWeight);
            this.synergyExponent = Mathf.Clamp(synergyExponent, 1f, 4f);
            this.terraformPeak = Mathf.Clamp01(terraformPeak);
            this.habitatWeight = Mathf.Clamp01(habitatWeight);
        }

        /// <summary>
        /// 既定＝適正気圧1.0(許容0.5)・適正重力1.0(許容0.5)・適温0.5(許容0.35)・氷床は水の半分・
        /// 相乗指数1.5(欠落で総崩れ)・テラフォーム余地は居住性0.4で最大・人工環境寄与0.6。
        /// </summary>
        public static PlanetaryHabitabilityParams Default => new PlanetaryHabitabilityParams(
            1f, 0.5f,
            1f, 0.5f,
            0.5f, 0.35f,
            0.5f, 1.5f,
            0.4f, 0.6f);
    }

    /// <summary>
    /// 惑星居住性＝惑星環境の人類居住適性の純ロジック（Core・決定論・乱数なし・test-first）。
    /// 大気/重力/温度/水の4要素から素の居住性を出し、生命維持コスト・テラフォーミング余地・
    /// 人工環境込みの実効収容力まで導く。実効値パターン（入力を破壊せずローカルで計算）・全入力 Clamp・
    /// 配列は受け取らないため null 非該当だが、欠落要素（0）は <see cref="BaseHabitability"/> の相乗で総崩れに反映。
    /// 各メソッドは Params 明示版＋ Default 委譲版を持つ。
    /// </summary>
    public static class PlanetaryHabitabilityRules
    {
        /// <summary>適正値からの逸脱を0..1の適性へ写す（許容幅で割った絶対距離を1から引く）。決定論。</summary>
        private static float Proximity(float value, float ideal, float tolerance)
        {
            float dist = Mathf.Abs(value - ideal);
            return Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, tolerance));
        }

        /// <summary>
        /// 大気の居住性（0..1）＝酸素×(1-毒性)×適正気圧。酸素が薄い・毒性が高い・気圧が外れると下がる。
        /// </summary>
        public static float AtmosphereScore(float oxygenLevel, float toxicity, float pressure, PlanetaryHabitabilityParams p)
        {
            float oxy = Mathf.Clamp01(oxygenLevel);
            float clean = 1f - Mathf.Clamp01(toxicity);
            float press = Proximity(Mathf.Max(0f, pressure), p.idealPressure, p.pressureTolerance);
            return Mathf.Clamp01(oxy * clean * press);
        }

        /// <summary>大気の居住性（既定Params委譲版）。</summary>
        public static float AtmosphereScore(float oxygenLevel, float toxicity, float pressure)
            => AtmosphereScore(oxygenLevel, toxicity, pressure, PlanetaryHabitabilityParams.Default);

        /// <summary>
        /// 重力適性（0..1）＝標準重力(1G想定)からの逸脱で低下。過大（重い）も過小（軽い）も住みづらい。
        /// </summary>
        public static float GravityScore(float gravity, PlanetaryHabitabilityParams p)
            => Proximity(Mathf.Max(0f, gravity), p.idealGravity, p.gravityTolerance);

        /// <summary>重力適性（既定Params委譲版）。</summary>
        public static float GravityScore(float gravity)
            => GravityScore(gravity, PlanetaryHabitabilityParams.Default);

        /// <summary>
        /// 温度適性（0..1）＝適温帯（0..1正規化の中央付近）からの逸脱で低下。灼熱も極寒も下がる。
        /// </summary>
        public static float ThermalScore(float temperature, PlanetaryHabitabilityParams p)
            => Proximity(Mathf.Clamp01(temperature), p.idealTemperature, p.thermalTolerance);

        /// <summary>温度適性（既定Params委譲版）。</summary>
        public static float ThermalScore(float temperature)
            => ThermalScore(temperature, PlanetaryHabitabilityParams.Default);

        /// <summary>
        /// 水の利用可能性（0..1）＝地表水＋氷床×割引。地表水が主、氷床は採掘/融解の手間ぶん割り引く。
        /// </summary>
        public static float WaterAvailability(float surfaceWater, float iceReserves, PlanetaryHabitabilityParams p)
        {
            float surf = Mathf.Clamp01(surfaceWater);
            float ice = Mathf.Clamp01(iceReserves) * p.iceWeight;
            return Mathf.Clamp01(surf + ice);
        }

        /// <summary>水の利用可能性（既定Params委譲版）。</summary>
        public static float WaterAvailability(float surfaceWater, float iceReserves)
            => WaterAvailability(surfaceWater, iceReserves, PlanetaryHabitabilityParams.Default);

        /// <summary>
        /// 素の居住性（0..1）＝4要素（大気/重力/温度/水）の相乗平均を相乗指数で締める。
        /// どれか1要素でも0なら総合も0＝「欠けると大きく下がる」を相乗で表現する。
        /// </summary>
        public static float BaseHabitability(float atmosphereScore, float gravityScore, float thermalScore, float waterAvailability, PlanetaryHabitabilityParams p)
        {
            float a = Mathf.Clamp01(atmosphereScore);
            float g = Mathf.Clamp01(gravityScore);
            float t = Mathf.Clamp01(thermalScore);
            float w = Mathf.Clamp01(waterAvailability);
            float product = a * g * t * w;            // 0..1（欠落要素で0へ）
            float geo = Mathf.Pow(product, 0.25f);    // 4要素の幾何平均
            float shaped = Mathf.Pow(geo, p.synergyExponent); // 指数>1で中庸を下げ偏りに厳しく
            return Mathf.Clamp01(shaped);
        }

        /// <summary>素の居住性（既定Params委譲版）。</summary>
        public static float BaseHabitability(float atmosphereScore, float gravityScore, float thermalScore, float waterAvailability)
            => BaseHabitability(atmosphereScore, gravityScore, thermalScore, waterAvailability, PlanetaryHabitabilityParams.Default);

        /// <summary>
        /// 生命維持コスト（0..1）＝(1-居住性)。過酷なほど高い＝楽園は0、致死環境は1。
        /// </summary>
        public static float LifeSupportCost(float baseHabitability, PlanetaryHabitabilityParams p)
            => Mathf.Clamp01(1f - Mathf.Clamp01(baseHabitability));

        /// <summary>生命維持コスト（既定Params委譲版）。</summary>
        public static float LifeSupportCost(float baseHabitability)
            => LifeSupportCost(baseHabitability, PlanetaryHabitabilityParams.Default);

        /// <summary>
        /// テラフォーミングの余地（0..1）＝中間的な居住性×惑星質量。
        /// 既に楽園（高居住性）は伸びしろ無し、致死環境（低居住性）は手に余る＝<see cref="PlanetaryHabitabilityParams.terraformPeak"/>
        /// 付近の「あと一歩」の惑星で最大。質量が小さいと大気を保てず改造が利かない（質量で減衰）。
        /// </summary>
        public static float TerraformingPotential(float baseHabitability, float planetMass, PlanetaryHabitabilityParams p)
        {
            float h = Mathf.Clamp01(baseHabitability);
            float peak = p.terraformPeak;
            // peak で1.0、両端(0/1)で0へ落ちる三角ポテンシャル
            float window = h <= peak
                ? (peak <= 0f ? 1f : h / peak)
                : (peak >= 1f ? 1f : (1f - h) / (1f - peak));
            float mass = Mathf.Clamp01(planetMass);
            return Mathf.Clamp01(Mathf.Clamp01(window) * mass);
        }

        /// <summary>テラフォーミングの余地（既定Params委譲版）。</summary>
        public static float TerraformingPotential(float baseHabitability, float planetMass)
            => TerraformingPotential(baseHabitability, planetMass, PlanetaryHabitabilityParams.Default);

        /// <summary>
        /// 実効的な人口収容力（0..1）＝素の居住性＋人工環境（ドーム都市等）で底上げ。
        /// 過酷でも人工環境（<paramref name="artificialHabitat"/>）への依存で住める＝過酷環境のプレミアム入植。
        /// 残り余地(1-居住性)に人工環境×<see cref="PlanetaryHabitabilityParams.habitatWeight"/>を埋める。
        /// </summary>
        public static float EffectiveCapacity(float baseHabitability, float artificialHabitat, PlanetaryHabitabilityParams p)
        {
            float h = Mathf.Clamp01(baseHabitability);
            float artificial = Mathf.Clamp01(artificialHabitat) * p.habitatWeight;
            float filled = h + (1f - h) * artificial; // 人工環境は素の居住性の残り余地を埋める
            return Mathf.Clamp01(filled);
        }

        /// <summary>実効的な人口収容力（既定Params委譲版）。</summary>
        public static float EffectiveCapacity(float baseHabitability, float artificialHabitat)
            => EffectiveCapacity(baseHabitability, artificialHabitat, PlanetaryHabitabilityParams.Default);
    }
}
