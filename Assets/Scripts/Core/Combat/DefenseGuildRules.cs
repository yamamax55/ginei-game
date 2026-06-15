using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 守城専門集団の純データ（墨子の墨家型＝非国家の防衛請負組織）。守城技術 expertise・規律 discipline・規模 size を持ち、
    /// 攻められる弱小国の要請に応じて守りだけを請け負う（攻撃には加担しない）。解決は <see cref="DefenseGuildRules"/> が
    /// 唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public struct DefenseGuild
    {
        public float expertise;    // 守城技術 0..1（専門家の蓄積）
        public float discipline;   // 規律 0..1（攻めない・大義に従う組織規律）
        public float size;         // 規模 0..1（駆けつける人数。寡兵でも専門技術で守る）

        public DefenseGuild(float expertise, float discipline = 1f, float size = 0.5f)
        {
            this.expertise = Mathf.Clamp01(expertise);
            this.discipline = Mathf.Clamp01(discipline);
            this.size = Mathf.Clamp01(size);
        }
    }

    /// <summary>守城専門集団の調整係数（墨家型）。</summary>
    public readonly struct DefenseGuildParams
    {
        /// <summary>守城技術が防御に与える最大上乗せ（expertise×これだけ防御倍率が増える）。</summary>
        public readonly float expertiseDefenseBonus;
        /// <summary>要塞（fortification）と組んだときの相乗の強さ。</summary>
        public readonly float fortificationSynergy;
        /// <summary>守城を請け負う最低の大義の正しさ（これ未満は金を積まれても守らない）。</summary>
        public readonly float minCauseJustness;
        /// <summary>請負判断で大義に対する報酬の重み（小さいほど理念重視＝金で動きにくい）。</summary>
        public readonly float paymentWeight;
        /// <summary>籠城1単位時間・強度1あたりの消耗（規模が減る速さ）。</summary>
        public readonly float attritionRate;
        /// <summary>守城技術を現地守備隊へ伝える速さ（去っても守りが残る）。</summary>
        public readonly float transferRate;

        public DefenseGuildParams(float expertiseDefenseBonus, float fortificationSynergy, float minCauseJustness,
            float paymentWeight, float attritionRate, float transferRate)
        {
            this.expertiseDefenseBonus = Mathf.Max(0f, expertiseDefenseBonus);
            this.fortificationSynergy = Mathf.Max(0f, fortificationSynergy);
            this.minCauseJustness = Mathf.Clamp01(minCauseJustness);
            this.paymentWeight = Mathf.Clamp01(paymentWeight);
            this.attritionRate = Mathf.Max(0f, attritionRate);
            this.transferRate = Mathf.Max(0f, transferRate);
        }

        /// <summary>
        /// 既定＝技術防御+150%・要塞相乗0.5・最低大義0.4・報酬重み0.25（理念重視）・籠城消耗0.1・技術伝承0.2。
        /// </summary>
        public static DefenseGuildParams Default => new DefenseGuildParams(1.5f, 0.5f, 0.4f, 0.25f, 0.1f, 0.2f);
    }

    /// <summary>
    /// 守城専門集団の純ロジック（#1555・墨子の墨家型）。守城専門集団は攻撃に加担せず（<see cref="RefusesOffense"/> 常に拒否）、
    /// 金より大義の正しさに応じて守りを請け負い（<see cref="WillDefend"/>）、寡兵でも専門技術で守り抜く
    /// （<see cref="SiegeResistance"/> は規模より技術・規律を重く見る）。要塞と組めば相乗で防御が跳ね上がる。
    /// 金で攻守問わず戦う傭兵は <see cref="MercenaryRules"/>、要塞の物理防御倍率は <see cref="FortressRules"/>、
    /// 攻めない理念（非攻）の判断は同 EPIC MOZI の <c>NonAggressionDoctrineRules</c> が担う（こちらは守城請負の組織を扱う＝別系統）。
    /// 全入力クランプ・乱数なし・決定論・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DefenseGuildRules
    {
        /// <summary>
        /// 守城集団の技術が防御を底上げする実効倍率（≥1.0）。技術 expertise で 1+expertiseDefenseBonus×expertise まで上がり、
        /// 要塞 fortificationLevel(0..1)と組むと fortificationSynergy×expertise×fortification ぶん相乗で更に増える。
        /// </summary>
        public static float DefensiveBonus(DefenseGuild g, float fortificationLevel, DefenseGuildParams p)
        {
            float exp = Mathf.Clamp01(g.expertise);
            float fort = Mathf.Clamp01(fortificationLevel);
            float bonus = p.expertiseDefenseBonus * exp;
            float synergy = p.fortificationSynergy * exp * fort;
            return 1f + bonus + synergy;
        }

        public static float DefensiveBonus(DefenseGuild g, float fortificationLevel)
            => DefensiveBonus(g, fortificationLevel, DefenseGuildParams.Default);

        /// <summary>
        /// 守城専門が攻城に耐える度合い（0..1）。専門家は寡兵でも守り抜く＝技術・規律を重く（0.7）、規模を軽く（0.3）見た
        /// 守備力を、攻撃側 attackerStrength(0..1)に対する余力として返す。守備力が攻撃を上回るほど 1 に近づく。
        /// </summary>
        public static float SiegeResistance(DefenseGuild g, float attackerStrength, DefenseGuildParams p)
        {
            float skill = 0.7f * Mathf.Clamp01(g.expertise) + 0.3f * Mathf.Clamp01(g.size);
            // 規律は崩れにくさ＝守備力の底上げ（実効値、基準は非破壊）
            float defense = skill * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(g.discipline));
            float attack = Mathf.Clamp01(attackerStrength);
            // 寡兵でも専門技術で守り抜く：守備が攻撃を上回るぶんを耐性として 0..1 に写す
            return Mathf.Clamp01(defense / Mathf.Max(1e-4f, defense + attack));
        }

        public static float SiegeResistance(DefenseGuild g, float attackerStrength)
            => SiegeResistance(g, attackerStrength, DefenseGuildParams.Default);

        /// <summary>
        /// 守城を請け負うか＝大義の正しさ causeJustness(0..1)が最低基準を満たし、かつ大義＋報酬の総合が閾値0.5以上。
        /// 傭兵と違い理念重視＝報酬 payment(0..1)の重みは小さく、金だけ積まれても大義が無ければ動かない。
        /// </summary>
        public static bool WillDefend(DefenseGuild g, float causeJustness, float payment, DefenseGuildParams p)
        {
            float cause = Mathf.Clamp01(causeJustness);
            if (cause < p.minCauseJustness) return false;
            float pay = Mathf.Clamp01(payment);
            // 規律ある集団ほど大義に忠実：大義の重みは (1−paymentWeight)、報酬は paymentWeight
            float appeal = (1f - p.paymentWeight) * cause + p.paymentWeight * pay;
            return appeal >= 0.5f;
        }

        public static bool WillDefend(DefenseGuild g, float causeJustness, float payment)
            => WillDefend(g, causeJustness, payment, DefenseGuildParams.Default);

        /// <summary>
        /// 攻撃要請は規律ゆえ常に拒否（守城専門＝攻めない）。組織が組織として在る（規律&gt;0）限り true。
        /// </summary>
        public static bool RefusesOffense(DefenseGuild g) => g.discipline >= 0f; // 守城専門は常に攻撃を拒む

        /// <summary>
        /// 守り抜いた／守れなかったときの名声の増減。弱者を守り抜く（successfulDefense=true）と技術と規律に比例して名声が上がり、
        /// 守れなければその逆で下がる（墨家の評判）。
        /// </summary>
        public static float ReputationGain(DefenseGuild g, bool successfulDefense, DefenseGuildParams p)
        {
            float merit = Mathf.Clamp01(0.5f * Mathf.Clamp01(g.expertise) + 0.5f * Mathf.Clamp01(g.discipline));
            return successfulDefense ? merit : -merit;
        }

        public static float ReputationGain(DefenseGuild g, bool successfulDefense)
            => ReputationGain(g, successfulDefense, DefenseGuildParams.Default);

        /// <summary>
        /// 長期の籠城で集団が消耗した後の規模（0..1）。siegeIntensity(0..1)×dt×attritionRate ぶん size が減る。
        /// 規律が高いほど踏みとどまり消耗が緩む（実効値、基準フィールドは非破壊で新しい size を返す）。
        /// </summary>
        public static float AttritionUnderSiege(DefenseGuild g, float siegeIntensity, float dt, DefenseGuildParams p)
        {
            float intensity = Mathf.Clamp01(siegeIntensity);
            float resist = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(g.discipline)); // 規律で消耗半減まで
            float loss = intensity * Mathf.Max(0f, dt) * p.attritionRate * resist;
            return Mathf.Clamp01(g.size - loss);
        }

        public static float AttritionUnderSiege(DefenseGuild g, float siegeIntensity, float dt)
            => AttritionUnderSiege(g, siegeIntensity, dt, DefenseGuildParams.Default);

        /// <summary>
        /// 守城技術を現地守備隊へ伝えた後の守備隊技量（0..1）。集団の技術 expertise を上限に、現地 localGarrison(0..1)が
        /// transferRate で近づく＝去っても守りが残る。集団技術を超えては伝わらない。
        /// </summary>
        public static float KnowledgeTransfer(DefenseGuild g, float localGarrison, DefenseGuildParams p)
        {
            float local = Mathf.Clamp01(localGarrison);
            float ceiling = Mathf.Clamp01(g.expertise);
            float taught = Mathf.MoveTowards(local, ceiling, p.transferRate);
            return Mathf.Clamp01(Mathf.Max(local, taught)); // 伝承は守りを減らさない
        }

        public static float KnowledgeTransfer(DefenseGuild g, float localGarrison)
            => KnowledgeTransfer(g, localGarrison, DefenseGuildParams.Default);

        /// <summary>難攻不落の守りを成立させたか＝防御倍率 defensiveBonus が threshold 以上。</summary>
        public static bool IsImpregnableDefense(float defensiveBonus, float threshold)
        {
            return defensiveBonus >= threshold;
        }
    }
}
