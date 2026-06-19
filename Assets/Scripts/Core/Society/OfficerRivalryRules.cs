using UnityEngine;

namespace Ginei
{
    /// <summary>将官の確執（派閥・反目・連携）の調整係数。</summary>
    public readonly struct OfficerRivalryParams
    {
        /// <summary>確執の強さのスケール（双方の功名心×地位の近さに掛ける）。</summary>
        public readonly float rivalryScale;
        /// <summary>協調摩擦のスケール（確執×(1-共通目標) に掛ける）。</summary>
        public readonly float frictionScale;
        /// <summary>派閥分極のスケール（派閥忠誠×思想距離に掛ける）。</summary>
        public readonly float polarizationScale;
        /// <summary>友誼のスケール（苦楽の共有×相互敬意の幾何平均に掛ける）。</summary>
        public readonly float camaraderieScale;
        /// <summary>共同作戦効率で摩擦を引く重み（友誼−摩擦×この重み）。</summary>
        public readonly float frictionWeight;
        /// <summary>不服従判定で上官の正統性に掛ける重み（高いほど不服従が芽生えにくい）。</summary>
        public readonly float legitimacyWeight;
        /// <summary>手柄横取り争いのスケール（確執×野心に掛ける）。</summary>
        public readonly float creditScale;

        public OfficerRivalryParams(float rivalryScale, float frictionScale, float polarizationScale,
                                    float camaraderieScale, float frictionWeight, float legitimacyWeight,
                                    float creditScale)
        {
            this.rivalryScale = Mathf.Max(0f, rivalryScale);
            this.frictionScale = Mathf.Max(0f, frictionScale);
            this.polarizationScale = Mathf.Max(0f, polarizationScale);
            this.camaraderieScale = Mathf.Max(0f, camaraderieScale);
            this.frictionWeight = Mathf.Max(0f, frictionWeight);
            this.legitimacyWeight = Mathf.Max(0f, legitimacyWeight);
            this.creditScale = Mathf.Max(0f, creditScale);
        }

        /// <summary>既定＝確執1/摩擦1/分極1/友誼1/摩擦重み1/正統性重み1/横取り1。</summary>
        public static OfficerRivalryParams Default
            => new OfficerRivalryParams(1f, 1f, 1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 将官の確執（派閥・反目・連携）の純ロジック。提督同士の功名争い・反目による協調の乱れ・派閥対立・
    /// 友誼による連携強化・確執が作戦に与える摩擦を式に出す（銀英伝の門閥貴族の内輪もめ／同盟軍の主導権争い型）。
    /// 近い地位で野心家同士ほど確執は激しく、共通目標が確執の摩擦を和らげ、苦楽を共にした友誼が共同作戦を支える。
    /// 上官の正統性が確執に勝てば命令は通り、負ければ不服従の芽が出る。功名心は手柄の横取り争いを煽る。
    /// 倍率・係数は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし決定論・全入力クランプ。
    /// 純ロジック（非 MonoBehaviour・test-first）。野心 ambition・地位 status は 0..100、その他比率は 0..1 を想定。
    /// </summary>
    public static class OfficerRivalryRules
    {
        /// <summary>
        /// 確執の強さ（0..1）。双方の功名心 ambitionA/ambitionB（0..100）の平均と地位の近さ（1-statusGap）の積。
        /// 近い地位で野心家同士ほど激しく反目し、どちらかが無欲か地位差が大きければ確執は薄れる。
        /// </summary>
        public static float RivalryIntensity(float ambitionA, float ambitionB, float statusGap, OfficerRivalryParams p)
        {
            float a = Mathf.Clamp(ambitionA, 0f, 100f) / 100f;
            float b = Mathf.Clamp(ambitionB, 0f, 100f) / 100f;
            float ambAvg = (a + b) * 0.5f;
            float proximity = 1f - Mathf.Clamp01(statusGap);
            return Mathf.Clamp01(ambAvg * proximity * p.rivalryScale);
        }

        public static float RivalryIntensity(float ambitionA, float ambitionB, float statusGap)
            => RivalryIntensity(ambitionA, ambitionB, statusGap, OfficerRivalryParams.Default);

        /// <summary>
        /// 協調の摩擦（0..1）。確執 rivalryIntensity に「共通目標の不在」(1-sharedGoal) を掛ける＝
        /// 共通の目標があれば反目も抑えられ、目標がばらばらなら確執がそのまま協調を乱す。
        /// </summary>
        public static float CooperationFriction(float rivalryIntensity, float sharedGoal, OfficerRivalryParams p)
        {
            float r = Mathf.Clamp01(rivalryIntensity);
            float g = Mathf.Clamp01(sharedGoal);
            return Mathf.Clamp01(r * (1f - g) * p.frictionScale);
        }

        public static float CooperationFriction(float rivalryIntensity, float sharedGoal)
            => CooperationFriction(rivalryIntensity, sharedGoal, OfficerRivalryParams.Default);

        /// <summary>
        /// 派閥の分極（0..1）。派閥忠誠 factionLoyalty×思想距離 ideologicalDistance の積＝
        /// 派閥への忠誠が強く思想が遠いほど対立が深まる（門閥貴族の派閥抗争・主流／反主流の分断）。
        /// </summary>
        public static float FactionPolarization(float factionLoyalty, float ideologicalDistance, OfficerRivalryParams p)
        {
            float l = Mathf.Clamp01(factionLoyalty);
            float d = Mathf.Clamp01(ideologicalDistance);
            return Mathf.Clamp01(l * d * p.polarizationScale);
        }

        public static float FactionPolarization(float factionLoyalty, float ideologicalDistance)
            => FactionPolarization(factionLoyalty, ideologicalDistance, OfficerRivalryParams.Default);

        /// <summary>
        /// 友誼（0..1）。苦楽を共にした度合い sharedHardship×相互敬意 mutualRespect の幾何平均＝
        /// どちらか一方だけでは厚い友誼にならず、苦楽の共有と相互敬意が揃って初めて連携が育つ（双璧型の盟友）。
        /// </summary>
        public static float Camaraderie(float sharedHardship, float mutualRespect, OfficerRivalryParams p)
        {
            float h = Mathf.Clamp01(sharedHardship);
            float r = Mathf.Clamp01(mutualRespect);
            return Mathf.Clamp01(Mathf.Sqrt(h * r) * p.camaraderieScale);
        }

        public static float Camaraderie(float sharedHardship, float mutualRespect)
            => Camaraderie(sharedHardship, mutualRespect, OfficerRivalryParams.Default);

        /// <summary>
        /// 共同作戦の効率（-1..1）。友誼 camaraderie から協調摩擦 cooperationFriction×frictionWeight を引く＝
        /// 厚い友誼ほど連携が高まり（正）、確執の摩擦が勝てば足を引っ張り合う（負）。
        /// </summary>
        public static float JointOperationEffect(float camaraderie, float cooperationFriction, OfficerRivalryParams p)
        {
            float c = Mathf.Clamp01(camaraderie);
            float f = Mathf.Clamp01(cooperationFriction);
            return Mathf.Clamp(c - f * p.frictionWeight, -1f, 1f);
        }

        public static float JointOperationEffect(float camaraderie, float cooperationFriction)
            => JointOperationEffect(camaraderie, cooperationFriction, OfficerRivalryParams.Default);

        /// <summary>
        /// 命令不服従の芽（0..1）。確執 rivalryIntensity を「確執＋上官の正統性×legitimacyWeight」で割る＝
        /// 上官の正統性が確執に勝てば命令は通り（低い）、確執が正統性を上回れば不服従が芽生える（高い）。
        /// </summary>
        public static float Insubordination(float rivalryIntensity, float superiorLegitimacy, OfficerRivalryParams p)
        {
            float r = Mathf.Clamp01(rivalryIntensity);
            float leg = Mathf.Clamp01(superiorLegitimacy) * p.legitimacyWeight;
            float denom = r + leg;
            if (denom <= 0f) return 0f; // 確執も正統性も無ければ不服従なし
            return Mathf.Clamp01(r / denom);
        }

        public static float Insubordination(float rivalryIntensity, float superiorLegitimacy)
            => Insubordination(rivalryIntensity, superiorLegitimacy, OfficerRivalryParams.Default);

        /// <summary>
        /// 手柄の横取り争い（0..1）。確執 rivalryIntensity×当人の功名心 ambitionA（0..100）＝
        /// 反目している相手で野心家ほど互いの手柄を奪い合う（功を焦って抜け駆け・報告の歪曲）。
        /// </summary>
        public static float CreditRivalry(float rivalryIntensity, float ambitionA, OfficerRivalryParams p)
        {
            float r = Mathf.Clamp01(rivalryIntensity);
            float a = Mathf.Clamp(ambitionA, 0f, 100f) / 100f;
            return Mathf.Clamp01(r * a * p.creditScale);
        }

        public static float CreditRivalry(float rivalryIntensity, float ambitionA)
            => CreditRivalry(rivalryIntensity, ambitionA, OfficerRivalryParams.Default);

        /// <summary>協力できるか（共同作戦効率が閾値以上）。</summary>
        public static bool WillCooperate(float jointOperationEffect, float threshold)
            => Mathf.Clamp(jointOperationEffect, -1f, 1f) >= Mathf.Clamp(threshold, -1f, 1f);

        /// <summary>協力できるか（既定閾値0＝友誼が摩擦を上回るか）。</summary>
        public static bool WillCooperate(float jointOperationEffect)
            => WillCooperate(jointOperationEffect, 0f);
    }
}
