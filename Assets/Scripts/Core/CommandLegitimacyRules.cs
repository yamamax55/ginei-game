using UnityEngine;

namespace Ginei
{
    /// <summary>会戦指揮への将兵の服従段階（正統性が高いほど完全服従・低いほど不服従へ）。</summary>
    public enum ObedienceLevel
    {
        完全服従,       // 正統な指揮＝命令どおり動く
        渋々服従,       // 一応従うが切れ味が鈍る（デバフ）
        部分的不服従,   // 危険・無理な命令は拒みうる
        不服従          // 指揮が崩壊＝命令が通らない
    }

    /// <summary>会戦指揮の正統性ロジックの調整係数。</summary>
    public readonly struct CommandLegitimacyParams
    {
        /// <summary>これ以上の正統性で完全服従（渋々服従との境界）。</summary>
        public readonly float fullObedienceThreshold;
        /// <summary>これ以上の正統性で渋々服従（部分的不服従との境界）。</summary>
        public readonly float reluctantThreshold;
        /// <summary>これ以上の正統性で部分的不服従（不服従との境界）。</summary>
        public readonly float partialThreshold;
        /// <summary>正統性が低いときの命令実行倍率の下限（完全な不服従でもこの程度は動く）。</summary>
        public readonly float minComplianceFactor;
        /// <summary>正統性欠如の士気ペナルティの最大幅。</summary>
        public readonly float moralePenaltyScale;
        /// <summary>勝利による正統性強化の最大幅（勝てば求心力が増す）。</summary>
        public readonly float victoryAuthorityGain;

        public CommandLegitimacyParams(float fullObedienceThreshold, float reluctantThreshold,
                                       float partialThreshold, float minComplianceFactor,
                                       float moralePenaltyScale, float victoryAuthorityGain)
        {
            this.fullObedienceThreshold = Mathf.Clamp01(fullObedienceThreshold);
            this.reluctantThreshold = Mathf.Clamp01(reluctantThreshold);
            this.partialThreshold = Mathf.Clamp01(partialThreshold);
            this.minComplianceFactor = Mathf.Clamp01(minComplianceFactor);
            this.moralePenaltyScale = Mathf.Max(0f, moralePenaltyScale);
            this.victoryAuthorityGain = Mathf.Max(0f, victoryAuthorityGain);
        }

        /// <summary>既定＝完全服従0.75・渋々0.5・部分0.25・命令倍率下限0.4・士気減0.3・勝利強化0.15。</summary>
        public static CommandLegitimacyParams Default =>
            new CommandLegitimacyParams(0.75f, 0.5f, 0.25f, 0.4f, 0.3f, 0.15f);
    }

