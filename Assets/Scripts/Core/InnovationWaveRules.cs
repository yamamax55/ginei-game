using UnityEngine;

namespace Ginei
{
    /// <summary>コンドラチェフ長波の4フェーズ（上昇→繁栄→後退→不況の弧）。</summary>
    public enum KondratievPhase
    {
        /// <summary>上昇（Prosperity/Recovery 前半）＝不況期に束ねられた革新クラスターが普及を始め、景気が立ち上がる相。</summary>
        上昇,
        /// <summary>繁栄（Boom/Peak）＝革新の普及が最も活発で投資と成長がピークに達する相。</summary>
        繁栄,
        /// <summary>後退（Recession）＝普及が飽和し市場が食い尽くされ、成長が鈍って下り坂へ転じる相。</summary>
        後退,
        /// <summary>不況（Depression）＝古いクラスターが行き詰まり、その圧力が次の革新クラスターを準備する相。</summary>
        不況
    }

    /// <summary>革新の波（コンドラチェフ長波）の調整係数。</summary>
    public readonly struct InnovationWaveParams
    {
        /// <summary>波位置（0..1）で 上昇→繁栄 へ移る閾値。</summary>
        public readonly float prosperityThreshold;
        /// <summary>波位置（0..1）で 繁栄→後退 へ移る閾値。</summary>
        public readonly float recessionThreshold;
        /// <summary>波位置（0..1）で 後退→不況 へ移る閾値。</summary>
        public readonly float depressionThreshold;
        /// <summary>不況の圧力がクラスター形成へ寄与する重み（不況が革新を準備する＝シュンペーターの逆説の強さ）。</summary>
        public readonly float depressionPressureWeight;
        /// <summary>科学ストックがクラスター形成へ寄与する重み（蓄積された発明の在庫）。</summary>
        public readonly float scientificStockWeight;
        /// <summary>起業家精神がクラスター形成へ寄与する重み（革新を事業化する気概）。</summary>
        public readonly float entrepreneurialWeight;
        /// <summary>普及の基準速度（クラスター強度1のとき per dt・S字の立ち上がり率）。</summary>
        public readonly float diffusionRate;
        /// <summary>飽和の抵抗スケール（普及が進むほど成長を鈍らせる強さ＝市場の食い尽くし）。</summary>
        public readonly float saturationDragScale;
        /// <summary>波の基準振幅（クラスター強度0でも残る最小の波）。</summary>
        public readonly float baseAmplitude;
        /// <summary>クラスター強度が振幅へ寄与するスケール（大きな革新ほど大きな波）。</summary>
        public readonly float amplitudeScale;
        /// <summary>波位置を進める基準進行率（勢い1.0のとき per dt）。</summary>
        public readonly float wavePositionRate;

        public InnovationWaveParams(float prosperityThreshold, float recessionThreshold, float depressionThreshold,
            float depressionPressureWeight, float scientificStockWeight, float entrepreneurialWeight,
            float diffusionRate, float saturationDragScale, float baseAmplitude, float amplitudeScale,
            float wavePositionRate)
        {
            this.prosperityThreshold = Mathf.Clamp01(prosperityThreshold);
            this.recessionThreshold = Mathf.Clamp01(recessionThreshold);
            this.depressionThreshold = Mathf.Clamp01(depressionThreshold);
            this.depressionPressureWeight = Mathf.Max(0f, depressionPressureWeight);
            this.scientificStockWeight = Mathf.Max(0f, scientificStockWeight);
            this.entrepreneurialWeight = Mathf.Max(0f, entrepreneurialWeight);
            this.diffusionRate = Mathf.Max(0f, diffusionRate);
            this.saturationDragScale = Mathf.Max(0f, saturationDragScale);
            this.baseAmplitude = Mathf.Clamp01(baseAmplitude);
            this.amplitudeScale = Mathf.Max(0f, amplitudeScale);
            this.wavePositionRate = Mathf.Max(0f, wavePositionRate);
        }

        /// <summary>
        /// 既定＝繁栄閾値0.3・後退閾値0.6・不況閾値0.8・不況圧力重み0.5・科学ストック重み0.3・起業家重み0.2・
        /// 普及率0.4・飽和抵抗1.0・基準振幅0.2・振幅スケール0.8・波進行率0.5。
        /// </summary>
        public static InnovationWaveParams Default =>
            new InnovationWaveParams(0.3f, 0.6f, 0.8f, 0.5f, 0.3f, 0.2f, 0.4f, 1.0f, 0.2f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 革新の波（シュンペーターのコンドラチェフ循環）の純ロジック（SCHU-4 #1591・シュンペーター
    /// 『景気循環論』参考）。技術革新は均等にではなく束（クラスター）で現れ、不況期の圧力がその束を
    /// 準備し（不況が次の革新の苗床になる＝シュンペーターの逆説）、生まれたクラスターが経済へ S字に
    /// 普及して景気を押し上げ、普及が飽和すると市場を食い尽くして成長が鈍り後退へ転じる＝こうして
    /// 上昇→繁栄→後退→不況の数十年周期の長波が生まれる。「革新は束で現れ、不況がそれを準備し、
    /// 普及と飽和が長波をつくる」を式に出す。<see cref="CrisisCycleRules"/>（ミンスキー型の金融循環＝信用と
    /// 脆弱性の相）とは別系統＝こちらは技術革新の実物経済の長期波動。国家間の技術流入は
    /// <see cref="InnovationDiffusionRules"/>（国際伝播＝他国からの漏出）、同 EPIC の創造的破壊は
    /// <see cref="CreativeDestructionRules"/>（古い秩序の破壊と置換）へ委譲する。
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InnovationWaveRules
    {
        /// <summary>普及度の上限（0..1＝市場の100%へ行き着いたら飽和）。</summary>
        public const float MaxAdoption = 1f;

        /// <summary>
        /// 波位置（0..1）を4フェーズへ写す。閾値で 上昇→繁栄→後退→不況 と段階的に切り替わる
        /// （位置が進むほど相も進む＝長波の弧をたどる）。
        /// </summary>
        public static KondratievPhase PhaseOf(float wavePosition, InnovationWaveParams p)
        {
            float x = Mathf.Clamp01(wavePosition);
            if (x < p.prosperityThreshold) return KondratievPhase.上昇;
            if (x < p.recessionThreshold) return KondratievPhase.繁栄;
            if (x < p.depressionThreshold) return KondratievPhase.後退;
            return KondratievPhase.不況;
        }

        public static KondratievPhase PhaseOf(float wavePosition)
            => PhaseOf(wavePosition, InnovationWaveParams.Default);

        /// <summary>
        /// 相の決定論循環＝上昇→繁栄→後退→不況→上昇（不況の次は上昇＝次のクラスターが芽吹く）。
        /// 行き詰まった不況が次の革新クラスターを準備し、それが普及して新しい上昇が始まる長波の円環。
        /// </summary>
        public static KondratievPhase NextPhase(KondratievPhase phase)
        {
            switch (phase)
            {
                case KondratievPhase.上昇: return KondratievPhase.繁栄;
                case KondratievPhase.繁栄: return KondratievPhase.後退;
                case KondratievPhase.後退: return KondratievPhase.不況;
                default: return KondratievPhase.上昇; // 不況→上昇＝次のクラスターが芽吹く
            }
        }

        /// <summary>
        /// 革新クラスターの形成強度（0..1）。不況期の圧力 depression が束ねの主因で
        /// （不況が次の革新を準備する＝シュンペーターの逆説）、蓄積された科学ストックと起業家精神が
        /// それを事業化に変える。三者の重み付き和を1でクランプ＝不況なしには大きな束は生まれにくい。
        /// </summary>
        public static float ClusterFormation(float scientificStock, float entrepreneurialClimate, float depression,
            InnovationWaveParams p)
        {
            float stock = Mathf.Clamp01(scientificStock);
            float climate = Mathf.Clamp01(entrepreneurialClimate);
            float dep = Mathf.Clamp01(depression);
            return Mathf.Clamp01(p.depressionPressureWeight * dep
                                + p.scientificStockWeight * stock
                                + p.entrepreneurialWeight * climate);
        }

        public static float ClusterFormation(float scientificStock, float entrepreneurialClimate, float depression)
            => ClusterFormation(scientificStock, entrepreneurialClimate, depression, InnovationWaveParams.Default);

        /// <summary>
        /// 革新クラスターの普及度の1tick後の値（0..1）。残余（1−adoption）に比例して広がる S字（ロジスティック）で、
        /// クラスター強度が強いほど速い。普及が進むほど未開拓の余地が減り立ち上がりが鈍る＝飽和へ向かう。
        /// </summary>
        public static float DiffusionTick(float adoption, float clusterStrength, float dt, InnovationWaveParams p)
        {
            float a = Mathf.Clamp01(adoption);
            float strength = Mathf.Clamp01(clusterStrength);
            float headroom = MaxAdoption - a; // 残余＝まだ普及していない市場
            float gain = p.diffusionRate * strength * a * headroom * Mathf.Max(0f, dt);
            // S字の立ち上がり：普及ゼロからは芽が小さいため、基準漏出ぶんの初動を残余比例で足す（種火）。
            float seed = p.diffusionRate * strength * (1f - a) * 0.05f * Mathf.Max(0f, dt);
            return Mathf.Clamp01(a + gain + seed);
        }

        public static float DiffusionTick(float adoption, float clusterStrength, float dt)
            => DiffusionTick(adoption, clusterStrength, dt, InnovationWaveParams.Default);

        /// <summary>
        /// 飽和の抵抗（0..1）＝普及が飽和に近づくほど成長を鈍らせる足枷（市場の食い尽くし）。
        /// 普及度の2乗に比例＝終盤で急に効き、繁栄を後退へ転じさせる。成長率に (1−drag) を掛けて使う。
        /// </summary>
        public static float SaturationDrag(float adoption, InnovationWaveParams p)
        {
            float a = Mathf.Clamp01(adoption);
            return Mathf.Clamp01(a * a * p.saturationDragScale);
        }

        public static float SaturationDrag(float adoption)
            => SaturationDrag(adoption, InnovationWaveParams.Default);

        /// <summary>
        /// 波の振幅（0..1）＝クラスターの強さが長波の振れ幅を決める（大きな革新ほど大きな波）。
        /// 基準振幅にクラスター強度×スケールを足す＝弱い革新でも最小の波は立つが、強い束は深い長波をつくる。
        /// </summary>
        public static float WaveAmplitude(float clusterStrength, InnovationWaveParams p)
        {
            float strength = Mathf.Clamp01(clusterStrength);
            return Mathf.Clamp01(p.baseAmplitude + strength * p.amplitudeScale);
        }

        public static float WaveAmplitude(float clusterStrength)
            => WaveAmplitude(clusterStrength, InnovationWaveParams.Default);

        /// <summary>
        /// 景気の勢い（0..1）＝フェーズと普及度で決まる。上昇・繁栄は普及が進むほど勢いづき、
        /// 後退は飽和抵抗のぶん削られ、不況は最も淀む。波位置を進める速さの素。
        /// </summary>
        public static float EconomicMomentum(KondratievPhase phase, float adoption, InnovationWaveParams p)
        {
            float a = Mathf.Clamp01(adoption);
            float drag = SaturationDrag(a, p);
            switch (phase)
            {
                case KondratievPhase.上昇: return Mathf.Clamp01(0.5f + 0.5f * a);   // 普及で立ち上がる
                case KondratievPhase.繁栄: return Mathf.Clamp01(0.8f + 0.2f * a);   // ピークの勢い
                case KondratievPhase.後退: return Mathf.Clamp01(0.5f * (1f - drag)); // 飽和で削れる
                default: return Mathf.Clamp01(0.2f * (1f - drag));                  // 不況＝淀む
            }
        }

        public static float EconomicMomentum(KondratievPhase phase, float adoption)
            => EconomicMomentum(phase, adoption, InnovationWaveParams.Default);

        /// <summary>
        /// 長波の位置（0..1）を進める。勢い momentum と基準進行率で前進し、1.0 を超えたら 0 へ
        /// 巻き戻る＝長波の弧を一周して次のサイクルへ（不況の谷から次の上昇へ）。
        /// </summary>
        public static float WavePositionTick(float wavePosition, float momentum, float dt, InnovationWaveParams p)
        {
            float pos = Mathf.Clamp01(wavePosition);
            float advance = p.wavePositionRate * Mathf.Clamp01(momentum) * Mathf.Max(0f, dt);
            float next = pos + advance;
            if (next >= 1f) next -= 1f; // 循環＝一周したら谷へ巻き戻る
            return Mathf.Clamp01(next);
        }

        public static float WavePositionTick(float wavePosition, float momentum, float dt)
            => WavePositionTick(wavePosition, momentum, dt, InnovationWaveParams.Default);

        /// <summary>
        /// 長波の谷（不況の底＝次の革新クラスターの苗床）判定。波位置が閾値以上＝不況の深部に
        /// 沈んでいれば true。ここで蓄えられた科学ストックと不況圧力が次のクラスターを準備する。
        /// </summary>
        public static bool IsLongWaveTrough(float wavePosition, float threshold)
        {
            return Mathf.Clamp01(wavePosition) >= Mathf.Clamp01(threshold);
        }

        public static bool IsLongWaveTrough(float wavePosition, InnovationWaveParams p)
            => IsLongWaveTrough(wavePosition, p.depressionThreshold);

        public static bool IsLongWaveTrough(float wavePosition)
            => IsLongWaveTrough(wavePosition, InnovationWaveParams.Default);
    }
}
