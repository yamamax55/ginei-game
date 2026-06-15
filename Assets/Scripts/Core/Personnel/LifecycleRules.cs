using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人物ライフサイクルの純ロジック（LIFE-1 年齢／LIFE-2 死亡 #151/#152・唯一の窓口）。生年からの<b>年齢導出</b>と、
    /// 年齢に応じた<b>死亡率カーブ</b>・寿命判定を扱う。死亡は <see cref="Person.deathYear"/> を立てるだけ（席の空席化・
    /// 後任補充は <see cref="VacancyRules"/>）。乱数は呼び出し側が roll(0..1) を渡す＝決定論的にテストできる。test-first。
    /// </summary>
    public static class LifecycleRules
    {
        /// <summary>寿命・死亡率の調整値。</summary>
        public readonly struct LifespanParams
        {
            /// <summary>平時の基礎年間死亡率（若年・壮年）。</summary>
            public readonly float baseMortality;
            /// <summary>これを超えると死亡率が上がり始める年齢。</summary>
            public readonly int onsetAge;
            /// <summary>onsetAge 超過1歳あたりの年間死亡率の増分。</summary>
            public readonly float slope;

            public LifespanParams(float baseMortality, int onsetAge, float slope)
            {
                this.baseMortality = Mathf.Max(0f, baseMortality);
                this.onsetAge = onsetAge;
                this.slope = Mathf.Max(0f, slope);
            }

            /// <summary>既定＝基礎0.5%/年・60歳から+2%/年（80歳で約40%、90歳で約60%）。</summary>
            public static LifespanParams Default => new LifespanParams(0.005f, 60, 0.02f);
        }

        /// <summary>年齢（生年から導出。生年0=未設定なら0）。負にならない。</summary>
        public static int Age(int birthYear, int currentYear)
        {
            if (birthYear <= 0) return 0;
            return Mathf.Max(0, currentYear - birthYear);
        }

        /// <summary>人物の年齢（<see cref="ICharacter.BirthYear"/> から導出）。</summary>
        public static int Age(ICharacter c, int currentYear)
            => c == null ? 0 : Age(c.BirthYear, currentYear);

        /// <summary>年齢に応じた年間死亡率（0..1）。onsetAge までは基礎、以降は線形に上昇。</summary>
        public static float AnnualMortality(int age, LifespanParams p)
        {
            float rate = p.baseMortality;
            if (age > p.onsetAge) rate += (age - p.onsetAge) * p.slope;
            return Mathf.Clamp01(rate);
        }

        /// <summary>このターン老衰死するか（roll∈[0,1) が per-turn 死亡率を下回れば死亡）。yearsPerTurn でターン換算。</summary>
        public static bool ShouldDieOfAge(int age, float roll, int yearsPerTurn, LifespanParams p)
        {
            float annual = AnnualMortality(age, p);
            float perTurn = Mathf.Clamp01(annual * Mathf.Max(1, yearsPerTurn));
            return roll < perTurn;
        }

        /// <summary>人物を死亡させる（没年を立てる。既に故人なら false）。席の空席化は <see cref="VacancyRules"/> が行う。</summary>
        public static bool Kill(Person person, int deathYear)
        {
            if (person == null || person.IsDeceased) return false;
            person.deathYear = deathYear;
            return true;
        }
    }
}
