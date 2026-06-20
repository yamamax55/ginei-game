namespace Ginei
{
    /// <summary>
    /// 人物どうしの結婚の純ロジック（結婚と出産システム基盤・唯一の窓口）。家どうしの政略結婚（<see cref="MarriageRules"/>＝
    /// 同盟結束/請求権 #647）とは別＝<b>個人の婚姻関係</b>（<see cref="Person.spouseId"/>）を結ぶ。
    /// </summary>
    /// <remarks>
    /// <b>倫理ガード：能力・身分で結婚を縛らない＝優生学NG</b>。<see cref="CanMarry"/> は能力（統率/攻撃…）を一切参照しない
    /// （「優秀な者だけ結婚させる」品種改良はできない）。生死・自由・成年・近親（<see cref="AreCloseKin"/>）のみを見る。
    /// 子の能力の遺伝は <see cref="HeredityRules"/>、出産は <see cref="ChildbirthRules"/>。決定論・test-first。
    /// </remarks>
    public static class PersonMarriageRules
    {
        /// <summary>結婚できる最少年齢（成年＝<c>RoyalEducationRules.MajorityAge</c> と同値）。</summary>
        public const int MinMarriageAge = 16;

        /// <summary>成年か（生年未設定 birthYear≤0 は年齢不問＝ブロックしない）。</summary>
        public static bool IsAdult(Person p, int currentYear)
            => p != null && (p.BirthYear <= 0 || LifecycleRules.Age(p, currentYear) >= MinMarriageAge);

        /// <summary>独身か（存命・自由かつ配偶者なし）。</summary>
        public static bool IsSingle(Person p) => p != null && p.IsAvailable && p.spouseId < 0;

        /// <summary>近親か（親子・きょうだい＝同じ親を持つ）。近親婚を禁じる倫理ガード。</summary>
        public static bool AreCloseKin(Person a, Person b)
        {
            if (a == null || b == null || a.id == b.id) return false;
            // 親子
            if (a.id == b.motherId || a.id == b.fatherId) return true;
            if (b.id == a.motherId || b.id == a.fatherId) return true;
            // きょうだい（少なくとも一方の親を共有）
            if (a.motherId >= 0 && a.motherId == b.motherId) return true;
            if (a.fatherId >= 0 && a.fatherId == b.fatherId) return true;
            return false;
        }

        /// <summary>
        /// 結婚できるか＝別人・双方存命自由・双方独身・双方成年・近親でない。<b>能力/身分は問わない（優生学NG）</b>。
        /// </summary>
        public static bool CanMarry(Person a, Person b, int currentYear)
        {
            if (a == null || b == null || a.id == b.id) return false;
            if (!IsSingle(a) || !IsSingle(b)) return false;
            if (!IsAdult(a, currentYear) || !IsAdult(b, currentYear)) return false;
            if (AreCloseKin(a, b)) return false;
            return true;
        }

        /// <summary>結婚させる（相互に配偶者を設定）。条件を満たさなければ false。</summary>
        public static bool Marry(Person a, Person b, int currentYear)
        {
            if (!CanMarry(a, b, currentYear)) return false;
            a.spouseId = b.id;
            b.spouseId = a.id;
            return true;
        }

        /// <summary>互いに配偶者として結ばれているか。</summary>
        public static bool AreMarried(Person a, Person b)
            => a != null && b != null && a.id != b.id && a.spouseId == b.id && b.spouseId == a.id;

        /// <summary>離婚（相互の配偶者を解除）。結ばれていなければ false。</summary>
        public static bool Divorce(Person a, Person b)
        {
            if (!AreMarried(a, b)) return false;
            a.spouseId = -1;
            b.spouseId = -1;
            return true;
        }

        /// <summary>死別＝生存配偶者の婚姻を解除する（故人側は問わない・<see cref="LifecycleRules"/> 死亡時に呼ぶ想定）。</summary>
        public static void Widow(Person survivor)
        {
            if (survivor != null) survivor.spouseId = -1;
        }
    }
}
