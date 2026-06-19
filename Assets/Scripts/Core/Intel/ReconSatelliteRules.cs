using UnityEngine;

namespace Ginei
{
    /// <summary>偵察衛星・偵察機（前方偵察と監視・#偵察衛星）の調整係数。</summary>
    public readonly struct ReconSatelliteParams
    {
        /// <summary>カバー率のスケール（偵察機数×走査幅/面積に掛ける感度）。</summary>
        public readonly float coverageScale;
        /// <summary>高度ペナルティ係数（高度が解像度を落とす強さ＝1/(1+高度×これ)）。</summary>
        public readonly float altitudePenalty;
        /// <summary>再訪頻度のスケール（機数/周期に掛ける感度）。</summary>
        public readonly float revisitScale;
        /// <summary>被発見/撃墜リスクのスケール（低空×敵防空に掛ける感度）。</summary>
        public readonly float riskScale;
        /// <summary>情報鮮度の減衰係数（経過時間が鮮度を落とす強さ＝1/(1+経過×これ)）。</summary>
        public readonly float freshnessDecay;
        /// <summary>偵察拒否のスケール（敵隠蔽×敵妨害に掛ける感度）。</summary>
        public readonly float denialScale;
        /// <summary>偵察成立の既定しきい値（実効偵察価値－リスクがこれ以上なら成立）。</summary>
        public readonly float viabilityThreshold;

        public ReconSatelliteParams(float coverageScale, float altitudePenalty, float revisitScale,
            float riskScale, float freshnessDecay, float denialScale, float viabilityThreshold)
        {
            this.coverageScale = Mathf.Max(0f, coverageScale);
            this.altitudePenalty = Mathf.Max(0f, altitudePenalty);
            this.revisitScale = Mathf.Max(0f, revisitScale);
            this.riskScale = Mathf.Max(0f, riskScale);
            this.freshnessDecay = Mathf.Max(0f, freshnessDecay);
            this.denialScale = Mathf.Max(0f, denialScale);
            this.viabilityThreshold = Mathf.Clamp01(viabilityThreshold);
        }

        /// <summary>既定＝カバー1.0/高度ペナルティ1.0/再訪1.0/リスク1.0/鮮度減衰0.5/拒否1.0/成立閾値0.2。</summary>
        public static ReconSatelliteParams Default
            => new ReconSatelliteParams(1f, 1f, 1f, 1f, 0.5f, 1f, 0.2f);
    }

    /// <summary>
    /// 偵察衛星・偵察機による前方偵察と監視（#偵察衛星）の純ロジック。偵察範囲のカバー、
    /// 解像度と高度のトレードオフ（低高度ほど精細だが危険）、再訪頻度＝監視の継続性、
    /// 被発見・撃墜リスク、偵察情報の鮮度、敵の偵察拒否（隠蔽×妨害）を扱い、最後にそれらを
    /// 合成して実効偵察価値と「偵察が成立するか（損失リスク見合いか）」を返す。
    /// 高度・敵能力などの 0..1 入力は正規化済みとして扱う。乱数なし決定論・全入力クランプ。
    /// 実効値パターン（基準フィールド非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// 分担：<see cref="DetectionRules"/> は会戦の索敵範囲、<see cref="BattlePerceptionRules"/> は
    /// 提督の戦場知覚を担う。ここは衛星/偵察機による偵察オペレーションそのものを扱う。
    /// </summary>
    public static class ReconSatelliteRules
    {
        /// <summary>
        /// 偵察範囲のカバー率(0..1)＝偵察機数×走査幅 / 対象面積。多くの機で広く掃くほど覆える。
        /// 面積0以下は完全カバー扱い（覆うべき面積が無い）。
        /// </summary>
        public static float ReconCoverage(float orbitCount, float sweepWidth, float areaSize, ReconSatelliteParams p)
        {
            float n = Mathf.Max(0f, orbitCount);
            float w = Mathf.Max(0f, sweepWidth);
            float area = Mathf.Max(0f, areaSize);
            if (area <= 0f) return 1f;
            return Mathf.Clamp01(n * w / area * p.coverageScale);
        }

        public static float ReconCoverage(float orbitCount, float sweepWidth, float areaSize)
            => ReconCoverage(orbitCount, sweepWidth, areaSize, ReconSatelliteParams.Default);

        /// <summary>
        /// 解像度(0..1)＝センサー精度 / (1 + 高度×ペナルティ)。低高度ほど精細（だが下の被発見リスクが上がる）。
        /// </summary>
        public static float Resolution(float altitude, float sensorQuality, ReconSatelliteParams p)
        {
            float alt = Mathf.Clamp01(altitude);
            float quality = Mathf.Clamp01(sensorQuality);
            return Mathf.Clamp01(quality / (1f + alt * p.altitudePenalty));
        }

