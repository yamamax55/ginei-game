using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 英雄時代／英雄なき時代の純ロジック（#英雄時代・キングダム＝王騎の「英雄の時代」）。
    /// 世界の英雄度（0..1）が、<b>個の将才が戦を決めるか・数と組織が戦を決めるか</b>の天秤を傾ける：
    /// ・英雄時代＝傑物の能力差が戦場で増幅し（一騎当千・寡兵で大軍を覆す）、軍神も生まれやすい。
    /// ・英雄なき時代＝個の差は薄れ、数（ランチェスター二乗則）・兵站・組織が支配し、英雄は稀。
    /// 時代は英雄の在不在で漂流する（六大将軍が死に絶えれば英雄なき時代へ、新世代が興れば英雄時代へ）。
    /// 数式は既存窓口（<see cref="CombatModifiers"/>/<see cref="LanchesterRules"/>/<see cref="TenchijinRules"/>）を流用し
    /// 増幅・減衰の係数だけを足す＝二重実装しない。実効値パターン・test-first。
    /// </summary>
    public static class HeroicAgeRules
    {
        /// <summary>英雄時代とみなす英雄度の下限。</summary>
        public const float HeroicThreshold = 0.66f;
        /// <summary>英雄なき時代とみなす英雄度の上限（これ未満）。</summary>
        public const float HerolessThreshold = 0.33f;

        /// <summary>英雄時代で個の将才差を増幅する最大倍率（英雄度1で edge×これ）。</summary>
        public const float HeroInfluenceMax = 1.5f;
        /// <summary>英雄なき時代で数（ランチェスター指数）を強める最大倍率（英雄度0でこれ）。</summary>
        public const float MassInfluenceMax = 1.3f;

        /// <summary>英雄を英雄たらしめる将才のしきい値（平均武勲）。これ以上か軍神なら英雄。</summary>
        public const int HeroAbilityThreshold = 90;
        /// <summary>この割合の指揮官が英雄なら英雄時代が満ちる（一握りの傑物が時代を定義する）。</summary>
        public const float ReferenceHeroDensity = 0.15f;

        /// <summary>英雄度→時代局面。</summary>
        public static HeroicEra EraFor(float heroism)
        {
            float h = Mathf.Clamp01(heroism);
            if (h >= HeroicThreshold) return HeroicEra.英雄時代;
            if (h >= HerolessThreshold) return HeroicEra.移行期;
            return HeroicEra.英雄なき時代;
        }

        /// <summary>個の将才差の増幅係数（英雄なき時代1.0〜英雄時代 <see cref="HeroInfluenceMax"/>）。</summary>
        public static float HeroInfluenceFactor(float heroism)
            => Mathf.Lerp(1f, HeroInfluenceMax, Mathf.Clamp01(heroism));

        /// <summary>数（量）の支配係数（英雄時代1.0〜英雄なき時代 <see cref="MassInfluenceMax"/>）。英雄度の逆。</summary>
        public static float MassInfluenceFactor(float heroism)
            => Mathf.Lerp(MassInfluenceMax, 1f, Mathf.Clamp01(heroism));

        /// <summary>軍神/英雄の登場しやすさ補正（英雄なき時代0.5〜英雄時代1.5＝英雄が英雄を生む）。</summary>
        public static float EmergenceMultiplier(float heroism)
            => Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(heroism));

        /// <summary>
        /// 時代を加味した能力倍率。<see cref="CombatModifiers.AbilityFactor"/> の基準1.0からの「ずれ（将才の差）」だけを
        /// 英雄度で増幅する＝凡将（能力50＝ずれ0）は時代に左右されず、傑物ほど英雄時代に冴え、無能ほど露呈する。数式は流用。
        /// </summary>
        public static float EraAdjustedAbilityFactor(float effectiveStat, float heroism)
        {
            float edge = CombatModifiers.AbilityFactor(effectiveStat) - 1f; // 将才の差（±）
            return 1f + edge * HeroInfluenceFactor(heroism);
        }

        /// <summary>
        /// 時代を加味したランチェスター指数。英雄なき時代ほど指数が大きく数の集中が二乗で効く（量が支配）。
        /// 既存の指数を <see cref="MassInfluenceFactor"/> で伸縮するだけ（ConcentrationFactor の数式は流用）。
        /// </summary>
        public static float EraAdjustedLanchesterExponent(float baseExponent, float heroism)
            => Mathf.Max(0f, baseExponent) * MassInfluenceFactor(heroism);

        /// <summary>時代を加味したランチェスター係数パラメータ（指数だけ伸縮・min/maxは据え置き）。</summary>
        public static LanchesterParams EraAdjustedLanchesterParams(LanchesterParams baseParams, float heroism)
            => new LanchesterParams(EraAdjustedLanchesterExponent(baseParams.exponent, heroism), baseParams.minFactor, baseParams.maxFactor);

        /// <summary>この提督は英雄か（軍神＝<see cref="AdmiralData.isTranscendent"/> か、平均武勲がしきい値以上）。</summary>
        public static bool IsHero(AdmiralData admiral)
        {
            if (admiral == null) return false;
            if (admiral.isTranscendent) return true;
            return MartialAverage(admiral) >= HeroAbilityThreshold;
        }

        /// <summary>平均武勲（統率/攻撃/防御/機動の実効値の平均）。</summary>
        public static float MartialAverage(AdmiralData admiral)
        {
            if (admiral == null) return 0f;
            return (admiral.EffectiveLeadership + admiral.EffectiveAttack
                  + admiral.EffectiveDefense + admiral.EffectiveMobility) / 4f;
        }

        /// <summary>英雄密度（英雄数/全指揮官数）。総数0以下は0。</summary>
        public static float HeroDensity(int heroCount, int totalCommanders)
        {
            if (totalCommanders <= 0) return 0f;
            return Mathf.Clamp01((float)heroCount / totalCommanders);
        }

        /// <summary>英雄密度→目標英雄度（参照密度で1へ飽和＝一握りの傑物が時代を満たす）。</summary>
        public static float HeroismTarget(float heroDensity)
            => Mathf.Clamp01(Mathf.Clamp01(heroDensity) / Mathf.Max(0.0001f, ReferenceHeroDensity));

        /// <summary>
        /// 英雄度を目標へ漸近させる（英雄が興れば英雄時代へ、死に絶えれば英雄なき時代へ）。
        /// 1tickの寄り幅は rate×dt をクランプ（オーバーシュート防止）。基準収束パターン（GovernanceRules 流儀）。
        /// </summary>
        public static float Drift(float currentHeroism, float targetHeroism, float rate, float dt)
        {
            float cur = Mathf.Clamp01(currentHeroism);
            float tgt = Mathf.Clamp01(targetHeroism);
            float t = Mathf.Clamp01(Mathf.Max(0f, rate) * Mathf.Max(0f, dt));
            return Mathf.Clamp01(Mathf.Lerp(cur, tgt, t));
        }
    }
}
