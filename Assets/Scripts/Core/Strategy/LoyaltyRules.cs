using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>忠誠・寝返り判定の調整係数（#817 SEKI）。</summary>
    public readonly struct LoyaltyParams
    {
        /// <summary>純忠誠(loyalty-intrigue)がこれ以上なら「戦う」。</summary>
        public readonly float fightThreshold;
        /// <summary>調略(intrigue)がこれ以上＋自軍劣勢なら「寝返り」。</summary>
        public readonly float defectThreshold;

        public LoyaltyParams(float fightThreshold, float defectThreshold)
        {
            this.fightThreshold = fightThreshold;
            this.defectThreshold = defectThreshold;
        }

        public static LoyaltyParams Default => new LoyaltyParams(0.5f, 0.5f);
    }

    /// <summary>関ヶ原型会戦の決着（#817 SEKI-5）。損害は「実際に戦った兵」に集中する。</summary>
    public readonly struct EngagementResult
    {
        public readonly Faction winner;
        public readonly int winnerEffective; // 勝者の実効兵力（戦う＋寝返り）
        public readonly int loserEffective;  // 敗者の実効兵力
        public readonly int winnerSurvivors; // 勝者の戦闘兵の残存（消耗後＝winnerEff-loserEff）
        public readonly int loserCasualties; // 敗者の戦闘兵の損失（実際に戦った分のみ）

        public EngagementResult(Faction winner, int winnerEffective, int loserEffective, int winnerSurvivors, int loserCasualties)
        {
            this.winner = winner;
            this.winnerEffective = winnerEffective;
            this.loserEffective = loserEffective;
            this.winnerSurvivors = winnerSurvivors;
            this.loserCasualties = loserCasualties;
        }
    }

    /// <summary>
    /// 関ヶ原型「戦う前に決まる戦い」の純ロジック（#817・SEKI-1〜3 / #822）。
    /// 各諸侯の旗幟（戦う/静観/寝返り）を、忠誠・調略・趨勢から解決し、寝返りカスケード
    /// （ナッシュ均衡崩壊 #818）で「実際に戦う兵力（実効兵力）」を確定する。
    /// 勝敗は名目兵力でなく実効兵力で決まる＝兵力多数でも静観（フリーライダー #820）・寝返りで崩れる。
    /// 戦う前の調略（#819 家康の手紙＝<see cref="ApplyIntrigue"/>）で勝敗を仕込むのが核。test-first。
    /// </summary>
    public static class LoyaltyRules
    {
        /// <summary>自軍の趨勢(-1..+1)。own/enemy の実効兵力から。正＝自軍優勢。</summary>
        public static float Momentum(float own, float enemy)
        {
            float total = own + enemy;
            return total <= 0f ? 0f : (own - enemy) / total;
        }

        /// <summary>
        /// 一諸侯の旗幟を、自軍の趨勢 momentumOwn(-1..+1) から決める。
        /// 純忠誠が勝れば「戦う」、調略済みかつ自軍劣勢なら「寝返り」、それ以外は「静観」（ただ乗り）。
        /// </summary>
        public static Stance ResolveStance(Allegiance a, float momentumOwn, LoyaltyParams p)
        {
            if (a == null) return Stance.静観;
            float net = a.loyalty - a.intrigue;
            if (net >= p.fightThreshold) return Stance.戦う;                        // 忠誠が勝る＝命がけで戦う
            if (a.intrigue >= p.defectThreshold && momentumOwn < 0f) return Stance.寝返り; // 調略＋自軍劣勢＝寝返り
            return Stance.静観;                                                     // それ以外＝山上で静観（フリーライダー）
        }

        /// <summary>side 陣営のために実際に戦う兵力の合計（戦う＋敵側からの寝返り。静観は数えない）。</summary>
        public static int EffectiveStrength(IList<Allegiance> list, Faction side)
        {
            if (list == null) return 0;
            int s = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Allegiance a = list[i];
                if (a != null && a.FightsFor(side)) s += a.strength;
            }
            return s;
        }

        /// <summary>
        /// 旗幟のカスケード解決（不動点反復）。各諸侯が現在の趨勢を見て旗幟を決め直し、変化が無くなるまで繰り返す。
        /// 劣勢になった側の調略済み諸侯が次々に寝返る雪崩（小早川の寝返り＝ナッシュ均衡崩壊 #818）を再現。
        /// </summary>
        public static void ResolveCascade(IList<Allegiance> list, Faction sideA, Faction sideB, LoyaltyParams p, int maxIter = 24)
        {
            if (list == null) return;
            for (int iter = 0; iter < maxIter; iter++)
            {
                int effA = EffectiveStrength(list, sideA);
                int effB = EffectiveStrength(list, sideB);
                bool changed = false;
                for (int i = 0; i < list.Count; i++)
                {
                    Allegiance a = list[i];
                    if (a == null || a.locked) continue;
                    float own = (a.side == sideA) ? Momentum(effA, effB) : Momentum(effB, effA);
                    Stance st = ResolveStance(a, own, p);
                    if (st != a.stance) { a.stance = st; changed = true; }
                }
                if (!changed) break;
            }
        }

        /// <summary>
        /// カスケードを解決し、実効兵力が多い側を勝者として返す（同数は sideA）。
        /// ＝「戦う前（調略の段階）に勝敗が決まっている」を体現する窓口。
        /// </summary>
        public static Faction ResolveWinner(IList<Allegiance> list, Faction sideA, Faction sideB, LoyaltyParams p, out int effA, out int effB)
        {
            ResolveCascade(list, sideA, sideB, p);
            effA = EffectiveStrength(list, sideA);
            effB = EffectiveStrength(list, sideB);
            return effB > effA ? sideB : sideA;
        }

        public static Faction ResolveWinner(IList<Allegiance> list, Faction sideA, Faction sideB, out int effA, out int effB)
            => ResolveWinner(list, sideA, sideB, LoyaltyParams.Default, out effA, out effB);

        /// <summary>
        /// カスケード解決後、実効兵力差で会戦を決着させる（#817 SEKI-5）。勝者の戦闘兵は消耗
        /// （winnerEff-loserEff が残存）、敗者の戦闘兵は全滅。静観（フリーライダー #820）・寝返りは
        /// 戦っていないので無傷で残る＝<b>敗者側の損害は実際に戦った少数に集中する</b>
        /// （関ヶ原で西軍の損害が三成・大谷に集中したのと同型＝大半は降伏/寝返りで生き残る）。
        /// </summary>
        public static EngagementResult ResolveEngagement(IList<Allegiance> list, Faction sideA, Faction sideB, LoyaltyParams p)
        {
            Faction winner = ResolveWinner(list, sideA, sideB, p, out int effA, out int effB);
            int winEff = (winner == sideA) ? effA : effB;
            int loseEff = (winner == sideA) ? effB : effA;
            int winSurv = Mathf.Max(0, winEff - loseEff);
            return new EngagementResult(winner, winEff, loseEff, winSurv, loseEff);
        }

        public static EngagementResult ResolveEngagement(IList<Allegiance> list, Faction sideA, Faction sideB)
            => ResolveEngagement(list, sideA, sideB, LoyaltyParams.Default);

        /// <summary>調略：敵方の浸透を amount だけ強める（#819 家康の手紙＝戦前プログラミング）。</summary>
        public static void ApplyIntrigue(Allegiance a, float amount)
        {
            if (a == null) return;
            a.intrigue = Mathf.Clamp01(a.intrigue + amount);
        }

        /// <summary>名目兵力の合計（実効と対比して「兵力≠実働」を見せる用・#820）。</summary>
        public static int NominalStrength(IList<Allegiance> list, Faction side)
        {
            if (list == null) return 0;
            int s = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Allegiance a = list[i];
                if (a != null && a.side == side) s += a.strength;
            }
            return s;
        }
    }
}
