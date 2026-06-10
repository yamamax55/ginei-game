using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人物システムの純ロジック（適材適所＝正名 #866 の数値解決・唯一の窓口）。
    /// 役職(<see cref="PostType"/>)に対する人物の適性を、役割(<see cref="PersonRole"/>)の一致で割り引いて実効力にする：
    /// 軍人を軍務に・文民を政務に就ければ満額、ミスマッチ（軍人を政務／文民を軍務）は <see cref="PersonParams.mismatchPenalty"/> 倍に減衰。
    /// 「名（役割）と実（役職）が一致してこそ力が出る」＝正名。配属の最適化（<see cref="BestFor"/>）も提供。
    /// 基準値（Person の能力フィールド）は非破壊＝読み取り時に実効力を合成する（実効値パターン）。test-first。
    /// </summary>
    public static class PersonRules
    {
        /// <summary>役職適性の調整値。</summary>
        public readonly struct PersonParams
        {
            /// <summary>役割と役職が不一致のときの実効力倍率（0..1。0=全く役に立たない／1=ペナルティ無し）。</summary>
            public readonly float mismatchPenalty;

            public PersonParams(float mismatchPenalty)
            {
                this.mismatchPenalty = Mathf.Clamp01(mismatchPenalty);
            }

            /// <summary>既定＝ミスマッチは半減（0.5）。</summary>
            public static PersonParams Default => new PersonParams(0.5f);
        }

        /// <summary>役職に対する素の適性（0..100）。軍務＝軍才／政務＝文才（役割は問わない＝才能そのもの）。</summary>
        public static float Aptitude(Person p, PostType post)
        {
            if (p == null) return 0f;
            return post == PostType.軍務 ? p.MilitaryAptitude : p.CivilAptitude;
        }

        /// <summary>役割と役職が一致するか（軍人↔軍務／文民↔政務）。</summary>
        public static bool RoleMatches(Person p, PostType post)
        {
            if (p == null) return false;
            return (p.role == PersonRole.軍人 && post == PostType.軍務)
                || (p.role == PersonRole.文民 && post == PostType.政務);
        }

        /// <summary>実効力＝適性 ×（役割一致なら1.0／不一致なら mismatchPenalty）＝適材適所。</summary>
        public static float Effectiveness(Person p, PostType post, PersonParams prm)
        {
            if (p == null) return 0f;
            float factor = RoleMatches(p, post) ? 1f : prm.mismatchPenalty;
            return Aptitude(p, post) * factor;
        }

        /// <summary>実効力（既定パラメータ）。</summary>
        public static float Effectiveness(Person p, PostType post) => Effectiveness(p, post, PersonParams.Default);

        /// <summary>
        /// 指定勢力の中で、役職に最も適した人物を選ぶ（適材適所の自動配属）。
        /// 実効力（役割一致を加味）が最大の人物を返す。候補が無ければ null。
        /// </summary>
        public static Person BestFor(IEnumerable<Person> people, Faction faction, PostType post, PersonParams prm)
        {
            if (people == null) return null;
            Person best = null;
            float bestVal = float.NegativeInfinity;
            foreach (Person p in people)
            {
                if (p == null || p.faction != faction) continue;
                float v = Effectiveness(p, post, prm);
                if (v > bestVal) { bestVal = v; best = p; }
            }
            return best;
        }

        /// <summary>適材適所の自動配属（既定パラメータ）。</summary>
        public static Person BestFor(IEnumerable<Person> people, Faction faction, PostType post)
            => BestFor(people, faction, post, PersonParams.Default);
    }
}
