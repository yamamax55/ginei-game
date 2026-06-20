namespace Ginei
{
    /// <summary>
    /// ネームド人物の職分ロジック（人物の「職業」＝POP 職業#110 とは別系統・純ロジック・唯一の窓口）。
    /// 人物（<see cref="Person"/>）の職分（<see cref="PersonVocation"/>）を役割・フラグから導き、<b>君主など POP 職業分類に載らない地位を別管理</b>する。
    /// POP（<see cref="Occupation"/>）からネームドへ昇格する<b>経路を保つ</b>（<see cref="PromotionVocation"/>）＝平民が名のある人物へ上がる道は残す。
    /// JSOC との対応は分析用に <see cref="JsocAnalog"/> が橋渡しする（君主は別格＝便宜上 管理 へ寄せるのみ）。test-first。
    /// </summary>
    public static class PersonVocationRules
    {
        /// <summary>
        /// 人物 → 職分。君主（元首）＞政治家＞軍人=武官＞文民（技術才≥文才で技術者・他は文官）。null は その他。
        /// 君主は <see cref="Person.isSovereign"/> でのみ立つ（POP 昇格では到達しない別格）。
        /// </summary>
        public static PersonVocation VocationOf(Person p)
        {
            if (p == null) return PersonVocation.その他;
            if (p.isSovereign) return PersonVocation.君主;
            if (p.isPolitician) return PersonVocation.政治家;
            if (p.role == PersonRole.軍人) return PersonVocation.武官;
            // 文民
            if (p.TechnicalAptitude > 0f && p.TechnicalAptitude >= p.CivilAptitude)
                return PersonVocation.技術者;
            return PersonVocation.文官;
        }

        /// <summary>君主（王・皇帝・元首）か＝POP 職業分類外の別格。</summary>
        public static bool IsRuler(PersonVocation v) => v == PersonVocation.君主;

        /// <summary>その人物が君主か（<see cref="VocationOf"/> の君主判定）。</summary>
        public static bool IsRuler(Person p) => VocationOf(p) == PersonVocation.君主;

        /// <summary>
        /// 職分の JSOC 大分類アナログ（<b>分析用の橋渡し</b>・人物の職業そのものではない）。
        /// 政治家/君主→管理／文官→事務／武官→保安／技術者→専門技術／その他→無職（対応なし）。
        /// 君主は本来 JSOC に載らない別格だが、便宜上 管理（管理的職業の apex）へ寄せる。
        /// </summary>
        public static OccupationCategory JsocAnalog(PersonVocation v)
        {
            switch (v)
            {
                case PersonVocation.君主:   return OccupationCategory.管理;
                case PersonVocation.政治家: return OccupationCategory.管理;
                case PersonVocation.文官:   return OccupationCategory.事務;
                case PersonVocation.武官:   return OccupationCategory.保安;
                case PersonVocation.技術者: return OccupationCategory.専門技術;
                default:                    return OccupationCategory.無職; // その他＝対応なし
            }
        }

        /// <summary>
        /// POP の職業プール → 昇格時に就く職分（<b>POP→ネームド昇格の経路</b>）。
        /// 軍属（保安）→武官／官吏（事務）→文官／工員・鉱員→技術者（現場叩き上げ）／農民・無職→その他（在野）。
        /// <b>君主は返さない</b>＝POP からは到達しない別格（継承#152/革命で替わる）。
        /// 正式な制度経路は士官学校#155/大学#156/科挙#156 の輩出（<c>MilitaryAcademyRules</c> 等）で、本写像はその職分の対応づけ。
        /// </summary>
        public static PersonVocation PromotionVocation(Occupation popOccupation)
        {
            switch (popOccupation)
            {
                case Occupation.軍属: return PersonVocation.武官;
                case Occupation.官吏: return PersonVocation.文官;
                case Occupation.工員: return PersonVocation.技術者;
                case Occupation.鉱員: return PersonVocation.技術者;
                case Occupation.農民: return PersonVocation.その他;
                default:             return PersonVocation.その他; // 無職＝在野
            }
        }

        /// <summary>POP からネームドへ昇格する道が開いているか（どの職業プールからも上がれる＝道は残す）。</summary>
        public static bool CanPromoteToNamed(Occupation popOccupation) => true;
    }
}
