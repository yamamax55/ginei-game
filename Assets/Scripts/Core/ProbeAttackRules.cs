using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 威力偵察（武力偵察）の純ロジック。
    /// 本格攻撃の前に小部隊で攻撃を仕掛け、敵の火力配置・予備の位置・弱点を炙り出す。
    /// 情報を得られるが、本隊の攻撃意図を悟られ、威力偵察部隊は損害を受ける。
    ///
    /// 分担（重複実装しない）：
    /// - ReconRules（受動的偵察＝見て探る）とは別＝こちらは攻撃で能動的に敵の反応を引き出す。
    /// - FeignedRetreatRules（偽装退却で敵を誘い出す）/ TacticalSurpriseRules（不意打ちの戦術効果）
    ///   とは別＝こちらは「小規模攻撃で配置と弱点を露見させる情報行為」を解く。
    /// 盤面非依存の plain 引数・乱数なし・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class ProbeAttackRules
    {
        /// <summary>威力偵察の係数。トップレベル readonly struct・全 Clamp。</summary>
        public readonly struct ProbeAttackParams
        {
            /// <summary>威力偵察が敵の反応を引き出す効き（露出度の利き）。</summary>
            public readonly float exposureScale;
            /// <summary>露出度→得られる情報量の換算係数。</summary>
            public readonly float intelScale;
            /// <summary>得た情報が弱点判明へ寄与する効き。</summary>
            public readonly float weakPointScale;
            /// <summary>敵火力が威力偵察部隊に与える損害の効き。</summary>
            public readonly float lossScale;
            /// <summary>偵察規模・方向の集中が本隊意図を悟らせる効き。</summary>
            public readonly float telegraphScale;
            /// <summary>露出度→敵予備の引き出し（あぶり出し）の効き。</summary>
            public readonly float reserveFlushScale;
            /// <summary>正味価値で損害・意図露見を差し引く重み。</summary>
            public readonly float netCostWeight;

            public ProbeAttackParams(
                float exposureScale,
                float intelScale,
                float weakPointScale,
                float lossScale,
                float telegraphScale,
                float reserveFlushScale,
                float netCostWeight)
            {
                this.exposureScale = Mathf.Max(0f, exposureScale);
                this.intelScale = Mathf.Max(0f, intelScale);
                this.weakPointScale = Mathf.Max(0f, weakPointScale);
                this.lossScale = Mathf.Max(0f, lossScale);
                this.telegraphScale = Mathf.Max(0f, telegraphScale);
                this.reserveFlushScale = Mathf.Max(0f, reserveFlushScale);
                this.netCostWeight = Mathf.Max(0f, netCostWeight);
            }

            public static ProbeAttackParams Default => new ProbeAttackParams(
                exposureScale: 1.0f,
                intelScale: 1.0f,
                weakPointScale: 1.0f,
                lossScale: 0.5f,
                telegraphScale: 1.0f,
                reserveFlushScale: 1.0f,
                netCostWeight: 0.5f);
        }

        /// <summary>
        /// 威力偵察が敵の反応を引き出す度合い（露出度 0..1）。
        /// 偵察兵力（規模 0..1）× 敵の反応（応射・予備投入の積極性 0..1）× 利き。
        /// 強く突くほど、また敵が積極的に応じるほど、敵の手の内が露わになる。
        /// </summary>
        public static float ProbeExposure(float probeStrength, float enemyResponse, ProbeAttackParams p)
        {
            float s = Mathf.Clamp01(probeStrength);
            float r = Mathf.Clamp01(enemyResponse);
            return Mathf.Clamp01(s * r * p.exposureScale);
        }

        public static float ProbeExposure(float probeStrength, float enemyResponse)
            => ProbeExposure(probeStrength, enemyResponse, ProbeAttackParams.Default);

        /// <summary>
        /// 敵配置・弱点について得られる情報（0..1）。
        /// 露出度 × 観測の質（索敵・記録能力 0..1）× 利き＝引き出した反応を観て取れたぶんだけ情報になる。
        /// </summary>
        public static float IntelGathered(float probeExposure, float observationQuality, ProbeAttackParams p)
        {
            float e = Mathf.Clamp01(probeExposure);
            float q = Mathf.Clamp01(observationQuality);
            return Mathf.Clamp01(e * q * p.intelScale);
        }

        public static float IntelGathered(float probeExposure, float observationQuality)
            => IntelGathered(probeExposure, observationQuality, ProbeAttackParams.Default);

        /// <summary>
        /// 敵の弱点（火力の薄い箇所）の判明度（0..1）。
        /// 得た情報 ×(1 − 防御の均一さ)× 利き＝防御がムラがあるほど薄い箇所が浮かび上がる。
        /// 均一（uniformity=1）なら付け入る隙が見えず 0 に近づく。
        /// </summary>
        public static float WeakPointRevealed(float intelGathered, float enemyDefenseUniformity, ProbeAttackParams p)
        {
            float i = Mathf.Clamp01(intelGathered);
            float unevenness = 1f - Mathf.Clamp01(enemyDefenseUniformity);
            return Mathf.Clamp01(i * unevenness * p.weakPointScale);
        }

        public static float WeakPointRevealed(float intelGathered, float enemyDefenseUniformity)
            => WeakPointRevealed(intelGathered, enemyDefenseUniformity, ProbeAttackParams.Default);

        /// <summary>
        /// 威力偵察部隊が払う損害（0..1）。
        /// 偵察兵力（さらした規模 0..1）× 敵火力（0..1）× 利き＝深く突くほど、敵が強いほど血を流す。
        /// </summary>
        public static float ProbeLosses(float probeStrength, float enemyFirepower, ProbeAttackParams p)
        {
            float s = Mathf.Clamp01(probeStrength);
            float f = Mathf.Clamp01(enemyFirepower);
            return Mathf.Clamp01(s * f * p.lossScale);
        }

        public static float ProbeLosses(float probeStrength, float enemyFirepower)
            => ProbeLosses(probeStrength, enemyFirepower, ProbeAttackParams.Default);

        /// <summary>
        /// 偵察で本隊の攻撃意図を悟られる度合い（0..1）。
        /// 偵察規模（0..1）× 方向の指向性（0..1＝本命方向に集中するほど読まれる）× 利き。
        /// 大兵力を本命方向へ向けるほど「ここが主攻」と見抜かれる。
        /// </summary>
        public static float IntentionTelegraphed(float probeScale, float probeDirection, ProbeAttackParams p)
        {
            float s = Mathf.Clamp01(probeScale);
            float d = Mathf.Clamp01(probeDirection);
            return Mathf.Clamp01(s * d * p.telegraphScale);
        }

        public static float IntentionTelegraphed(float probeScale, float probeDirection)
            => IntentionTelegraphed(probeScale, probeDirection, ProbeAttackParams.Default);

        /// <summary>
        /// 敵の予備を引っ張り出して位置を暴く度合い（0..1）。
        /// 露出度 × 利き＝偵察に応じて敵が予備を動かすほど、その位置が露見する。
        /// </summary>
        public static float ReserveFlushing(float probeExposure, ProbeAttackParams p)
        {
            float e = Mathf.Clamp01(probeExposure);
            return Mathf.Clamp01(e * p.reserveFlushScale);
        }

        public static float ReserveFlushing(float probeExposure)
            => ReserveFlushing(probeExposure, ProbeAttackParams.Default);

        /// <summary>
        /// 威力偵察の正味価値（−1..1）。
        /// 得た情報 − (損害 + 意図露見)× コスト重み＝情報の利得から、流した血と読まれた不利を差し引く。
        /// 正なら割に合う偵察、負なら情報以上の代償を払った偵察。
        /// </summary>
        public static float ProbeNetValue(float intelGathered, float probeLosses, float intentionTelegraphed, ProbeAttackParams p)
        {
            float gain = Mathf.Clamp01(intelGathered);
            float loss = Mathf.Clamp01(probeLosses);
            float tele = Mathf.Clamp01(intentionTelegraphed);
            float net = gain - (loss + tele) * p.netCostWeight;
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float ProbeNetValue(float intelGathered, float probeLosses, float intentionTelegraphed)
            => ProbeNetValue(intelGathered, probeLosses, intentionTelegraphed, ProbeAttackParams.Default);

        /// <summary>
        /// 攻撃すべき弱点を見つけたか（弱点判明度が閾値以上）。
        /// </summary>
        public static bool IsWeakPointFound(float weakPointRevealed, float threshold)
        {
            return Mathf.Clamp01(weakPointRevealed) >= Mathf.Clamp01(threshold);
        }
    }
}