    /// <summary>
    /// 会戦指揮の正統性の純ロジック（#898）。将兵が指揮官の命令に従うのは、指揮権の<b>正統性</b>ゆえ＝
    /// 正式な任命（formalAuthority）だけでなく、本人の武威・実績（personalProwess）と将兵の忠誠（soldierLoyalty）の
    /// 三つが揃って初めて将兵は従う。「なぜプレイヤーは会戦を指揮できるのか」をモデル化する。正統な指揮なら完全服従、
    /// 正統性が低いと命令の切れ味が鈍り（デバフ）、危険な命令は部分的に拒まれ、果ては不服従に至る。
    /// 戦場での命令服従に特化し、軍政の上下＝クーデターリスク（`CivilianControlRules` #145）・軍紀の引き締めと
    /// 抗命（`DisciplineRules`）・諸侯の旗幟と寝返り（`LoyaltyRules` #817）・集団的な艦隊反乱（`MutinyRules`）とは別系統＝
    /// 「個々の命令に従うか」だけを扱う。乱数は外から与える roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommandLegitimacyRules
    {
        /// <summary>
        /// 指揮の正統性（0..1）＝正式な指揮権 × 本人の武威・実績 × 将兵の忠誠。三つの積ゆえ、どれか一つでも
        /// 欠ければ将兵はついてこない（任命だけの臆病者にも、人望なき猛将にも全力では従わない）。
        /// </summary>
        public static float CommandLegitimacy(float formalAuthority, float personalProwess, float soldierLoyalty)
        {
            float a = Mathf.Clamp01(formalAuthority);
            float p = Mathf.Clamp01(personalProwess);
            float l = Mathf.Clamp01(soldierLoyalty);
            return Mathf.Clamp01(a * p * l);
        }

        /// <summary>正統性から服従段階を導く（高いほど完全服従・低いほど不服従へ）。</summary>
        public static ObedienceLevel ObedienceLevel(float legitimacy, CommandLegitimacyParams prm)
        {
            float v = Mathf.Clamp01(legitimacy);
            if (v >= prm.fullObedienceThreshold) return Ginei.ObedienceLevel.完全服従;
            if (v >= prm.reluctantThreshold) return Ginei.ObedienceLevel.渋々服従;
            if (v >= prm.partialThreshold) return Ginei.ObedienceLevel.部分的不服従;
            return Ginei.ObedienceLevel.不服従;
        }

        public static ObedienceLevel ObedienceLevel(float legitimacy)
            => ObedienceLevel(legitimacy, CommandLegitimacyParams.Default);

        /// <summary>
        /// 命令実行の倍率（minComplianceFactor..1）＝正統性が低いほど命令の動きが鈍る（デバフ）。
        /// 正統性1.0で満額、0で下限まで。基準値に掛けて使う（実効値パターン・基準非破壊）。
        /// </summary>
        public static float OrderComplianceFactor(float legitimacy, CommandLegitimacyParams prm)
        {
            return Mathf.Lerp(prm.minComplianceFactor, 1f, Mathf.Clamp01(legitimacy));
        }

        public static float OrderComplianceFactor(float legitimacy)
            => OrderComplianceFactor(legitimacy, CommandLegitimacyParams.Default);

        /// <summary>
        /// 部分的不服従の確率（0..1）＝正統性の不足分（1−正統性）× 命令の危険度（orderRisk）。
        /// 危険・無理な命令ほど、正統性が低いと従われない（無理な突撃は拒まれる）。安全な命令（risk=0）は
        /// 正統性が低くても拒まれず、危険な命令でも正統性が高ければ通る。
        /// </summary>
        public static float PartialDisobedienceRisk(float legitimacy, float orderRisk)
        {
            float shortfall = 1f - Mathf.Clamp01(legitimacy);
            return Mathf.Clamp01(shortfall * Mathf.Clamp01(orderRisk));
        }

        /// <summary>部分的不服従の判定。roll∈[0,1) がリスク未満なら命令拒否＝true（決定論）。</summary>
        public static bool PartialDisobedienceOccurs(float legitimacy, float orderRisk, float roll)
        {
            return roll < PartialDisobedienceRisk(legitimacy, orderRisk);
        }

        /// <summary>
        /// 正統性欠如の士気ペナルティ（0..moralePenaltyScale）＝（1−正統性）に比例。
        /// 納得できない指揮官の下では士気が下がる。正統性1.0なら0。`FleetMorale`（Game層）へ係数として渡す想定。
        /// </summary>
        public static float MoralePenaltyFromIllegitimacy(float legitimacy, CommandLegitimacyParams prm)
        {
            return (1f - Mathf.Clamp01(legitimacy)) * prm.moralePenaltyScale;
        }

        public static float MoralePenaltyFromIllegitimacy(float legitimacy)
            => MoralePenaltyFromIllegitimacy(legitimacy, CommandLegitimacyParams.Default);

        /// <summary>
        /// 勝利による正統性の強化（0..1）。勝てば求心力が増す＝実績が指揮権を固める＝現正統性に
        /// 残り余地（1−正統性）× victoryAuthorityGain を上乗せ。敗北（victory=false）は据え置き。
        /// </summary>
        public static float AuthorityFromVictory(float legitimacy, bool victory, CommandLegitimacyParams prm)
        {
            float v = Mathf.Clamp01(legitimacy);
            if (!victory) return v;
            return Mathf.Clamp01(v + (1f - v) * prm.victoryAuthorityGain);
        }

        public static float AuthorityFromVictory(float legitimacy, bool victory)
            => AuthorityFromVictory(legitimacy, victory, CommandLegitimacyParams.Default);
    }
}
