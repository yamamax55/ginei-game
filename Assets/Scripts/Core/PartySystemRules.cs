using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政党制の純ロジック（政党システム・GOV-6 #159・#113）。<b>民主主義の成熟度が上がるほど二大政党制へ近づく</b>
    /// （デュヴェルジェの法則＝有効政党数が2へ収束）。だが<b>二大政党制で成熟するほど分極化（分断）が高まり危機</b>になる
    /// ＝多党は連立で穏健化し、一党は対立が無く、二党は対立が先鋭化する。成熟は国家状態（正統性/協力/包摂/低腐敗）から導く。
    /// 有効政党数は Laakso–Taagepera 指数（1/Σ支持率²）。決定論・基準値非破壊（<see cref="TickConsolidation"/> 以外）。test-first。
    /// </summary>
    public static class PartySystemRules
    {
        /// <summary>二大政党制の有効政党数。</summary>
        public const float TwoParty = 2f;
        /// <summary>未成熟（成熟0）が目標とする有効政党数の目安＝多党乱立。</summary>
        public const float FragmentedParties = 5f;
        /// <summary>分断危機とみなす分極化の既定しきい値。</summary>
        public const float DefaultCrisisThreshold = 0.6f;

        /// <summary>
        /// 民主主義の成熟度（0..1）を国家状態から導く＝正統性・協力（同意）・包摂・（腐敗の低さ）の平均。
        /// 高いほど制度が根づいた成熟民主主義（二大政党へ収束しやすい）。null は0。
        /// </summary>
        public static float MaturityFrom(FactionState s)
        {
            if (s == null) return 0f;
            float legitimacy = s.regime != null ? s.regime.legitimacy : 0.5f;
            float corruption = s.regime != null ? s.regime.corruption : 0.5f;
            float cooperation = s.polity != null ? s.polity.cooperation : 0.5f;
            float inclusiveness = s.inclusiveness;
            float m = (legitimacy + cooperation + inclusiveness + (1f - corruption)) / 4f;
            return Mathf.Clamp01(m);
        }

        /// <summary>支持率の総和（負値は0扱い）。</summary>
        static float SupportSum(IEnumerable<Party> parties)
        {
            if (parties == null) return 0f;
            float t = 0f;
            foreach (var p in parties) if (p != null) t += Mathf.Max(0f, p.support);
            return t;
        }

        /// <summary>
        /// 有効政党数（Laakso–Taagepera 指数＝1/Σ支持率²）。支持を正規化して算出する。
        /// 1党独占で1.0、互角2党で2.0、4党均等で4.0。政党が無い/総和0は0。
        /// </summary>
        public static float EffectiveNumberOfParties(IEnumerable<Party> parties)
        {
            float total = SupportSum(parties);
            if (total <= 0f) return 0f;
            float sumSq = 0f;
            foreach (var p in parties)
            {
                if (p == null) continue;
                float share = Mathf.Max(0f, p.support) / total;
                sumSq += share * share;
            }
            return sumSq > 0f ? 1f / sumSq : 0f;
        }

        /// <summary>成熟度が目標とする有効政党数（成熟0→多党 <see cref="FragmentedParties"/>、成熟1→2＝二大政党）。</summary>
        public static float TargetEffectiveParties(float maturity, float fragmentedParties)
            => Mathf.Lerp(fragmentedParties, TwoParty, Mathf.Clamp01(maturity));

        public static float TargetEffectiveParties(float maturity)
            => TargetEffectiveParties(maturity, FragmentedParties);

        /// <summary>二大政党制に近いか（有効政党数が2付近・許容差 <paramref name="tolerance"/>）。</summary>
        public static bool IsTwoPartySystem(IEnumerable<Party> parties, float tolerance)
        {
            float enp = EffectiveNumberOfParties(parties);
            return enp > 0f && Mathf.Abs(enp - TwoParty) <= tolerance;
        }

        public static bool IsTwoPartySystem(IEnumerable<Party> parties)
            => IsTwoPartySystem(parties, 0.5f);

        /// <summary>二大政党への近さ（0..1）＝有効政党数が2で1.0、1や3で0。一党/多党では分極化しにくいことを表す。</summary>
        public static float TwoPartyProximity(float enp)
            => Mathf.Clamp01(1f - Mathf.Abs(enp - TwoParty));

        /// <summary>
        /// 分極化（分断）の度合い（0..1）＝成熟度 × 二大政党への近さ。<b>二大政党制で成熟するほど高い</b>
        /// （多党は連立で穏健化、一党は対立なし）。アメリカ型二大政党制の分断危機を表す。
        /// </summary>
        public static float Polarization(float maturity, float enp)
            => Mathf.Clamp01(Mathf.Clamp01(maturity) * TwoPartyProximity(enp));

        public static float Polarization(float maturity, IEnumerable<Party> parties)
            => Polarization(maturity, EffectiveNumberOfParties(parties));

        /// <summary>分断危機か（分極化がしきい値以上）。</summary>
        public static bool IsDividedCrisis(float maturity, float enp, float threshold)
            => Polarization(maturity, enp) >= threshold;

        public static bool IsDividedCrisis(float maturity, IEnumerable<Party> parties)
            => IsDividedCrisis(maturity, EffectiveNumberOfParties(parties), DefaultCrisisThreshold);

        /// <summary>二大政党化の圧力（≥0）＝現在の有効政党数が成熟度の目標を上回るぶん（小党が淘汰される向きの強さ）。</summary>
        public static float ConsolidationPressure(IEnumerable<Party> parties, float maturity)
        {
            float enp = EffectiveNumberOfParties(parties);
            return Mathf.Max(0f, enp - TargetEffectiveParties(maturity));
        }

        /// <summary>
        /// 二大政党化を一歩進める（成熟度に応じて上位2党以外の支持を上位2党へ移す＝有効政党数を2へ寄せる）。総支持を保存する。
        /// 既に目標以下の集中（<see cref="ConsolidationPressure"/>=0）なら何もしない。移動した支持量を返す。
        /// 唯一の状態変更メソッド（他は基準値非破壊）。
        /// </summary>
        public static float TickConsolidation(IList<Party> parties, float maturity, float rate)
        {
            if (parties == null || parties.Count <= 2) return 0f;
            if (EffectiveNumberOfParties(parties) <= TargetEffectiveParties(maturity)) return 0f;

            // 上位2党を探す
            int top1 = -1, top2 = -1;
            for (int i = 0; i < parties.Count; i++)
            {
                if (parties[i] == null) continue;
                if (top1 < 0 || parties[i].support > parties[top1].support) { top2 = top1; top1 = i; }
                else if (top2 < 0 || parties[i].support > parties[top2].support) { top2 = i; }
            }
            if (top1 < 0 || top2 < 0) return 0f;

            float topSum = Mathf.Max(0f, parties[top1].support) + Mathf.Max(0f, parties[top2].support);
            if (topSum <= 0f) return 0f;
            float share1 = Mathf.Max(0f, parties[top1].support) / topSum;
            float share2 = Mathf.Max(0f, parties[top2].support) / topSum;

            float move = Mathf.Clamp01(rate * Mathf.Clamp01(maturity));
            float moved = 0f;
            for (int i = 0; i < parties.Count; i++)
            {
                if (i == top1 || i == top2 || parties[i] == null) continue;
                float take = Mathf.Max(0f, parties[i].support) * move;
                if (take <= 0f) continue;
                parties[i].support -= take;
                moved += take;
            }
            parties[top1].support += moved * share1;
            parties[top2].support += moved * share2;
            return moved;
        }
    }
}
