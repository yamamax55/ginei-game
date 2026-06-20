using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 継承危機・内乱・クーデターの薄いオーケストレータ（純ロジック・唯一の合成窓口）。
    /// 既存の未配線 Core を<b>合成して呼ぶだけ</b>＝数式は持たず委譲する：
    ///   ・後継指名/正統性論争＝<see cref="HeirDesignationRules"/>・<see cref="SuccessionRules"/>
    ///   ・継承戦争（請求者並立→無政府化）＝<see cref="SuccessionWarRules"/>
    ///   ・内乱の長期帰結（経済崩壊/対外無防備/雪崩）＝<see cref="CivilWarRules"/>
    ///   ・クーデター（発生確率/帰結/事後正統性）＝<see cref="CoupRules"/>
    ///   ・軍政関係のクーデター素地＝<see cref="CivilianControlRules"/>（CoupRisk/WouldCoup）
    ///   ・摂政・幼君（簒奪誘惑）＝<see cref="RegencyRules"/>
    /// 「曖昧な継承が国を割り、不満な軍が政権を奪う」一連の危機連鎖を1か所で組む。
    /// <b>状態は一切変えない</b>（リスク/判定/係数を返すだけ）。乱数は roll 引数で決定論。
    /// 並行システムを作らず既存ルールへ橋渡しする（重複実装しない）。test-first。
    /// </summary>
    public static class SuccessionCrisisRules
    {
        /// <summary>
        /// 継承紛争リスク（0..1）。後継者が指名されておらず（hasDesignatedHeir=false）、正統性が低く、
        /// 派閥対立（factionalism）が強いほど高い。
        /// 指名後継の有無は <see cref="HeirDesignationRules"/> の世界観（指名＝危機を抑える）に、
        /// 正統性論争は <see cref="SuccessionRules.LegitimacyConflict"/> に委譲して合成する。
        /// 指名後継があれば紛争の素地は大きく減じる（曖昧さの解消）。
        /// </summary>
        public static float SuccessionDisputeRisk(bool hasDesignatedHeir, float legitimacy, float factionalism)
        {
            // 正統性が低いほど先代の宿老が従わない＝論争の素地（既存式へ委譲）。
            float legitConflict = SuccessionRules.LegitimacyConflict(legitimacy); // = 1 - clamp01(legitimacy)
            float faction = Mathf.Clamp01(factionalism);
            // 論争の素地と派閥対立の合成（どちらも高いほど跳ね上がる）。
            float raw = Mathf.Clamp01(legitConflict * (1f + faction) * 0.5f + faction * legitConflict * 0.5f);
            // 明確な指名後継があれば曖昧さが解け、紛争リスクは大きく減る。
            float clarity = hasDesignatedHeir ? 0.25f : 1f;
            return Mathf.Clamp01(raw * clarity);
        }

        /// <summary>
        /// 継承紛争を請求者並立として見たときの危機の深刻度（0..1）＝<see cref="SuccessionWarRules.CrisisSeverity"/> へ委譲。
        /// claimantCount&lt;=1（明確な世継ぎ）なら0、請求者が多く後継が曖昧（heirClarity低）なほど高い。
        /// </summary>
        public static float ClaimantCrisisSeverity(int claimantCount, float heirClarity)
            => SuccessionWarRules.CrisisSeverity(claimantCount, heirClarity);

        /// <summary>
        /// 内乱（内戦）が勃発するか＝継承紛争リスク（disputeRisk）が閾値以上。
        /// 紛争が一定を超えると国が二分して長期消耗戦へ＝以後の帰結は <see cref="CivilWarRules"/> が担う。
        /// </summary>
        public static bool WouldEruptCivilWar(float disputeRisk, float threshold)
            => Mathf.Clamp01(disputeRisk) >= Mathf.Clamp01(threshold);

        /// <summary>
        /// 内乱勃発時の中央権威の崩壊度（0..1）＝請求者の趨勢シェアから <see cref="SuccessionWarRules"/> 経由で導く。
        /// 拮抗する請求者が並立するほど無政府化し中央が裁定できない（係数を返すだけ＝状態非破壊）。
        /// </summary>
        public static float CentralAuthorityCollapse(float[] claimantStrengths)
            => SuccessionWarRules.CentralAuthorityCollapse(SuccessionWarRules.AnarchyLevel(claimantStrengths));

        /// <summary>
        /// クーデターの発生・成功確率（0..1）＝<see cref="CoupRules.CoupSuccessChance"/> へ委譲。
        /// 軍の忠誠（militaryLoyalty）・政治的支持（politicalSupport）が低いほど、主体（<see cref="CoupType"/>）の
        /// 主因に応じて高まる。
        /// </summary>
        public static float CoupChance(float militaryLoyalty, float politicalSupport, CoupType type)
            => CoupRules.CoupSuccessChance(militaryLoyalty, politicalSupport, type);

        /// <summary>
        /// 軍政関係（文民統制型）からのクーデター素地リスク（0..1）＝<see cref="CivilianControlRules.CoupRisk"/> への橋渡し。
        /// 統制が弱い・支持が低い・敗戦直後ほど高い。
        /// </summary>
        public static float CoupRisk(CivilianControlType controlType, float controlStrength, float support, bool recentDefeat)
            => CivilianControlRules.CoupRisk(controlType, controlStrength, support, recentDefeat);

        /// <summary>
        /// 軍が独走・クーデターへ踏み切るか＝<see cref="CivilianControlRules.WouldCoup"/> への橋渡し（既定閾値）。
        /// </summary>
        public static bool WouldCoup(CivilianControlType controlType, float controlStrength, float support, bool recentDefeat)
            => CivilianControlRules.WouldCoup(controlType, controlStrength, support, recentDefeat);

        /// <summary>
        /// クーデターの帰結を解決する（成功/粛清/内戦）＝<see cref="CoupRules.Resolve"/> へ委譲。roll(0..1) で決定論。
        /// chance は <see cref="CoupChance"/> または <see cref="CoupRisk"/> から渡す想定。
        /// </summary>
        public static CoupOutcome ResolveCoup(float chance, float roll)
            => CoupRules.Resolve(chance, roll);

        /// <summary>
        /// 摂政の簒奪が危険水準か＝<see cref="RegencyRules.UsurpationLooms"/> への橋渡し。
        /// 幼君の継承（未成年）では摂政の実権・野心・成人接近度から簒奪危機が膨らむ。
        /// </summary>
        public static bool RegentUsurpationLooms(float regentPower, float ambition, int monarchAge)
            => RegencyRules.UsurpationLooms(regentPower, ambition, monarchAge);
    }
}
