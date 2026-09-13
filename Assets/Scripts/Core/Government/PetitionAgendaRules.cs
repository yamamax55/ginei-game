using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>建白が上がる理由（状況）。表示と重複抑止のキーを兼ねる。</summary>
    public enum PetitionTrigger
    {
        なし,
        財政難,       // 国庫が乏しい
        重税の不満,   // 税率が高く民心が削れている
        敵の接近,     // 自勢力の星系の近くに敵艦隊がいる
        戦争の長期化, // 交戦中で厭戦が高い
        守りの綻び,   // 惑星の制空が削られている
        余剰の艦艇,   // 艦艇プールに余りがある
    }

    /// <summary>状況から起こす建白1件（何を・なぜ・どれだけ急ぐか）。</summary>
    public readonly struct PetitionAgendaItem
    {
        public readonly PetitionTrigger trigger;
        public readonly string effectKey;
        /// <summary>切迫度 0..1（大きいほど先に上がる）。</summary>
        public readonly float urgency;
        /// <summary>なぜ上がったかの1行（カード本文に添える）。</summary>
        public readonly string reason;

        public PetitionAgendaItem(PetitionTrigger trigger, string effectKey, float urgency, string reason)
        {
            this.trigger = trigger;
            this.effectKey = effectKey ?? "";
            this.urgency = Mathf.Clamp01(urgency);
            this.reason = reason ?? "";
        }

        public bool IsValid => trigger != PetitionTrigger.なし && !string.IsNullOrEmpty(effectKey);
    }

    /// <summary>状況起案の判定に使う盤面の要約（Game 層が測って渡す）。</summary>
    public readonly struct PetitionSituation
    {
        /// <summary>国庫。</summary>
        public readonly float treasury;
        /// <summary>税率 0..1。</summary>
        public readonly float taxRate;
        /// <summary>民心（希望）0..1。</summary>
        public readonly float hope;
        /// <summary>自勢力の星系に接近している敵艦隊の数。</summary>
        public readonly int approachingEnemies;
        /// <summary>交戦中か。</summary>
        public readonly bool atWar;
        /// <summary>厭戦 0..1（交戦中のみ意味がある）。</summary>
        public readonly float warWeariness;
        /// <summary>制空が削られている自勢力の惑星の数。</summary>
        public readonly int weakenedPlanets;
        /// <summary>艦艇プールの未配分（余剰）。</summary>
        public readonly int sparePoolShips;

        public PetitionSituation(float treasury, float taxRate, float hope, int approachingEnemies,
                                 bool atWar, float warWeariness, int weakenedPlanets, int sparePoolShips)
        {
            this.treasury = treasury;
            this.taxRate = Mathf.Clamp01(taxRate);
            this.hope = Mathf.Clamp01(hope);
            this.approachingEnemies = Mathf.Max(0, approachingEnemies);
            this.atWar = atWar;
            this.warWeariness = Mathf.Clamp01(warWeariness);
            this.weakenedPlanets = Mathf.Max(0, weakenedPlanets);
            this.sparePoolShips = Mathf.Max(0, sparePoolShips);
        }
    }

    /// <summary>状況起案のしきい値。</summary>
    public readonly struct PetitionAgendaParams
    {
        /// <summary>これを下回ると財政難とみなす国庫。</summary>
        public readonly float lowTreasury;
        /// <summary>これを上回ると重税とみなす税率。</summary>
        public readonly float highTaxRate;
        /// <summary>これを下回ると民心が荒れているとみなす。</summary>
        public readonly float lowHope;
        /// <summary>これを上回ると厭戦が強いとみなす。</summary>
        public readonly float highWeariness;
        /// <summary>同じ対象の建白を再び上げるまでの間隔（game-秒）。</summary>
        public readonly float cooldownSeconds;
        /// <summary>同時に決裁待ちにできる件数。</summary>
        public readonly int maxConcurrent;

        public PetitionAgendaParams(float lowTreasury, float highTaxRate, float lowHope,
                                    float highWeariness, float cooldownSeconds, int maxConcurrent)
        {
            this.lowTreasury = lowTreasury;
            this.highTaxRate = Mathf.Clamp01(highTaxRate);
            this.lowHope = Mathf.Clamp01(lowHope);
            this.highWeariness = Mathf.Clamp01(highWeariness);
            this.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            this.maxConcurrent = Mathf.Max(1, maxConcurrent);
        }

        /// <summary>既定＝国庫120未満で財政難・税率0.35超で重税・希望0.35未満で民心不安・厭戦0.5超・同一案件は180秒あけて・同時3件。</summary>
        public static PetitionAgendaParams Default => new PetitionAgendaParams(120f, 0.35f, 0.35f, 0.5f, 180f, 3);
    }

    /// <summary>
    /// <b>状況から建白を起こす</b>（作業票④）。
    ///
    /// <b>これが要る理由</b>：従来は 50 game秒ごとに確率0.6でサンプル11種から<b>無条件にランダム</b>で選んでいた。
    /// 国庫が潤沢でも増税の建白が来るし、戦争していなくても講和が上がる。
    /// ここでは<b>実状態を見て</b>意味のある案件だけを出し、同じ対象を続けて出さない。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>成立条件が無ければ<b>何も出さない</b>（間を持たせるための定型案を混ぜない）。</item>
    ///   <item>同じ対象（<see cref="PetitionTrigger"/>）は<b>未解決なら出さない</b>＋クールダウン。</item>
    ///   <item>件数上限を超えたら出さない。</item>
    ///   <item>成立条件が消えた案件は <see cref="IsStillRelevant"/> が false を返す
    ///   ＝呼び手が取り下げられる（国庫が回復したのに増税の建白が残らない）。</item>
    /// </list>
    ///
    /// 乱数なし・決定論（切迫度の高い順・同点は列挙順）。
    /// </summary>
    public static class PetitionAgendaRules
    {
        /// <summary>
        /// いまの状況で意味のある建白を、切迫度の高い順に並べて返す（無ければ空）。
        /// </summary>
        public static List<PetitionAgendaItem> Candidates(in PetitionSituation s, in PetitionAgendaParams p)
        {
            var list = new List<PetitionAgendaItem>();

            // 財政難＝国庫が乏しい。増税で凌ぐ（民心は削れる）。
            if (s.treasury < p.lowTreasury)
                list.Add(new PetitionAgendaItem(PetitionTrigger.財政難, "tax.hike",
                    Urgency(p.lowTreasury - s.treasury, p.lowTreasury),
                    $"国庫が乏しい（残 {s.treasury:0}）。増税で凌ぐべしとの建白。"));

            // 重税の不満＝税率が高く民心が沈んでいる。減税。
            if (s.taxRate > p.highTaxRate && s.hope < p.lowHope)
                list.Add(new PetitionAgendaItem(PetitionTrigger.重税の不満, "tax.cut",
                    Urgency(s.taxRate - p.highTaxRate, 1f - p.highTaxRate),
                    $"重税（{s.taxRate * 100f:0}%）に民が苦しみ、民心が沈んでいる（{s.hope * 100f:0}%）。減税の建白。"));

            // 敵の接近＝動員して備える。
            if (s.approachingEnemies > 0)
                list.Add(new PetitionAgendaItem(PetitionTrigger.敵の接近, "mil.mobilize",
                    Urgency(s.approachingEnemies, 4f),
                    $"自領の近くに敵艦隊 {s.approachingEnemies} 隊。動員して備えるべしとの建白。"));

            // 守りの綻び＝制空が削られている惑星がある。防衛強化。
            if (s.weakenedPlanets > 0)
                list.Add(new PetitionAgendaItem(PetitionTrigger.守りの綻び, "mil.defend",
                    Urgency(s.weakenedPlanets, 3f),
                    $"制空を削られた惑星が {s.weakenedPlanets}。守りを固めよとの建白。"));

            // 戦争の長期化＝厭戦が高い。講和。
            if (s.atWar && s.warWeariness > p.highWeariness)
                list.Add(new PetitionAgendaItem(PetitionTrigger.戦争の長期化, "diplo.ceasefire",
                    Urgency(s.warWeariness - p.highWeariness, 1f - p.highWeariness),
                    $"戦が長引き厭戦が広がっている（{s.warWeariness * 100f:0}%）。講和を求める建白。"));

            // 余剰の艦艇＝プールに余りがある。攻勢に出る。
            if (s.sparePoolShips > 0)
                list.Add(new PetitionAgendaItem(PetitionTrigger.余剰の艦艇, "mil.offensive",
                    Urgency(s.sparePoolShips, 8000f),
                    $"艦艇プールに余剰 {s.sparePoolShips:N0} 隻。攻勢に出よとの建白。"));

            // 切迫度の高い順（同点は列挙順＝決定論）。
            SortByUrgency(list);
            return list;
        }

        /// <summary>
        /// 次に上げるべき建白を1件選ぶ（無ければ <see cref="PetitionAgendaItem.IsValid"/> が false）。
        /// <paramref name="isPending"/>＝その状況の建白がすでに未解決で残っているか。
        /// <paramref name="secondsSince"/>＝その状況で最後に建白してからの game-秒（初回は大きい値を返すこと）。
        /// </summary>
        public static PetitionAgendaItem Next(in PetitionSituation s, in PetitionAgendaParams p,
                                              int pendingCount,
                                              System.Func<PetitionTrigger, bool> isPending,
                                              System.Func<PetitionTrigger, float> secondsSince)
        {
            if (pendingCount >= p.maxConcurrent) return default;   // 積みすぎない

            List<PetitionAgendaItem> candidates = Candidates(s, p);
            for (int i = 0; i < candidates.Count; i++)
            {
                PetitionAgendaItem c = candidates[i];
                if (isPending != null && isPending(c.trigger)) continue;                 // 未解決の重複を出さない
                if (secondsSince != null && secondsSince(c.trigger) < p.cooldownSeconds) continue; // クールダウン
                return c;
            }
            return default;   // 出すべきものが無ければ<b>出さない</b>（定型案で埋めない）
        }

        /// <summary>
        /// その建白の成立条件がまだ生きているか。false なら状況が変わった＝取り下げてよい
        /// （国庫が回復したのに増税の建白が残り続ける、といったことを防ぐ）。
        /// </summary>
        public static bool IsStillRelevant(PetitionTrigger trigger, in PetitionSituation s, in PetitionAgendaParams p)
        {
            switch (trigger)
            {
                case PetitionTrigger.財政難: return s.treasury < p.lowTreasury;
                case PetitionTrigger.重税の不満: return s.taxRate > p.highTaxRate && s.hope < p.lowHope;
                case PetitionTrigger.敵の接近: return s.approachingEnemies > 0;
                case PetitionTrigger.守りの綻び: return s.weakenedPlanets > 0;
                case PetitionTrigger.戦争の長期化: return s.atWar && s.warWeariness > p.highWeariness;
                case PetitionTrigger.余剰の艦艇: return s.sparePoolShips > 0;
                default: return false;
            }
        }

        /// <summary>効果キーからその建白の状況を逆に引く（保存された案件の再評価に使う）。</summary>
        public static PetitionTrigger TriggerOf(string effectKey)
        {
            switch (effectKey)
            {
                case "tax.hike": return PetitionTrigger.財政難;
                case "tax.cut": return PetitionTrigger.重税の不満;
                case "mil.mobilize": return PetitionTrigger.敵の接近;
                case "mil.defend": return PetitionTrigger.守りの綻び;
                case "diplo.ceasefire": return PetitionTrigger.戦争の長期化;
                case "mil.offensive": return PetitionTrigger.余剰の艦艇;
                default: return PetitionTrigger.なし;
            }
        }

        /// <summary>0..1 の切迫度（超過分 ÷ 目安幅）。</summary>
        private static float Urgency(float over, float span)
            => span <= 0.0001f ? 1f : Mathf.Clamp01(over / span);

        /// <summary>切迫度の降順（安定＝同点は元の並び）。</summary>
        private static void SortByUrgency(List<PetitionAgendaItem> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                PetitionAgendaItem cur = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].urgency < cur.urgency) { list[j + 1] = list[j]; j--; }
                list[j + 1] = cur;
            }
        }
    }
}
