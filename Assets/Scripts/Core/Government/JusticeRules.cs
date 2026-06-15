using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>競合する正義観（サンデル #918-923）。同じ政策でも立場で正/不正の評価が割れる。</summary>
    public enum JusticeView
    {
        功利主義,      // 総効用の最大化（再分配と自由のバランス）
        ロールズ,      // 最不遇層を益するか（格差原理＝高再分配を是とする）
        リバタリアン,  // 自由の不可侵（再分配＝強制移転を不正とみなす）
        アリストテレス, // 功績主義（卓越に報いる＝門地を開く正義）
        共通善         // 共同体への忠誠・美徳の涵養
    }

    /// <summary>正義の天秤の調整係数（各正義観の評価の重み付け）。</summary>
    public readonly struct JusticeParams
    {
        /// <summary>功利主義の効用に占める再分配の重み（残りは自由の重み）。</summary>
        public readonly float utilRedistributionWeight;
        /// <summary>リバタリアンが再分配を不正とみなす強さ（高再分配でどれだけ評価を削るか）。</summary>
        public readonly float libertarianRedistributionPenalty;
        /// <summary>正統性増減の振れ幅（是認0.5を中立として ±legitimacySwing）。</summary>
        public readonly float legitimacySwing;

        public JusticeParams(float utilRedistributionWeight, float libertarianRedistributionPenalty, float legitimacySwing)
        {
            this.utilRedistributionWeight = Mathf.Clamp01(utilRedistributionWeight);
            this.libertarianRedistributionPenalty = Mathf.Clamp01(libertarianRedistributionPenalty);
            this.legitimacySwing = Mathf.Max(0f, legitimacySwing);
        }

        /// <summary>既定＝効用は再分配/自由を同等(0.5)・リバタリアンの再分配ペナルティ全幅(1.0)・正統性振れ±0.2。</summary>
        public static JusticeParams Default => new JusticeParams(0.5f, 1f, 0.2f);
    }

    /// <summary>
    /// 正義の天秤の純ロジック（サンデル #918-923）。競合する正義観（<see cref="JusticeView"/>）が、政策の特徴
    /// （再分配・自由・功績主義・共同体忠誠）をそれぞれの物差しで是認/否認する＝「同じ政策が立場で正にも不正にもなる」。
    /// 住民の正義観構成（重み）に対する是認の加重和が正統性を駆動し、最も不満な正義観がイベントの火種になる。
    /// すべて 0..1 の plain 引数で完結（基準値非破壊・決定論）。test-first。
    /// </summary>
    public static class JusticeRules
    {
        /// <summary>
        /// ある正義観から見た政策の是認度 0..1。
        /// 功利主義＝再分配と自由のバランス（総効用）／ロールズ＝最不遇層を益す高再分配を是とする／
        /// リバタリアン＝自由を是とし再分配を不正とみなす（高再分配で低評価）／アリストテレス＝功績主義に報いるか／
        /// 共通善＝共同体忠誠を重んじるか。
        /// </summary>
        public static float Approval(JusticeView view, float redistribution, float liberty, float meritocracy, float communalLoyalty, JusticeParams p)
        {
            float r = Mathf.Clamp01(redistribution);
            float l = Mathf.Clamp01(liberty);
            float m = Mathf.Clamp01(meritocracy);
            float c = Mathf.Clamp01(communalLoyalty);

            switch (view)
            {
                case JusticeView.功利主義:
                    // 総効用＝再分配（厚生）と自由のバランス
                    return p.utilRedistributionWeight * r + (1f - p.utilRedistributionWeight) * l;
                case JusticeView.ロールズ:
                    // 格差原理＝最不遇層を益する高再分配を是とする
                    return r;
                case JusticeView.リバタリアン:
                    // 自由を是とし、再分配（強制移転）を不正とみなす＝高再分配で評価を削る
                    return Mathf.Clamp01(l - r * p.libertarianRedistributionPenalty);
                case JusticeView.アリストテレス:
                    // 卓越に報いる功績主義＝門地開放の正義
                    return m;
                case JusticeView.共通善:
                    // 共同体への忠誠・美徳の涵養
                    return c;
                default:
                    return 0.5f;
            }
        }

        /// <summary>既定パラメータ版。</summary>
        public static float Approval(JusticeView view, float redistribution, float liberty, float meritocracy, float communalLoyalty)
            => Approval(view, redistribution, liberty, meritocracy, communalLoyalty, JusticeParams.Default);

        /// <summary>
        /// 住民の正義観構成（view→weight）に対する政策の是認の加重平均（0..1）。重み総和が0なら中立0.5。
        /// </summary>
        public static float WeightedApproval(IList<(JusticeView view, float weight)> populace, float redistribution, float liberty, float meritocracy, float communalLoyalty, JusticeParams p)
        {
            if (populace == null || populace.Count == 0) return 0.5f;
            float sumW = 0f;
            float sumA = 0f;
            for (int i = 0; i < populace.Count; i++)
            {
                float w = Mathf.Max(0f, populace[i].weight);
                if (w <= 0f) continue;
                sumW += w;
                sumA += w * Approval(populace[i].view, redistribution, liberty, meritocracy, communalLoyalty, p);
            }
            if (sumW <= 0f) return 0.5f;
            return sumA / sumW;
        }

        /// <summary>
        /// 住民の正義観構成に対する政策の正統性増減。是認の加重平均（0..1）を中立0.5を境に ±legitimacySwing へ写す。
        /// 是認0.5で±0、全是認で +legitimacySwing、全否認で −legitimacySwing。
        /// </summary>
        public static float LegitimacyDelta(IList<(JusticeView view, float weight)> populace, float redistribution, float liberty, float meritocracy, float communalLoyalty, JusticeParams p)
        {
            float approval = WeightedApproval(populace, redistribution, liberty, meritocracy, communalLoyalty, p);
            return (approval - 0.5f) * 2f * p.legitimacySwing;
        }

        /// <summary>既定パラメータ版。</summary>
        public static float LegitimacyDelta(IList<(JusticeView view, float weight)> populace, float redistribution, float liberty, float meritocracy, float communalLoyalty)
            => LegitimacyDelta(populace, redistribution, liberty, meritocracy, communalLoyalty, JusticeParams.Default);

        /// <summary>
        /// 最も不満を持つ正義観（最低是認度かつ重み&gt;0）を返す＝イベント発火の種。
        /// hasGrievance=false なら該当なし（住民空・全重み0）。同点は populace の先頭側を優先。
        /// </summary>
        public static JusticeView DominantGrievance(IList<(JusticeView view, float weight)> populace, float redistribution, float liberty, float meritocracy, float communalLoyalty, out bool hasGrievance)
        {
            hasGrievance = false;
            JusticeView worst = default;
            float lowest = float.PositiveInfinity;
            if (populace == null) return worst;
            for (int i = 0; i < populace.Count; i++)
            {
                if (populace[i].weight <= 0f) continue;
                float a = Approval(populace[i].view, redistribution, liberty, meritocracy, communalLoyalty);
                if (a < lowest)
                {
                    lowest = a;
                    worst = populace[i].view;
                    hasGrievance = true;
                }
            }
            return worst;
        }

        /// <summary>out 無し簡易版（該当なしは <see cref="JusticeView.功利主義"/> を返す）。</summary>
        public static JusticeView DominantGrievance(IList<(JusticeView view, float weight)> populace, float redistribution, float liberty, float meritocracy, float communalLoyalty)
            => DominantGrievance(populace, redistribution, liberty, meritocracy, communalLoyalty, out _);
    }
}