        public static float Resolution(float altitude, float sensorQuality)
            => Resolution(altitude, sensorQuality, ReconSatelliteParams.Default);

        /// <summary>
        /// 再訪頻度(0..1)＝機数 / 周期。機が多く周期が短いほど高い＝監視が途切れにくい（継続的な監視）。
        /// 周期0以下は最頻（常時上空）扱いに寄せるため (周期+1) で割る。
        /// </summary>
        public static float RevisitRate(float orbitCount, float orbitalPeriod, ReconSatelliteParams p)
        {
            float n = Mathf.Max(0f, orbitCount);
            float period = Mathf.Max(0f, orbitalPeriod);
            return Mathf.Clamp01(n / (period + 1f) * p.revisitScale);
        }

        public static float RevisitRate(float orbitCount, float orbitalPeriod)
            => RevisitRate(orbitCount, orbitalPeriod, ReconSatelliteParams.Default);

        /// <summary>
        /// 被発見/撃墜リスク(0..1)＝(1 - 高度)×敵防空。低空ほど捕捉・迎撃されやすい（解像度との表裏）。
        /// </summary>
        public static float DetectionRisk(float altitude, float enemyAirDefense, ReconSatelliteParams p)
        {
            float alt = Mathf.Clamp01(altitude);
            float defense = Mathf.Clamp01(enemyAirDefense);
            return Mathf.Clamp01((1f - alt) * defense * p.riskScale);
        }

        public static float DetectionRisk(float altitude, float enemyAirDefense)
            => DetectionRisk(altitude, enemyAirDefense, ReconSatelliteParams.Default);

        /// <summary>
        /// 偵察情報の鮮度(0..1)＝再訪頻度 / (1 + 経過時間×減衰)。再訪が多く前回通過から日が浅いほど新しい。
        /// </summary>
        public static float IntelFreshness(float revisitRate, float timeSinceLastPass, ReconSatelliteParams p)
        {
            float revisit = Mathf.Clamp01(revisitRate);
            float t = Mathf.Max(0f, timeSinceLastPass);
            return Mathf.Clamp01(revisit / (1f + t * p.freshnessDecay));
        }

        public static float IntelFreshness(float revisitRate, float timeSinceLastPass)
            => IntelFreshness(revisitRate, timeSinceLastPass, ReconSatelliteParams.Default);

        /// <summary>
        /// 敵の偵察拒否(0..1)＝敵隠蔽×敵妨害。偽装で見えにくくし電子妨害で観測を潰すほど偵察が通らない。
        /// </summary>
        public static float ReconDenial(float enemyConcealment, float enemyJamming, ReconSatelliteParams p)
        {
            float conceal = Mathf.Clamp01(enemyConcealment);
            float jam = Mathf.Clamp01(enemyJamming);
            return Mathf.Clamp01(conceal * jam * p.denialScale);
        }

        public static float ReconDenial(float enemyConcealment, float enemyJamming)
            => ReconDenial(enemyConcealment, enemyJamming, ReconSatelliteParams.Default);

        /// <summary>
        /// 実効偵察価値(0..1)＝カバー率 × 解像度 × 鮮度 ×(1 - 偵察拒否)。どれか一つでも欠ければ価値は崩れる
        /// （広く覆っても粗ければ・新しくても拒否されれば偵察にならない）。p は将来拡張用（現状未使用）。
        /// </summary>
        public static float EffectiveRecon(float reconCoverage, float resolution, float intelFreshness,
            float reconDenial, ReconSatelliteParams p)
        {
            float coverage = Mathf.Clamp01(reconCoverage);
            float res = Mathf.Clamp01(resolution);
            float fresh = Mathf.Clamp01(intelFreshness);
            float denial = Mathf.Clamp01(reconDenial);
            return Mathf.Clamp01(coverage * res * fresh * (1f - denial));
        }

        public static float EffectiveRecon(float reconCoverage, float resolution, float intelFreshness, float reconDenial)
            => EffectiveRecon(reconCoverage, resolution, intelFreshness, reconDenial, ReconSatelliteParams.Default);

        /// <summary>
        /// 偵察が成立するか＝実効偵察価値が被発見/撃墜リスクを threshold ぶん上回るか
        /// （得られる情報が機を失う危険に見合うか）。
        /// </summary>
        public static bool IsReconViable(float effectiveRecon, float detectionRisk, float threshold)
        {
            float value = Mathf.Clamp01(effectiveRecon);
            float risk = Mathf.Clamp01(detectionRisk);
            return value - risk >= Mathf.Clamp01(threshold);
        }

        public static bool IsReconViable(float effectiveRecon, float detectionRisk)
            => IsReconViable(effectiveRecon, detectionRisk, ReconSatelliteParams.Default.viabilityThreshold);
    }
}
