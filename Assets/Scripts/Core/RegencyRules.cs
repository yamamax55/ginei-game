using UnityEngine;

namespace Ginei
{
    /// <summary>摂政・幼君の調整係数（エルウィン・ヨーゼフ型）。</summary>
    public readonly struct RegencyParams
    {
        /// <summary>成人年齢（これ未満は幼君＝摂政が要る）。</summary>
        public readonly int adultAge;
        /// <summary>幼君の正統性割引の最大幅（0歳のとき正統性がこれだけ削れる）。</summary>
        public readonly float minorityDiscount;
        /// <summary>摂政の実権が育つ速度（per dt）。</summary>
        public readonly float regentPowerGrowth;
        /// <summary>簒奪誘惑が危険水準とみなす閾値。</summary>
        public readonly float usurpThreshold;

        public RegencyParams(int adultAge, float minorityDiscount, float regentPowerGrowth, float usurpThreshold)
        {
            this.adultAge = Mathf.Max(1, adultAge);
            this.minorityDiscount = Mathf.Clamp01(minorityDiscount);
            this.regentPowerGrowth = Mathf.Max(0f, regentPowerGrowth);
            this.usurpThreshold = Mathf.Clamp01(usurpThreshold);
        }

        /// <summary>既定＝成人18・幼君割引0.5・摂政成長0.02・簒奪閾値0.6。</summary>
        public static RegencyParams Default => new RegencyParams(18, 0.5f, 0.02f, 0.6f);
    }

    /// <summary>
    /// 摂政・幼君の純ロジック（エルウィン・ヨーゼフ型＝未成年継承の特殊力学）。幼君の正統性は
    /// 年齢に応じて割引かれ（玉座の重みを体現できない）、統治の実務は摂政に流れて摂政の実権が時間で育つ。
    /// 君主の成人が近づくほど摂政の「今しかない」誘惑が膨らみ＝**権力の返上が最大の試金石**になる。
    /// 傀儡一般（<see cref="PowerRules"/>）とは別系統＝時限つき（成人で終わる）代理統治の力学。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RegencyRules
    {
        /// <summary>摂政が必要か＝君主が成人年齢未満。</summary>
        public static bool NeedsRegent(int monarchAge, RegencyParams p)
        {
            return monarchAge < p.adultAge;
        }

        public static bool NeedsRegent(int monarchAge) => NeedsRegent(monarchAge, RegencyParams.Default);

        /// <summary>
        /// 幼君の実効正統性（0..base）。0歳で最大割引、成人で割引消滅（年齢比例で回復）。
        /// 基準正統性は非破壊（実効値パターン）。
        /// </summary>
        public static float EffectiveLegitimacy(float baseLegitimacy, int monarchAge, RegencyParams p)
        {
            float maturity = Mathf.Clamp01(Mathf.Max(0, monarchAge) / (float)p.adultAge);
            float discount = p.minorityDiscount * (1f - maturity);
            return Mathf.Clamp01(baseLegitimacy) * (1f - discount);
        }

        public static float EffectiveLegitimacy(float baseLegitimacy, int monarchAge)
            => EffectiveLegitimacy(baseLegitimacy, monarchAge, RegencyParams.Default);

        /// <summary>
        /// 摂政の実権の1tick後の値（0..1）。摂政在任中は成長率×dt で育つ＝実務は人に付く。
        /// 君主が成人していれば育たない（返上フェーズ）。
        /// </summary>
        public static float RegentPowerTick(float regentPower, int monarchAge, float dt, RegencyParams p)
        {
            if (!NeedsRegent(monarchAge, p)) return Mathf.Clamp01(regentPower);
            return Mathf.Clamp01(Mathf.Clamp01(regentPower) + p.regentPowerGrowth * Mathf.Max(0f, dt));
        }

        public static float RegentPowerTick(float regentPower, int monarchAge, float dt)
            => RegentPowerTick(regentPower, monarchAge, dt, RegencyParams.Default);

        /// <summary>
        /// 簒奪誘惑（0..1）＝摂政の実権×野心 ambition(0..1)×成人接近度（成人が近いほど「今しかない」）。
        /// 成人済みなら returning の決断だけが残る（誘惑は満点＝返すか奪うかの瀬戸際）。
        /// </summary>
        public static float UsurpationTemptation(float regentPower, float ambition, int monarchAge, RegencyParams p)
        {
            float proximity = NeedsRegent(monarchAge, p)
                ? Mathf.Clamp01(Mathf.Max(0, monarchAge) / (float)p.adultAge)
                : 1f;
            return Mathf.Clamp01(regentPower) * Mathf.Clamp01(ambition) * proximity;
        }

        public static float UsurpationTemptation(float regentPower, float ambition, int monarchAge)
            => UsurpationTemptation(regentPower, ambition, monarchAge, RegencyParams.Default);

        /// <summary>簒奪の危険水準か＝誘惑が閾値以上（忠臣の監視・権限分割の手当てを促す警告）。</summary>
        public static bool UsurpationLooms(float regentPower, float ambition, int monarchAge, RegencyParams p)
        {
            return UsurpationTemptation(regentPower, ambition, monarchAge, p) >= p.usurpThreshold;
        }

        public static bool UsurpationLooms(float regentPower, float ambition, int monarchAge)
            => UsurpationLooms(regentPower, ambition, monarchAge, RegencyParams.Default);

        /// <summary>
        /// 円満な権力返上の確率（0..1）＝1−実権×野心（実権が育ちすぎた野心家は手放さない）。
        /// 成人時の roll∈[0,1) がこれ未満なら返上成立、以上なら簒奪・内紛へ。
        /// </summary>
        public static float HandoverChance(float regentPower, float ambition)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(regentPower) * Mathf.Clamp01(ambition));
        }

        /// <summary>返上判定（決定論）。</summary>
        public static bool HandsOverPeacefully(float regentPower, float ambition, float roll)
        {
            return roll < HandoverChance(regentPower, ambition);
        }
    }
}
