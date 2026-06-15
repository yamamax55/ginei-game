using UnityEngine;

namespace Ginei
{
    /// <summary>多極講和の調整係数（マジックナンバー禁止＝集約）。</summary>
    public readonly struct MultipartyPeaceParams
    {
        /// <summary>当事者数が交渉の複雑さへ寄与する重み（組み合わせ爆発の強さ）。</summary>
        public readonly float partyCountWeight;
        /// <summary>要求の対立が交渉の複雑さへ寄与する重み。</summary>
        public readonly float demandConflictWeight;
        /// <summary>戦争継続の選択肢（BATNA）がごね得を押し上げる重み。</summary>
        public readonly float alternativeWeight;
        /// <summary>包括合意が成立とみなされる成立見込みの既定閾値。</summary>
        public readonly float comprehensiveThreshold;
        /// <summary>仲介者の信頼が複雑さを切り崩す強さ。</summary>
        public readonly float mediatorEffectiveness;

        public MultipartyPeaceParams(float partyCountWeight, float demandConflictWeight,
            float alternativeWeight, float comprehensiveThreshold, float mediatorEffectiveness)
        {
            this.partyCountWeight = Mathf.Max(0f, partyCountWeight);
            this.demandConflictWeight = Mathf.Max(0f, demandConflictWeight);
            this.alternativeWeight = Mathf.Max(0f, alternativeWeight);
            this.comprehensiveThreshold = Mathf.Clamp01(comprehensiveThreshold);
            this.mediatorEffectiveness = Mathf.Max(0f, mediatorEffectiveness);
        }

        /// <summary>既定＝当事者数重み0.6/要求対立重み0.4・BATNA重み0.5・包括成立閾値0.5・仲介効力0.6。</summary>
        public static MultipartyPeaceParams Default
            => new MultipartyPeaceParams(0.6f, 0.4f, 0.5f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 多極講和（ウェストファリア型）の協調問題の純ロジック（三十年戦争・TYW-4 #1427・唯一の窓口）。
    /// 二者間の講和（<see cref="WarGoalRules"/> の <see cref="WarGoalRules.PeaceAcceptance"/>＝戦況・厭戦で講和に傾く）と違い、
    /// 三者以上の戦争の講和は各国の要求が絡み合い、<b>一つの包括パッケージとして全員が同時に合意</b>せねばならない。
    /// 誰か一人が拒否すれば全体が崩れ、交渉が膠着して戦争が長引く（三十年戦争の和平交渉は数年を要した）。
    /// <list type="bullet">
    /// <item><see cref="NegotiationComplexity"/>＝当事者数×要求対立で複雑さ（当事者が増えるほど組み合わせ爆発）。</item>
    /// <item><see cref="PackageDealFeasibility"/>＝各当事者の満足度配列から包括パッケージ成立見込み（最弱当事者が律速）。</item>
    /// <item><see cref="HoldoutPower"/>＝一当事者が合意を拒否できる力（戦争継続の選択肢があるほど強気＝ごね得）。</item>
    /// <item><see cref="SpoilerRisk"/>＝合意を妨害する当事者（spoiler）のリスク。</item>
    /// <item><see cref="StalemateDetection"/>＝包括合意が成立せず膠着している検知（戦争が長引く）。</item>
    /// <item><see cref="PartialAgreement"/>＝全員でなく一部だけで部分合意（分割講和＝完全合意の代替）。</item>
    /// <item><see cref="MediatorValue"/>＝中立の仲介者が複雑な多国間交渉をまとめる価値。</item>
    /// <item><see cref="IsComprehensivePeace"/>＝全当事者が合意した包括的和平の判定。</item>
    /// </list>
    /// 分担：<see cref="CoalitionRules"/> は議会内で多党が政権を組む構造力学、
    /// <see cref="PartitionRules"/> は戦後の領土分割、<see cref="SovereigntyNormRules"/>（同 EPIC TYW・生成済み）は
    /// ウェストファリアの主権規範。本クラスは多国間講和そのものの協調問題（包括パッケージ合意の難しさ）を扱う。
    /// 全入力クランプ・配列 null/空安全・乱数なしの決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MultipartyPeaceRules
    {
        /// <summary>0..1 に丸める。</summary>
        public static float Clamp01(float v) => Mathf.Clamp01(v);

        /// <summary>
        /// 交渉の複雑さ（0..1）。当事者数(partyCount 0..1＝当事者の多さの正規化)×重みと
        /// 要求の対立(demandConflict 0..1)×重みの加重和。当事者が増えるほど組み合わせ爆発で複雑になり、
        /// さらに当事者数×要求対立の相乗ぶんを足す（多者かつ要求が衝突すると交渉は跳ね上がる）。
        /// </summary>
        public static float NegotiationComplexity(float partyCount, float demandConflict, MultipartyPeaceParams p)
        {
            float count = Mathf.Clamp01(partyCount);
            float conflict = Mathf.Clamp01(demandConflict);
            float weightSum = p.partyCountWeight + p.demandConflictWeight;
            if (weightSum <= 0f) return 0f;
            float linear = (count * p.partyCountWeight + conflict * p.demandConflictWeight) / weightSum;
            // 組み合わせ爆発：当事者数×要求対立の相乗ぶんを上乗せ。
            float synergy = count * conflict;
            return Clamp01(linear * (1f - synergy) + synergy);
        }

        /// <summary>既定 Params で解決。</summary>
        public static float NegotiationComplexity(float partyCount, float demandConflict)
            => NegotiationComplexity(partyCount, demandConflict, MultipartyPeaceParams.Default);

        /// <summary>
        /// 包括パッケージの成立見込み（0..1）＝各当事者の満足度(partySatisfactions 各0..1)の<b>最小値</b>。
        /// 全員が最低限満足せねば成立しない＝最弱当事者が律速（誰か一人が不満なら包括合意は崩れる）。
        /// 手書きループで最小を取る。null・空配列は当事者不在＝成立見込み0。
        /// </summary>
        public static float PackageDealFeasibility(float[] partySatisfactions)
        {
            if (partySatisfactions == null || partySatisfactions.Length == 0) return 0f;
            float min = 1f;
            for (int i = 0; i < partySatisfactions.Length; i++)
            {
                float s = Mathf.Clamp01(partySatisfactions[i]);
                if (s < min) min = s;
            }
            return Clamp01(min);
        }

        /// <summary>
        /// ごね得の力（0..1）＝一当事者が合意を拒否できる力。自らのシェア(partyShare 0..1＝交渉での重み)に、
        /// 戦争継続という代替案(alternativeToAgreement 0..1＝BATNA＝合意しなくてもやっていける度合い)を加味。
        /// 代替案があるほど強気に拒否でき、ごね得が大きい（最弱でも拒否権1枚で全体を割れる）。
        /// </summary>
        public static float HoldoutPower(float partyShare, float alternativeToAgreement, MultipartyPeaceParams p)
        {
            float share = Mathf.Clamp01(partyShare);
            float alt = Mathf.Clamp01(alternativeToAgreement);
            // 代替案が拒否カードの価値を底上げする（掛け算でなく上乗せ＝シェア0でも代替が強ければ粘れる）。
            return Clamp01(share + alt * p.alternativeWeight);
        }

        /// <summary>既定 Params で解決。</summary>
        public static float HoldoutPower(float partyShare, float alternativeToAgreement)
            => HoldoutPower(partyShare, alternativeToAgreement, MultipartyPeaceParams.Default);

        /// <summary>
        /// 妨害者（spoiler）のリスク（0..1）＝合意を壊しにかかる当事者の危険度。拒否できる力(holdoutPower 0..1)が
        /// 高いほど、かつ妨害で得られる限界利得(marginalGain 0..1＝壊して粘れば取り分が増す見込み)が高いほど高い。
        /// 力があっても壊す動機が無ければ妨害しない＝両者の積。
        /// </summary>
        public static float SpoilerRisk(float holdoutPower, float marginalGain)
        {
            return Clamp01(Mathf.Clamp01(holdoutPower) * Mathf.Clamp01(marginalGain));
        }

        /// <summary>
        /// 膠着の検知＝包括合意の成立見込み(packageDealFeasibility 0..1)が閾値(threshold)未満なら true
        /// （包括合意が成立せず交渉が膠着している＝戦争が長引く）。
        /// </summary>
        public static bool StalemateDetection(float packageDealFeasibility, float threshold)
        {
            return Mathf.Clamp01(packageDealFeasibility) < Mathf.Clamp01(threshold);
        }

        /// <summary>既定の包括成立閾値で膠着検知。</summary>
        public static bool StalemateDetection(float packageDealFeasibility)
            => StalemateDetection(packageDealFeasibility, MultipartyPeaceParams.Default.comprehensiveThreshold);

        /// <summary>
        /// 部分合意の成立見込み（0..1）＝全員でなく一部の当事者だけで手を打つ（分割講和＝完全合意の代替）。
        /// 当事者を満足度の高い順に上から coalitionSubset（0..1＝参加させる割合）ぶんだけ取り、
        /// その部分集合の<b>最弱当事者の満足度</b>を成立見込みとする（参加者を絞れば最弱が引き上がる）。
        /// 手書きで上位 k 件の最小を選ぶ（部分ソート不要＝閾値走査）。null・空配列・subset≤0 は0。
        /// </summary>
        public static float PartialAgreement(float[] partySatisfactions, float coalitionSubset)
        {
            if (partySatisfactions == null || partySatisfactions.Length == 0) return 0f;
            float subset = Mathf.Clamp01(coalitionSubset);
            if (subset <= 0f) return 0f;
            int n = partySatisfactions.Length;
            // 参加させる人数（最低1名・切り上げで端数も1名扱い）。
            int k = Mathf.Clamp(Mathf.CeilToInt(subset * n), 1, n);
            // 上位 k 件＝満足度の高い側を採る。k 番目に高い値（＝採った部分集合の最小）を手書きで求める。
            // 各候補 c について「c 以上の満足度を持つ当事者数」を数え、k 件以上確保できる最大の c を探す。
            float best = 0f;
            for (int i = 0; i < n; i++)
            {
                float c = Mathf.Clamp01(partySatisfactions[i]);
                int countAtLeast = 0;
                for (int j = 0; j < n; j++)
                {
                    if (Mathf.Clamp01(partySatisfactions[j]) >= c) countAtLeast++;
                }
                // c 以上が k 件以上あるなら、上位 k 件をこの水準まで満たせる。最大の c を採る。
                if (countAtLeast >= k && c > best) best = c;
            }
            return Clamp01(best);
        }

        /// <summary>
        /// 仲介者の価値（0..1）＝中立の仲介者が複雑な多国間交渉をまとめる効き目。交渉の複雑さ(complexity 0..1)が
        /// 高いほど、かつ仲介者への信頼(mediatorTrust 0..1)が高いほど価値が大きい（単純な交渉に仲介は要らない）。
        /// 複雑さ×信頼×効力。仲介は複雑さに比例して値打ちを持つ＝多極講和では仲介が決定的に重要。
        /// </summary>
        public static float MediatorValue(float complexity, float mediatorTrust, MultipartyPeaceParams p)
        {
            float c = Mathf.Clamp01(complexity);
            float trust = Mathf.Clamp01(mediatorTrust);
            return Clamp01(c * trust * p.mediatorEffectiveness);
        }

        /// <summary>既定 Params で解決。</summary>
        public static float MediatorValue(float complexity, float mediatorTrust)
            => MediatorValue(complexity, mediatorTrust, MultipartyPeaceParams.Default);

        /// <summary>
        /// 包括的和平の判定＝包括合意の成立見込み(packageDealFeasibility 0..1)が閾値(threshold)以上なら true
        /// （全当事者が合意した包括パッケージが成立＝ウェストファリア型の包括和平）。膠着検知の裏返し。
        /// </summary>
        public static bool IsComprehensivePeace(float packageDealFeasibility, float threshold)
        {
            return Mathf.Clamp01(packageDealFeasibility) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定の包括成立閾値で包括的和平を判定。</summary>
        public static bool IsComprehensivePeace(float packageDealFeasibility)
            => IsComprehensivePeace(packageDealFeasibility, MultipartyPeaceParams.Default.comprehensiveThreshold);
    }
}
