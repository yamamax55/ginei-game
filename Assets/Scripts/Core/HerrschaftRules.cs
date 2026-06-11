using UnityEngine;

namespace Ginei
{
    /// <summary>正統的支配の三類型（マックス・ウェーバー）。安定プロファイルと崩壊モードが異なる。</summary>
    public enum HerrschaftType
    {
        伝統的,   // Traditionale Herrschaft：昔からの慣習・世襲の権威
        カリスマ的, // Charismatische Herrschaft：指導者個人の非日常的資質
        合法的    // Legale Herrschaft：制定された規則・官僚制
    }

    /// <summary>支配の三類型の調整係数（#1525）。</summary>
    public readonly struct HerrschaftParams
    {
        /// <summary>類型の基礎安定度（合法的＝高く安定／伝統的＝中／カリスマ的＝高いが脆い）。</summary>
        public readonly float legalStability;
        public readonly float traditionalStability;
        public readonly float charismaticStability;

        /// <summary>カリスマ的支配の脆さの強さ（日常化が進むほど打ち消す）。</summary>
        public readonly float charismaFragilityWeight;
        /// <summary>近代化が伝統的権威を蝕む速さ。</summary>
        public readonly float traditionErosionRate;
        /// <summary>カリスマの日常化（伝統化/合法化）の速さ。</summary>
        public readonly float routinizationRate;

        public HerrschaftParams(float legalStability, float traditionalStability, float charismaticStability,
            float charismaFragilityWeight, float traditionErosionRate, float routinizationRate)
        {
            this.legalStability = Mathf.Clamp01(legalStability);
            this.traditionalStability = Mathf.Clamp01(traditionalStability);
            this.charismaticStability = Mathf.Clamp01(charismaticStability);
            this.charismaFragilityWeight = Mathf.Clamp01(charismaFragilityWeight);
            this.traditionErosionRate = Mathf.Max(0f, traditionErosionRate);
            this.routinizationRate = Mathf.Max(0f, routinizationRate);
        }

        /// <summary>既定＝合法0.8/伝統0.6/カリスマ0.75・脆さ0.7・侵食0.2・日常化0.15。</summary>
        public static HerrschaftParams Default => new HerrschaftParams(0.8f, 0.6f, 0.75f, 0.7f, 0.2f, 0.15f);
    }

    /// <summary>類型ごとの崩壊の仕方（#1525）。</summary>
    public enum HerrschaftCollapseMode
    {
        近代化侵食,     // 伝統的：近代化・合理化が慣習の権威を蝕む
        指導者の死失敗, // カリスマ的：指導者個人の死や失敗で一挙に瓦解
        正統性危機硬直  // 合法的：規則の硬直・正統性危機で機能不全
    }

    /// <summary>
    /// 正統的支配の三類型の純ロジック（WEBR-1 #1525・マックス・ウェーバー参考）。
    /// 支配の正統性を①伝統的（慣習・世襲）②カリスマ的（個人資質）③合法的（規則・官僚制）の三類型で扱い、
    /// それぞれ <b>安定プロファイルと崩壊モードが異なる</b>＝カリスマは強いが脆く（指導者依存）、合法は安定だが硬直しうる。
    /// <see cref="CivilianControlRules"/>（軍政関係＝軍と政府の上下）・<see cref="RegimeRules"/>（天命と腐敗）とは別＝
    /// 支配の正統性類型そのもの（三類型×安定×崩壊モード）。カリスマの日常化は <see cref="SuccessionRules"/>（#812・組織の継承）へ接続、
    /// カリスマ的支配の現代版（人民投票的指導者民主制）は <c>PlebiscitaryRules</c>（生成済み）が担う。乱数なし決定論・test-first。
    /// </summary>
    public static class HerrschaftRules
    {
        /// <summary>
        /// 類型に応じた正統性の源（0..1）。伝統的＝慣習(tradition)・カリスマ的＝個人資質(charisma)・合法的＝規則(legality)を
        /// 主因に、他の二源は弱い補助として混ぜる（純粋型は稀で混合する）。
        /// </summary>
        public static float LegitimacyStrength(HerrschaftType type, float tradition, float charisma, float legality)
        {
            float tr = Mathf.Clamp01(tradition);
            float ch = Mathf.Clamp01(charisma);
            float lg = Mathf.Clamp01(legality);
            const float aux = 0.15f; // 補助源の寄与
            switch (type)
            {
                case HerrschaftType.伝統的:
                    return Mathf.Clamp01(tr * (1f - 2f * aux) + (ch + lg) * aux);
                case HerrschaftType.カリスマ的:
                    return Mathf.Clamp01(ch * (1f - 2f * aux) + (tr + lg) * aux);
                case HerrschaftType.合法的:
                    return Mathf.Clamp01(lg * (1f - 2f * aux) + (tr + ch) * aux);
                default:
                    return Mathf.Clamp01((tr + ch + lg) / 3f);
            }
        }

        /// <summary>類型ごとの基礎安定度（0..1）。合法的＝高く安定／伝統的＝中／カリスマ的＝高いが脆い（脆さは別関数）。</summary>
        public static float StabilityProfile(HerrschaftType type, HerrschaftParams p)
        {
            switch (type)
            {
                case HerrschaftType.合法的:   return p.legalStability;
                case HerrschaftType.伝統的:   return p.traditionalStability;
                case HerrschaftType.カリスマ的: return p.charismaticStability;
                default: return 0.5f;
            }
        }

