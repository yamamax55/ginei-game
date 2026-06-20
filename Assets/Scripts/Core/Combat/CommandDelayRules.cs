using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮伝達遅延の調整値（命令が末端に届くまでのラグ）。規模・階層の効き、分権の緩和、麻痺の閾値。
    /// すべて ctor でクランプ。実効値パターン（基準値は別に持ち、ここは係数だけ）。
    /// </summary>
    public readonly struct CommandDelayParams
    {
        /// <summary>艦隊規模1ユニットあたりの追加遅延（秒）。大艦隊ほど末端まで時間がかかる。</summary>
        public readonly float sizeDelayScale;
        /// <summary>梯団の階層1段あたりの追加遅延（秒）。指揮系統が深いほど伝達が遅い。</summary>
        public readonly float depthDelayPerLevel;
        /// <summary>基礎遅延（秒・規模0/階層0でもかかる最小ラグ）。</summary>
        public readonly float baseDelay;
        /// <summary>遅延の上限（秒・暴走防止）。</summary>
        public readonly float maxDelay;
        /// <summary>分権の効き（下級の裁量がどれだけ遅延の影響を緩和するか・0..1）。</summary>
        public readonly float decentralizationStrength;
        /// <summary>指揮系統の質による短縮の最大割合（質1.0でこの割合ぶん短縮・0..1）。</summary>
        public readonly float qualityReliefMax;
        /// <summary>指揮麻痺とみなす実効遅延の閾値（秒）。</summary>
        public readonly float paralysisThreshold;

        public CommandDelayParams(
            float sizeDelayScale,
            float depthDelayPerLevel,
            float baseDelay,
            float maxDelay,
            float decentralizationStrength,
            float qualityReliefMax,
            float paralysisThreshold)
        {
            this.sizeDelayScale = Mathf.Max(0f, sizeDelayScale);
            this.depthDelayPerLevel = Mathf.Max(0f, depthDelayPerLevel);
            this.baseDelay = Mathf.Max(0f, baseDelay);
            this.maxDelay = Mathf.Max(0.01f, maxDelay);
            this.decentralizationStrength = Mathf.Clamp01(decentralizationStrength);
            this.qualityReliefMax = Mathf.Clamp01(qualityReliefMax);
            this.paralysisThreshold = Mathf.Max(0f, paralysisThreshold);
        }

        /// <summary>既定：1ユニット0.001s・1段0.5s・基礎0.5s・上限60s・分権0.6・質短縮0.5・麻痺20s。</summary>
        public static CommandDelayParams Default => new CommandDelayParams(
            DefaultSizeDelayScale, DefaultDepthDelayPerLevel, DefaultBaseDelay, DefaultMaxDelay,
            DefaultDecentralizationStrength, DefaultQualityReliefMax, DefaultParalysisThreshold);

        public const float DefaultSizeDelayScale = 0.001f;
        public const float DefaultDepthDelayPerLevel = 0.5f;
        public const float DefaultBaseDelay = 0.5f;
        public const float DefaultMaxDelay = 60f;
        public const float DefaultDecentralizationStrength = 0.6f;
        public const float DefaultQualityReliefMax = 0.5f;
        public const float DefaultParalysisThreshold = 20f;
    }

    /// <summary>
    /// 指揮伝達遅延の純ロジック（命令が末端に届くまでのラグ・盤面非依存）。
    /// <b>大艦隊ほど命令が末端に届くまで時間がかかり、混戦・通信妨害で遅延が増す</b>。遅延が大きいと
    /// 好機を逃し後手に回る。優れた指揮系統（質）・分権（下級の裁量）で遅延の影響を抑える。
    /// 遅延は線形/多項式で組む（Log/Exp 不使用＝終盤ラグ規律・決定論）。実効値パターン（基準値非破壊）。
    /// <para>
    /// 分担：<see cref="MissionCommandRules"/>（任務戦術＝上級は目標だけ与え下級に裁量を委ねる）とは
    /// <b>連携するが別物</b>＝こちらは「命令が届くまでの伝達ラグ」を数値化する。
    /// <see cref="BattlefieldCommandRules"/>（指揮官戦死時の臨時指揮継承）とも別系統。
    /// 分権 <see cref="DecentralizationRelief"/> は任務戦術の効果（裁量で待たずに動ける）を遅延側に映す橋渡し。
    /// </para>
    /// </summary>
    public static class CommandDelayRules
    {
        // ---- 伝達遅延（規模＋階層の深さ） ----

        /// <summary>既定パラメータで伝達遅延を返す。</summary>
        public static float TransmissionDelay(float fleetSize, int echelonDepth)
            => TransmissionDelay(fleetSize, echelonDepth, CommandDelayParams.Default);

        /// <summary>
        /// 艦隊規模と梯団の階層の深さから伝達遅延（秒）を返す。
        /// `delay = clamp(base + size*sizeScale + depth*depthPerLevel, 0, maxDelay)`。
        /// 大艦隊・深い指揮系統ほど命令が末端に届くのが遅い。
        /// </summary>
        public static float TransmissionDelay(float fleetSize, int echelonDepth, CommandDelayParams p)
        {
            float size = Mathf.Max(0f, fleetSize);
            float depth = Mathf.Max(0, echelonDepth);
            float delay = p.baseDelay + size * p.sizeDelayScale + depth * p.depthDelayPerLevel;
            return Mathf.Clamp(delay, 0f, p.maxDelay);
        }

        // ---- 通信妨害（妨害で遅延が増す） ----

        /// <summary>既定パラメータで通信妨害ぶんを上乗せした遅延を返す。</summary>
        public static float JammingPenalty(float delay, float enemyEcm)
            => JammingPenalty(delay, enemyEcm, CommandDelayParams.Default);

        /// <summary>
        /// 敵の電子戦（妨害強度 0..1）で遅延が増す。`jammed = clamp(delay*(1+ecm), 0, maxDelay)`。
        /// 妨害1.0で遅延は倍。混戦・妨害下では命令が届きにくい。
        /// </summary>
        public static float JammingPenalty(float delay, float enemyEcm, CommandDelayParams p)
        {
            float d = Mathf.Max(0f, delay);
            float ecm = Mathf.Clamp01(enemyEcm);
            return Mathf.Clamp(d * (1f + ecm), 0f, p.maxDelay);
        }

        // ---- 実効遅延（指揮系統の質で短縮） ----

        /// <summary>既定パラメータで指揮系統の質を織り込んだ実効遅延を返す。</summary>
        public static float EffectiveDelay(float transmissionDelay, float jamming, float commandQuality)
            => EffectiveDelay(transmissionDelay, jamming, commandQuality, CommandDelayParams.Default);

        /// <summary>
        /// 妨害込みの遅延（<paramref name="jamming"/> と素の <paramref name="transmissionDelay"/> の大きい方）を
        /// 指揮系統の質（0..1）で短縮した実効遅延を返す。`eff = gross*(1 - qualityReliefMax*quality)`。
        /// 優れた指揮系統ほどラグが小さい（基準値は非破壊＝ここで実効値を出す）。
        /// </summary>
        public static float EffectiveDelay(float transmissionDelay, float jamming, float commandQuality, CommandDelayParams p)
        {
            float gross = Mathf.Max(Mathf.Max(0f, transmissionDelay), Mathf.Max(0f, jamming));
            float quality = Mathf.Clamp01(commandQuality);
            float relief = 1f - p.qualityReliefMax * quality;
            return Mathf.Max(0f, gross * relief);
        }

        // ---- 分権による緩和（下級の裁量で遅延の影響を和らげる） ----

        /// <summary>既定パラメータで分権による緩和後の遅延を返す。</summary>
        public static float DecentralizationRelief(float delay, float subordinateInitiative)
            => DecentralizationRelief(delay, subordinateInitiative, CommandDelayParams.Default);

        /// <summary>
        /// 下級の裁量（0..1）で遅延の影響を緩和する（任務戦術＝届くのを待たず動ける）。
        /// `relieved = delay*(1 - decentralizationStrength*initiative)`。完全分権でも係数ぶん（既定0.6）まで。
        /// </summary>
        public static float DecentralizationRelief(float delay, float subordinateInitiative, CommandDelayParams p)
        {
            float d = Mathf.Max(0f, delay);
            float init = Mathf.Clamp01(subordinateInitiative);
            return Mathf.Max(0f, d * (1f - p.decentralizationStrength * init));
        }

        // ---- 反応の遅れ・好機喪失・命令の陳腐化・指揮麻痺 ----

        /// <summary>敵の動きへの反応の遅れ（秒）＝実効遅延（負はクランプ）。</summary>
        public static float ReactionLag(float effectiveDelay)
            => Mathf.Max(0f, effectiveDelay);

        /// <summary>
        /// 反応の遅れで好機を逃す度合い（0..1）。`missed = clamp01(reactionLag / opportunityWindow)`。
        /// 反応の遅れが好機の窓に対して大きいほど取り逃す。窓0以下＝瞬間的好機は遅れがあれば全逃し。
        /// </summary>
        public static float MissedOpportunity(float reactionLag, float opportunityWindow)
        {
            float lag = Mathf.Max(0f, reactionLag);
            if (opportunityWindow <= 0f) return lag > 0f ? 1f : 0f;
            return Mathf.Clamp01(lag / opportunityWindow);
        }

        /// <summary>
        /// 命令が届く頃には状況が変わり陳腐化している度合い（0..1）。
        /// `obsolescence = clamp01(delay * situationChangeRate)`。状況変化が速い＝遅延が陳腐化を招く。
        /// </summary>
        public static float OrderObsolescence(float delay, float situationChangeRate)
        {
            float d = Mathf.Max(0f, delay);
            float rate = Mathf.Max(0f, situationChangeRate);
            return Mathf.Clamp01(d * rate);
        }

        /// <summary>既定の閾値で指揮麻痺かを返す。</summary>
        public static bool IsCommandParalyzed(float effectiveDelay)
            => IsCommandParalyzed(effectiveDelay, CommandDelayParams.DefaultParalysisThreshold);

        /// <summary>実効遅延が閾値以上なら指揮麻痺（命令が機能しない＝後手に回り続ける）。</summary>
        public static bool IsCommandParalyzed(float effectiveDelay, float threshold)
            => Mathf.Max(0f, effectiveDelay) >= Mathf.Max(0f, threshold);
    }
}
