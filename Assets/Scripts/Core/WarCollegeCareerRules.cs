using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>下士官・将校のキャリアイベント種別（通知用）。</summary>
    public enum CareerEventKind { 入校, 卒業, 恩賜の軍刀, 昇進 }

    /// <summary>キャリアイベント（Core は通知を持たないので結果として返し、Game 層が通知に変換する）。</summary>
    public struct CareerEvent
    {
        public CareerEventKind kind;
        public int personId;
        public string personName;
        public Faction faction;
        public int rankTier;
    }

    /// <summary>
    /// 陸軍大学校のエリート街道を回す年次オーケストレータ（#SCHOOL-AGE 配線・test-first・唯一の窓口）。
    /// 「<b>大学校入学 → ネームドが学校配属（艦隊配属不可） → 卒業して大学校卒=参謀＝恩賜の軍刀組＝昇進優遇</b>」を
    /// 一本の流れにする。各部品（<see cref="SchoolAgeRules"/> 入校/修業年・<see cref="SchoolPostingRules"/> 学校配属ゲート・
    /// <see cref="MilitaryAcademyRules.WarCollegeTierBonus"/> 星の優遇・<see cref="MilitarySwordHonorRules"/> 恩賜と昇進優遇）を束ねる。
    /// 状態（<see cref="Person"/> ロスター）を進めるが、数式は各 *Rules へ委譲する（二重実装しない）。
    /// </summary>
    public static class WarCollegeCareerRules
    {
        /// <summary>陸軍大学校の修業年限（＝<see cref="SchoolAgeRules"/> 31−28＝3）。</summary>
        public static int WarCollegeDuration => SchoolAgeRules.DurationYears(SchoolType.陸軍大学校);

        public const int MinEnrollAge = 26;   // 入校適齢の下限（数年の隊付勤務の後）
        public const int MaxEnrollAge = 33;   // 上限（中堅将校まで＝老将は行かない）
        public const int AnnualIntakePerFaction = 1;        // 年次の入校枠（勢力ごと）
        public const int PromotionsPerFactionPerYear = 1;   // 年次の昇進枠（勢力ごと）
        public const int EliteTierCeiling = 9;              // エリート街道の上限tier（元帥10 は別格）

        /// <summary>大学校へ入校できる候補か＝現役（生存・自由）・大学校未修了・在学中でない・入校適齢。</summary>
        public static bool CanEnroll(Person p, int year)
        {
            if (p == null || !p.IsAvailable) return false;
            if (p.serviceStatus != ServiceStatus.現役) return false; // 退役/予備役は入校しない
            if (p.militaryDegree == MilitaryDegree.大学校卒) return false; // 既に参謀
            if (p.birthYear <= 0) return false;
            if (SchoolPostingRules.IsEnrolled(p, year)) return false;       // 既に在学中
            int age = LifecycleRules.Age(p, year);
            return age >= MinEnrollAge && age <= MaxEnrollAge;
        }

        /// <summary>入校させる＝学校配属（修業年限ぶん艦隊配属不可・<see cref="SchoolPostingRules"/>）。</summary>
        public static void Enroll(Person p, int year)
        {
            if (p == null) return;
            p.schoolPostingUntilYear = year + WarCollegeDuration;
        }

        /// <summary>在学者で修業年限に達したか（卒業の年）。</summary>
        public static bool IsGraduating(Person p, int year)
            => p != null && p.schoolPostingUntilYear > 0 && year >= p.schoolPostingUntilYear;

        /// <summary>
        /// 卒業させる＝学校配属を解き、大学校卒（参謀）＋星の優遇（<see cref="MilitaryAcademyRules.WarCollegeTierBonus"/>・上限あり）・
        /// 大学校内席次 <paramref name="warCollegeRank"/>（上位 <see cref="MilitarySwordHonorRules.SwordQuota"/>＝恩賜の軍刀組）を刻む。
        /// </summary>
        public static void Graduate(Person p, int warCollegeRank)
        {
            if (p == null) return;
            p.schoolPostingUntilYear = 0;
            p.militaryDegree = MilitaryDegree.大学校卒;
            p.warCollegeRank = warCollegeRank;
            p.rankTier = Mathf.Min(EliteTierCeiling, p.rankTier + MilitaryAcademyRules.WarCollegeTierBonus);
        }

        /// <summary>その人物の栄誉credential（大学校卒×席次→なし/星/恩賜の軍刀）。</summary>
        public static MilitaryHonor HonorOf(Person p)
            => p == null ? MilitaryHonor.なし : MilitarySwordHonorRules.HonorOf(p.militaryDegree, p.warCollegeRank);

        /// <summary>昇進優遇スコア（credential×実務 merit を勢力の doctrine で混合）。</summary>
        public static float PromotionFavor(Person p, PromotionDoctrine doctrine)
        {
            if (p == null) return 0f;
            float merit = Mathf.Clamp01(p.MilitaryAptitude / (float)AdmiralData.MaxStatValue);
            return MilitarySwordHonorRules.PromotionFavor(HonorOf(p), merit, doctrine);
        }

        /// <summary>
        /// 年次オーケストレーション：勢力ごとに ①卒業（在学年限到達・軍才順に大学校内席次→恩賜判定）②入校（適格者から枠ぶん）
        /// ③昇進（在学中でない者を昇進優遇順に枠ぶん＝学閥主義では恩賜組が速く出世・実力主義では俊英が追い越す）。
        /// 起きた事象を <paramref name="events"/> に積む（Game 層が通知へ）。
        /// </summary>
        public static void TickYear(List<Person> roster, int year, System.Func<Faction, PromotionDoctrine> doctrineOf,
                                    List<CareerEvent> events = null)
        {
            if (roster == null) return;
            var factions = new List<Faction>();
            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i];
                if (p != null && !factions.Contains(p.faction)) factions.Add(p.faction);
            }

            for (int fi = 0; fi < factions.Count; fi++)
            {
                Faction f = factions[fi];
                PromotionDoctrine doctrine = doctrineOf != null ? doctrineOf(f) : PromotionDoctrine.学閥主義;

                // ① 卒業（軍才順に大学校内席次＝1首席。上位 SwordQuota が恩賜の軍刀組）
                var grads = CollectByFaction(roster, f, p => IsGraduating(p, year));
                grads.Sort((a, b) => b.MilitaryAptitude.CompareTo(a.MilitaryAptitude));
                for (int i = 0; i < grads.Count; i++)
                {
                    int rank = i + 1;
                    Graduate(grads[i], rank);
                    Emit(events, grads[i], MilitarySwordHonorRules.IsSwordGroup(MilitaryDegree.大学校卒, rank)
                        ? CareerEventKind.恩賜の軍刀 : CareerEventKind.卒業);
                }

                // ② 入校（軍才順に枠ぶん）
                var cands = CollectByFaction(roster, f, p => CanEnroll(p, year));
                cands.Sort((a, b) => b.MilitaryAptitude.CompareTo(a.MilitaryAptitude));
                int intake = Mathf.Min(AnnualIntakePerFaction, cands.Count);
                for (int i = 0; i < intake; i++)
                {
                    Enroll(cands[i], year);
                    Emit(events, cands[i], CareerEventKind.入校);
                }

                // ③ 昇進（在学中でない・上限未満を昇進優遇順に枠ぶん）
                var promo = CollectByFaction(roster, f, p =>
                    p.IsAvailable && p.serviceStatus == ServiceStatus.現役
                    && !SchoolPostingRules.IsEnrolled(p, year) && p.rankTier < EliteTierCeiling);
                promo.Sort((a, b) => PromotionFavor(b, doctrine).CompareTo(PromotionFavor(a, doctrine)));
                int n = Mathf.Min(PromotionsPerFactionPerYear, promo.Count);
                for (int i = 0; i < n; i++)
                {
                    promo[i].rankTier += 1;
                    Emit(events, promo[i], CareerEventKind.昇進);
                }
            }
        }

        private static List<Person> CollectByFaction(List<Person> roster, Faction f, System.Func<Person, bool> pred)
        {
            var list = new List<Person>();
            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i];
                if (p != null && p.faction == f && pred(p)) list.Add(p);
            }
            return list;
        }

        private static void Emit(List<CareerEvent> events, Person p, CareerEventKind kind)
        {
            if (events == null || p == null) return;
            events.Add(new CareerEvent { kind = kind, personId = p.id, personName = p.name, faction = p.faction, rankTier = p.rankTier });
        }
    }
}
