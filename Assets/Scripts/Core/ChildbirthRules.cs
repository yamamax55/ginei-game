using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 出産の純ロジック（結婚と出産システム基盤・唯一の窓口）。父（男性）と母（女性）から子の <see cref="Person"/> を生む。
    /// 子の能力は<b>両親と相関しつつばらつく</b>＝各能力を <see cref="HeredityRules"/> で個別に遺伝（中間親値＋平均回帰＋乱数）。
    /// </summary>
    /// <remarks>
    /// <b>倫理ガード：優生学NG</b>。<see cref="CanConceive"/> は能力を一切参照しない（能力で出産可否を選別しない）。
    /// 生まれた子を能力で<b>間引く/選別する API は無い</b>＝必ず生まれ、能力は <see cref="HeredityRules"/> の平均回帰＋乱数で決まる
    /// （きょうだいで違い、低能力の親から優れた子も、その逆も起こりうる）。生死・自由・成年・近親（<see cref="PersonMarriageRules.AreCloseKin"/>）のみを見る。
    /// 母集団の出生（POP）は <see cref="PopulationDynamicsRules"/>、家系の政略は <see cref="MarriageRules"/> が別途担う。決定論（roll は供給）・test-first。
    /// </remarks>
    public static class ChildbirthRules
    {
        /// <summary>出産可能年齢（母の上限。生年未設定は不問）。</summary>
        public const int MinChildbearingAge = 16;
        public const int MaxChildbearingAge = 50;

        /// <summary>母の年齢別の年間妊娠確率（出産が毎年保証されない＝加齢で妊孕性が低下する）。</summary>
        public readonly struct FertilityParams
        {
            /// <summary>ピーク年齢までの年間妊娠確率（0..1）。</summary>
            public readonly float peakChance;
            public readonly int minAge;  // これ未満は0
            public readonly int peakAge; // これまではピーク、以降は線形に低下
            public readonly int maxAge;  // これを超えると0

            public FertilityParams(float peakChance, int minAge, int peakAge, int maxAge)
            {
                this.peakChance = Mathf.Clamp01(peakChance);
                this.minAge = minAge;
                this.peakAge = Mathf.Max(minAge, peakAge);
                this.maxAge = Mathf.Max(this.peakAge, maxAge);
            }

            /// <summary>既定＝ピーク年間30%（16〜30で最高、50で0へ線形低下）。</summary>
            public static FertilityParams Default => new FertilityParams(0.30f, 16, 30, 50);
        }

        /// <summary>
        /// 母の年齢から年間妊娠確率（0..1）を返す＝ピーク年齢まで最高、以降は <see cref="FertilityParams.maxAge"/> へ向け線形に0へ低下。
        /// 範囲外・女性でない・故人/捕虜は0。生年未設定（birthYear≤0）はピーク扱い（年齢不明＝妊孕とみなす）。
        /// </summary>
        public static float ConceptionChance(Person mother, int currentYear, FertilityParams f)
        {
            if (mother == null || mother.sex != Sex.女性 || !mother.IsAvailable) return 0f;
            if (mother.BirthYear <= 0) return f.peakChance;
            int age = LifecycleRules.Age(mother, currentYear);
            if (age < f.minAge || age > f.maxAge) return 0f;
            if (age <= f.peakAge) return f.peakChance;
            float t = (float)(f.maxAge - age) / Mathf.Max(1, f.maxAge - f.peakAge);
            return Mathf.Clamp01(f.peakChance * t);
        }

        public static float ConceptionChance(Person mother, int currentYear)
            => ConceptionChance(mother, currentYear, FertilityParams.Default);

        /// <summary>母（女性）が出産可能な年齢域にあるか（生年未設定 birthYear≤0 は不問）。</summary>
        public static bool MotherInChildbearingAge(Person mother, int currentYear)
        {
            if (mother == null) return false;
            if (mother.BirthYear <= 0) return true;
            int age = LifecycleRules.Age(mother, currentYear);
            return age >= MinChildbearingAge && age <= MaxChildbearingAge;
        }

        /// <summary>
        /// 子を生せるか＝父が男性・母が女性・別人・双方存命自由・母が出産可能年齢・近親でない。
        /// <b>能力/身分は問わない（優生学NG）</b>。結婚は必須としない（基盤として婚外も許容＝呼び出し側が方針を決める）。
        /// </summary>
        public static bool CanConceive(Person father, Person mother, int currentYear)
        {
            if (father == null || mother == null || father.id == mother.id) return false;
            if (father.sex != Sex.男性 || mother.sex != Sex.女性) return false; // 生物学的な出産（婚姻の自由とは別軸）
            if (!father.IsAvailable || !mother.IsAvailable) return false;
            if (!MotherInChildbearingAge(mother, currentYear)) return false;
            if (PersonMarriageRules.AreCloseKin(father, mother)) return false;
            return true;
        }

        /// <summary>
        /// 子を出産する＝両親の各能力を <see cref="HeredityRules.InheritStat"/> で個別に遺伝させた新しい <see cref="Person"/> を返す。
        /// 性別は <paramref name="sexRoll"/>（&lt;0.5 で男性）。能力ごとに <paramref name="roll"/>() を引く（独立な乱数＝きょうだいで散る）。
        /// 勢力は父系（<paramref name="father"/> の faction）。血縁（母父id）を刻む。<see cref="CanConceive"/> を満たさなければ null。
        /// </summary>
        public static Person Conceive(Person father, Person mother, int childId, int birthYear,
            float sexRoll, Func<float> roll, HeredityRules.HeredityParams prm)
        {
            if (!CanConceive(father, mother, birthYear)) return null;
            if (roll == null) roll = () => 0.5f; // 既定＝無ノイズ（期待値）

            var child = new Person(childId, "", father.faction, PersonRole.軍人)
            {
                sex = sexRoll < 0.5f ? Sex.男性 : Sex.女性,
                birthYear = birthYear,
                fatherId = father.id,
                motherId = mother.id,
            };

            // 各能力を独立に遺伝（中間親値＋平均回帰＋乱数）＝相関しつつばらつく。
            child.leadership   = HeredityRules.InheritStat(father.leadership,   mother.leadership,   roll(), prm);
            child.attack       = HeredityRules.InheritStat(father.attack,       mother.attack,       roll(), prm);
            child.defense      = HeredityRules.InheritStat(father.defense,      mother.defense,      roll(), prm);
            child.mobility     = HeredityRules.InheritStat(father.mobility,     mother.mobility,     roll(), prm);
            child.operation    = HeredityRules.InheritStat(father.operation,    mother.operation,    roll(), prm);
            child.intelligence = HeredityRules.InheritStat(father.intelligence, mother.intelligence, roll(), prm);
            // 専門能力（テクノクラート LIFE-7）も同様に遺伝。
            child.research    = HeredityRules.InheritStat(father.research,    mother.research,    roll(), prm);
            child.engineering = HeredityRules.InheritStat(father.engineering, mother.engineering, roll(), prm);
            child.planning    = HeredityRules.InheritStat(father.planning,    mother.planning,    roll(), prm);
            child.production  = HeredityRules.InheritStat(father.production,  mother.production,  roll(), prm);

            // 財産特性（性格）はどちらかの親からランダムに受け継ぐ＋突然変異（優生学的選別でない）。
            child.financialTrait = HeredityRules.InheritFinancialTrait(father.financialTrait, mother.financialTrait, roll(), roll());

            // 劣性遺伝（マスクされた潜在能力）＝血統に埋もれた潜在を受け継ぎ、まれに子の最も強い能力域で開花する。
            // 凡庸な親からも突如 名将が生まれうる（乱数＝選別できない＝優生学NGと整合）。
            var rec = HeredityRules.RecessiveParams.Default;
            int parentMaxExpressed = MaxCore(father) > MaxCore(mother) ? MaxCore(father) : MaxCore(mother);
            int maxIdx = ArgMaxCore(child);
            int childMax = CoreAt(child, maxIdx);
            int carrier = HeredityRules.InheritRecessiveCarrier(
                father.recessiveTalent, mother.recessiveTalent, parentMaxExpressed, childMax, rec);
            int bloomed = HeredityRules.ExpressRecessive(childMax, carrier, roll(), roll(), rec, prm);
            if (bloomed != childMax) SetCore(child, maxIdx, bloomed); // 最強能力域で開花
            child.recessiveTalent = carrier;                          // 子も潜在を持ち越す（劣性として残る）

            return child;
        }

        // --- 6つの基礎能力（統率/攻撃/防御/機動/運営/情報）への添字アクセス（劣性発現の対象選択用） ---
        static int CoreAt(Person p, int i)
        {
            switch (i)
            {
                case 0: return p.leadership;
                case 1: return p.attack;
                case 2: return p.defense;
                case 3: return p.mobility;
                case 4: return p.operation;
                default: return p.intelligence;
            }
        }

        static void SetCore(Person p, int i, int v)
        {
            switch (i)
            {
                case 0: p.leadership = v; break;
                case 1: p.attack = v; break;
                case 2: p.defense = v; break;
                case 3: p.mobility = v; break;
                case 4: p.operation = v; break;
                default: p.intelligence = v; break;
            }
        }

        static int ArgMaxCore(Person p)
        {
            int idx = 0, best = CoreAt(p, 0);
            for (int i = 1; i < 6; i++) { int v = CoreAt(p, i); if (v > best) { best = v; idx = i; } }
            return idx;
        }

        static int MaxCore(Person p) => CoreAt(p, ArgMaxCore(p));

        public static Person Conceive(Person father, Person mother, int childId, int birthYear, float sexRoll, Func<float> roll)
            => Conceive(father, mother, childId, birthYear, sexRoll, roll, HeredityRules.HeredityParams.Default);

        /// <summary>
        /// 確率つき出産（年次 Tick 用）＝<see cref="CanConceive"/> かつ <paramref name="conceptionRoll"/> が
        /// <see cref="ConceptionChance"/> を下回れば子を生む、さもなくば null（その年は授からなかった）。出産は毎年保証されない。
        /// </summary>
        public static Person TryConceive(Person father, Person mother, int childId, int birthYear,
            float conceptionRoll, float sexRoll, Func<float> roll, HeredityRules.HeredityParams prm, FertilityParams fertility)
        {
            if (!CanConceive(father, mother, birthYear)) return null;
            if (conceptionRoll >= ConceptionChance(mother, birthYear, fertility)) return null;
            return Conceive(father, mother, childId, birthYear, sexRoll, roll, prm);
        }

        public static Person TryConceive(Person father, Person mother, int childId, int birthYear,
            float conceptionRoll, float sexRoll, Func<float> roll)
            => TryConceive(father, mother, childId, birthYear, conceptionRoll, sexRoll, roll,
                HeredityRules.HeredityParams.Default, FertilityParams.Default);

        /// <summary>ある人物（id）の子を名簿から拾う（母 or 父が一致）。</summary>
        public static List<Person> ChildrenOf(IEnumerable<Person> roster, int parentId)
        {
            var list = new List<Person>();
            if (roster == null || parentId < 0) return list;
            foreach (var p in roster)
                if (p != null && (p.motherId == parentId || p.fatherId == parentId)) list.Add(p);
            return list;
        }

        /// <summary>ある人物（id）の子の数。</summary>
        public static int ChildCount(IEnumerable<Person> roster, int parentId) => ChildrenOf(roster, parentId).Count;
    }
}
