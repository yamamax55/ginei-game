using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 世代交代を1年ぶん回す純ロジック（結婚・出産の配線オーケストレータ）。名簿（ネームド人物）の中で
    /// <b>成年が結婚し、夫婦が子をなし、子が名簿に加わる</b>＝年を追って血統が更新される。死（老衰）は
    /// <see cref="AnnualLifecycleRules.ProcessMortality"/> が別途担い、本ルールは婚姻と出産のみを進める。
    /// </summary>
    /// <remarks>
    /// 婚姻は <see cref="PersonMarriageRules"/>、受胎/遺伝は <see cref="ChildbirthRules"/>/<see cref="HeredityRules"/> へ委譲（数値は二重実装しない）。
    /// <b>倫理ガード（優生学NG）は委譲先で担保</b>＝結婚/出産は能力で選別しない・能力は平均回帰＋ばらつき・劣性のまれな開花。
    /// <b>スケーラビリティ</b>＝名簿上限 <see cref="GenerationParams.maxRosterSize"/> で出産を打ち切る（無制限増加＝終盤ラグを防ぐ）。決定論（roll/id 供給）・test-first。
    /// </remarks>
    public static class GenerationTickRules
    {
        /// <summary>世代交代の調整値。</summary>
        public readonly struct GenerationParams
        {
            /// <summary>独身の成年が1年で結婚に至る確率。</summary>
            public readonly float marriageChance;
            /// <summary>名簿の上限（これ以上は出産を止める＝終盤ラグ回避）。</summary>
            public readonly int maxRosterSize;

            public GenerationParams(float marriageChance, int maxRosterSize)
            {
                this.marriageChance = Mathf.Clamp01(marriageChance);
                this.maxRosterSize = Mathf.Max(0, maxRosterSize);
            }

            /// <summary>既定＝結婚40%/年・名簿上限150。</summary>
            public static GenerationParams Default => new GenerationParams(0.4f, 150);
        }

        /// <summary>1年の世代交代で起きた件数。</summary>
        public struct GenerationResult
        {
            public int marriages;
            public int births;
        }

        /// <summary>
        /// 名簿の世代交代を1年進める：①独身の成年女性を同勢力の独身成年男性と縁組み（確率 <see cref="GenerationParams.marriageChance"/>）、
        /// ②既婚で出産可能な母ごとに確率つき受胎（<see cref="ChildbirthRules.TryConceive"/> 相当）→新生児を名簿へ追加。新生児はその年は親に数えない。
        /// </summary>
        public static GenerationResult TickYear(List<Person> roster, int currentYear, Func<int> nextId, Func<float> roll,
            GenerationParams prm, ChildbirthRules.FertilityParams fertility, HeredityRules.HeredityParams heredity)
        {
            var res = default(GenerationResult);
            if (roster == null) return res;
            if (roll == null) roll = () => 0.5f;
            if (nextId == null) nextId = () => -1;

            int count = roster.Count; // この年の対象は走査開始時点の名簿（新生児・新規婚は当年に再処理しない）

            // ① 結婚：独身の成年女性ごとに、同勢力の独身成年男性と縁を結ぶ。
            for (int i = 0; i < count; i++)
            {
                Person f = roster[i];
                if (f == null || f.sex != Sex.女性) continue;
                if (!PersonMarriageRules.IsSingle(f) || !PersonMarriageRules.IsAdult(f, currentYear)) continue;
                if (roll() >= prm.marriageChance) continue; // 今年は縁がない

                Person groom = null;
                for (int j = 0; j < count; j++)
                {
                    Person m = roster[j];
                    if (m == null || m.sex != Sex.男性 || m.faction != f.faction) continue;
                    if (!PersonMarriageRules.IsSingle(m) || !PersonMarriageRules.IsAdult(m, currentYear)) continue;
                    if (PersonMarriageRules.AreCloseKin(f, m)) continue;
                    groom = m;
                    break;
                }
                if (groom != null && PersonMarriageRules.Marry(f, groom, currentYear)) res.marriages++;
            }

            // ② 出産：既婚で出産可能な母ごとに確率つき受胎。新生児は走査後にまとめて追加。
            var newborns = new List<Person>();
            for (int i = 0; i < count; i++)
            {
                if (roster.Count + newborns.Count >= prm.maxRosterSize) break; // 上限で打ち切り（ラグ回避）
                Person mother = roster[i];
                if (mother == null || mother.sex != Sex.女性 || mother.spouseId < 0 || !mother.IsAvailable) continue;
                Person father = FindById(roster, mother.spouseId);
                if (father == null || !ChildbirthRules.CanConceive(father, mother, currentYear)) continue;
                if (roll() >= ChildbirthRules.ConceptionChance(mother, currentYear, fertility)) continue; // 今年は授からなかった

                Person child = ChildbirthRules.Conceive(father, mother, nextId(), currentYear, roll(), roll, heredity);
                if (child != null) { newborns.Add(child); res.births++; }
            }
            roster.AddRange(newborns);
            return res;
        }

        public static GenerationResult TickYear(List<Person> roster, int currentYear, Func<int> nextId, Func<float> roll)
            => TickYear(roster, currentYear, nextId, roll, GenerationParams.Default,
                ChildbirthRules.FertilityParams.Default, HeredityRules.HeredityParams.Default);

        static Person FindById(List<Person> roster, int id)
        {
            if (id < 0) return null;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].id == id) return roster[i];
            return null;
        }
    }
}
