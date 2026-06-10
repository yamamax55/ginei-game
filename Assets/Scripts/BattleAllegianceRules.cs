using System.Collections.Generic;

namespace Ginei
{
    /// <summary>会戦中の旗幟遷移1件（どの諸侯が・どの旗幟から・どの旗幟へ）。</summary>
    public readonly struct StanceChange
    {
        public readonly int id;       // Allegiance.id（＝会戦側で艦隊と紐付けるキー）
        public readonly Stance from;
        public readonly Stance to;

        public StanceChange(int id, Stance from, Stance to)
        {
            this.id = id;
            this.from = from;
            this.to = to;
        }
    }

    /// <summary>
    /// 会戦中の旗幟解決の唯一の窓口（#817 関ヶ原型の会戦配線）。
    /// 戦略側で仕込まれた忠誠/調略（<see cref="Allegiance"/>）を、会戦の現在戦力（趨勢）で
    /// 定期的に再解決し、「寝返り」「静観」の遷移として返す。カスケード本体は
    /// <see cref="LoyaltyRules.ResolveCascade"/> に委譲し、ここでは会戦適用に必要な
    /// 差分収集・寝返りの不可逆ロック・静観組の退き判定だけを足す。純ロジック・test-first。
    /// </summary>
    public static class BattleAllegianceRules
    {
        /// <summary>
        /// 旗幟を現在の趨勢で再解決し、変化した諸侯を <paramref name="outChanges"/> に集める。
        /// 寝返った諸侯は <see cref="Allegiance.locked"/>＝true で確定し、以後は再解決の対象外
        /// （寝返り返りはしない＝小早川は戻らない）。戻り値は遷移数。
        /// </summary>
        public static int ResolveTransitions(IList<Allegiance> list, Faction sideA, Faction sideB,
            LoyaltyParams p, IList<StanceChange> outChanges)
        {
            if (list == null || outChanges == null) return 0;

            // 解決前の旗幟をスナップショット
            var before = new Stance[list.Count];
            for (int i = 0; i < list.Count; i++)
                before[i] = list[i] != null ? list[i].stance : Stance.未定;

            LoyaltyRules.ResolveCascade(list, sideA, sideB, p);

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Allegiance a = list[i];
                if (a == null || a.stance == before[i]) continue;
                outChanges.Add(new StanceChange(a.id, before[i], a.stance));
                count++;
                if (a.stance == Stance.寝返り) a.locked = true; // 不可逆＝寝返り返りはしない
            }
            return count;
        }

        /// <summary>
        /// side 陣営の「戦う者」が尽きたか（実効兵力0かつ敵側は残存）＝静観組が戦わずして戦場を去る条件。
        /// 関ヶ原で戦闘が三成・大谷の壊滅で終わり、静観の大半が戦わず帰国したのと同型。
        /// </summary>
        public static bool ShouldWithdraw(IList<Allegiance> list, Faction side, Faction enemySide)
        {
            if (list == null) return false;
            return LoyaltyRules.EffectiveStrength(list, side) <= 0
                && LoyaltyRules.EffectiveStrength(list, enemySide) > 0;
        }
    }
}