        public static float StabilityProfile(HerrschaftType type)
            => StabilityProfile(type, HerrschaftParams.Default);

        /// <summary>
        /// カリスマ的支配の脆さ（0..1）。指導者個人に依存し継承で崩れやすい＝カリスマが強いほど、
        /// かつ日常化(routinization)が進んでいないほど脆い。日常化が完了すれば脆さは消える（伝統化/合法化された）。
        /// </summary>
        public static float CharismaticFragility(float charisma, float routinization, HerrschaftParams p)
        {
            float ch = Mathf.Clamp01(charisma);
            float rt = Mathf.Clamp01(routinization);
            return Mathf.Clamp01(ch * (1f - rt) * p.charismaFragilityWeight);
        }

        public static float CharismaticFragility(float charisma, float routinization)
            => CharismaticFragility(charisma, routinization, HerrschaftParams.Default);

        /// <summary>
        /// カリスマの日常化（ウェーバーのルーティン化＝#812 と接続）。カリスマを伝統化/合法化して永続させる。
        /// institutionalization（制度化＝後継・規則の整備）と強いカリスマほど速く進み、dt ぶん 0..1 へ近づける。
        /// 進んだ routinization を返す。
        /// </summary>
        public static float CharismaRoutinization(float charisma, float institutionalization, float dt, HerrschaftParams p)
        {
            float ch = Mathf.Clamp01(charisma);
            float inst = Mathf.Clamp01(institutionalization);
            float step = ch * inst * p.routinizationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(step);
        }

        public static float CharismaRoutinization(float charisma, float institutionalization, float dt)
            => CharismaRoutinization(charisma, institutionalization, dt, HerrschaftParams.Default);

        /// <summary>
        /// 近代化が伝統的権威を蝕む。modernization（合理化・近代化の進展）ぶん tradition を削り、削った後の値を返す。
        /// 近代化が伝統的支配の崩壊モード（慣習の権威の侵食）を駆動する。
        /// </summary>
        public static float TraditionErosion(float tradition, float modernization, float dt, HerrschaftParams p)
        {
            float tr = Mathf.Clamp01(tradition);
            float mod = Mathf.Clamp01(modernization);
            float loss = mod * p.traditionErosionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(tr - loss);
        }

        public static float TraditionErosion(float tradition, float modernization, float dt)
            => TraditionErosion(tradition, modernization, dt, HerrschaftParams.Default);

        /// <summary>
        /// 合法的支配の官僚制の安定（0..1）。規則の一貫性(ruleConsistency)が高いほど安定する＝
        /// 合法的支配は人ではなく規則に依る。ただし一貫性は硬直の温床でもある（崩壊モード＝硬直は別関数）。
        /// </summary>
        public static float LegalRationalBureaucracy(float legality, float ruleConsistency)
        {
            float lg = Mathf.Clamp01(legality);
            float rc = Mathf.Clamp01(ruleConsistency);
            return Mathf.Clamp01(lg * (0.4f + 0.6f * rc));
        }

        /// <summary>
        /// 類型ごとの崩壊の仕方。伝統的＝近代化で侵食／カリスマ的＝指導者の死・失敗で一挙に瓦解／
        /// 合法的＝正統性危機・規則の硬直で機能不全。
        /// </summary>
        public static HerrschaftCollapseMode CollapseMode(HerrschaftType type)
        {
            switch (type)
            {
                case HerrschaftType.伝統的:   return HerrschaftCollapseMode.近代化侵食;
                case HerrschaftType.カリスマ的: return HerrschaftCollapseMode.指導者の死失敗;
                case HerrschaftType.合法的:   return HerrschaftCollapseMode.正統性危機硬直;
                default: return HerrschaftCollapseMode.正統性危機硬直;
            }
        }

        /// <summary>
        /// 類型別の継承の安定（0..1）。合法的＝制度で円滑（後継者の明確さ heirClarity に依らず高位安定）、
        /// 伝統的＝世襲の明確さ次第（中位＋heirClarity）、カリスマ的＝後継者問題で危機（明確さが効くが脆く低位）。
        /// ＝合法は安定だがカリスマは継承で崩れる、を式に出す。
        /// </summary>
        public static float SuccessionStability(HerrschaftType type, float heirClarity)
        {
            float hc = Mathf.Clamp01(heirClarity);
            switch (type)
            {
                // 合法的：制度が継承を担う＝後継者個人に依らず高位で安定。
                case HerrschaftType.合法的:
                    return Mathf.Clamp01(0.7f + 0.3f * hc);
                // 伝統的：世襲の明確さで決まる中位安定。
                case HerrschaftType.伝統的:
                    return Mathf.Clamp01(0.45f + 0.45f * hc);
                // カリスマ的：日常化前は後継者問題で危機＝明確さが効くが上限は低い。
                case HerrschaftType.カリスマ的:
                    return Mathf.Clamp01(0.15f + 0.45f * hc);
                default:
                    return Mathf.Clamp01(0.4f + 0.4f * hc);
            }
        }
    }
}
