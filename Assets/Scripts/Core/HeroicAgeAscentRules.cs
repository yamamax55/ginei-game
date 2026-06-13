using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 英雄時代へ至る道程の局面（#英雄時代＝至るまでの仕組み・キングダム「乱世が英雄を生む」）。
    /// 泰平（戦なき世）→胎動（乱世が兆し英雄の種が芽吹く）→群雄（乱世が満ち傑物が雲のごとく興る）→英雄時代（個が戦を決める）。
    /// </summary>
    public enum AscentStage { 泰平, 胎動, 群雄, 英雄時代 }

    /// <summary>
    /// 英雄時代に「至るまで」の純ロジック（#英雄時代の上流＝原因の側）。
    /// <see cref="HeroicAgeRules"/> が「英雄度が戦闘に何を及ぼすか（結果）」を扱うのに対し、ここは
    /// <b>なぜ英雄度が上がるのか＝乱世（turmoil）が英雄を生み、世代が興り、時代が満ちる</b>過程を扱う。
    /// 乱世（戦乱＋不安定）が上流の原因＝英雄の輩出圧力を生み、英雄度を英雄時代へ引き上げる目標（<see cref="AscentTarget"/>）を作る。
    /// 収束そのものは <see cref="HeroicAgeRules.Drift"/> が担う（二重実装しない）。決定論・test-first・後方互換。
    /// </summary>
    public static class HeroicAgeAscentRules
    {
        /// <summary>乱世＝戦乱の重み（残りが不安定さの重み）。</summary>
        public const float WarWeight = 0.6f;

        /// <summary>胎動（英雄時代の兆し）が始まる乱世度。</summary>
        public const float StirringTurmoil = 0.4f;

        /// <summary>群雄割拠（傑物が雲のごとく興る）に入る乱世度。</summary>
        public const float ContendingTurmoil = 0.6f;

        /// <summary>至る目標で「実現した英雄（密度）」が占める重み（残りが乱世の先行ぶん）。</summary>
        public const float RealizedWeight = 0.6f;

        /// <summary>乱世度（0..1）。戦乱と不安定さから合成＝戦が絶えず世が乱れるほど高い。</summary>
        public static float Turmoil(float warIntensity, float instability)
            => Mathf.Clamp01(WarWeight * Mathf.Clamp01(warIntensity) + (1f - WarWeight) * Mathf.Clamp01(instability));

        /// <summary>
        /// 英雄の輩出圧力。乱世が英雄を生み（土壌）、すでに興った英雄が次代を呼ぶ（<see cref="HeroicAgeRules.EmergenceMultiplier"/>）。
        /// 乱世0なら0＝泰平は英雄を生まない。0..1.5 程度の「圧力」（確率ではない）。
        /// </summary>
        public static float EmergencePressure(float turmoil, float heroism)
            => Mathf.Clamp01(turmoil) * HeroicAgeRules.EmergenceMultiplier(heroism);

        /// <summary>新たな英雄（軍神含む）が興る確率（決定論・基礎率×輩出圧力をクランプ）。</summary>
        public static float HeroEmergenceChance(float turmoil, float heroism, float baseRate)
            => Mathf.Clamp01(Mathf.Max(0f, baseRate) * EmergencePressure(turmoil, heroism));

        /// <summary>この tick で新たな英雄が興るか（roll∈[0,1) を外から注入＝決定論）。泰平では興らない。</summary>
        public static bool Emerges(float turmoil, float heroism, float baseRate, float roll)
            => roll < HeroEmergenceChance(turmoil, heroism, baseRate);

        /// <summary>
        /// 英雄度が向かう目標。実現した英雄（密度→<see cref="HeroicAgeRules.HeroismTarget"/>）と乱世の先行ぶんを混ぜる。
        /// 乱世だけでも胎動（移行期）まで持ち上がるが、英雄時代が満ちるには実際の英雄輩出（密度）が要る。
        /// </summary>
        public static float AscentTarget(float turmoil, float heroDensity)
        {
            float realized = HeroicAgeRules.HeroismTarget(heroDensity);
            return Mathf.Clamp01(RealizedWeight * realized + (1f - RealizedWeight) * Mathf.Clamp01(turmoil));
        }

        /// <summary>至る局面（英雄度＝結果・乱世＝原因の両面で判定）。英雄度が満ちれば英雄時代、乱世の段で胎動/群雄。</summary>
        public static AscentStage StageFor(float heroism, float turmoil)
        {
            if (Mathf.Clamp01(heroism) >= HeroicAgeRules.HeroicThreshold) return AscentStage.英雄時代;
            float tu = Mathf.Clamp01(turmoil);
            if (tu >= ContendingTurmoil) return AscentStage.群雄;
            if (tu >= StirringTurmoil) return AscentStage.胎動;
            return AscentStage.泰平;
        }

        /// <summary>英雄時代の胎動か（乱世が兆したが、まだ時代は満ちていない＝至る途上の先行シグナル）。</summary>
        public static bool IsStirring(float heroism, float turmoil)
            => StageFor(heroism, turmoil) == AscentStage.胎動;
    }
}
