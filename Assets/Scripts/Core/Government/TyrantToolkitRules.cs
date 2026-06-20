using UnityEngine;

namespace Ginei
{
    /// <summary>僭主維持術のカタログ（アリストテレス『政治学』第5巻・ARIS-5 #1504）。</summary>
    public enum TyrantTactic
    {
        /// <summary>傑出した人物を排除する（出る杭を切る）。</summary>
        傑物排除,
        /// <summary>民を貧困化させ反抗の余力を奪う（重税）。</summary>
        貧困化,
        /// <summary>大型公共事業で民を疲弊させ気を逸らす（パンとサーカスの暴政版）。</summary>
        大型事業,
        /// <summary>密告を奨励し相互不信を煽る。</summary>
        密告奨励,
        /// <summary>民を分断して連帯を妨げる。</summary>
        分断統治,
    }

    /// <summary>僭主維持術の調整係数（#1504・アリストテレスの僭主術カタログ）。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct TyrantToolkitParams
    {
        /// <summary>各手法の短期維持効果（脅威を抑える即効性）の最大幅。</summary>
        public readonly float shortTermScale;
        /// <summary>各手法の長期疲弊（人材喪失・経済疲弊・社会不信）の最大幅。</summary>
        public readonly float longTermScale;
        /// <summary>傑物排除が人材プールを毀損する最大割合（出る杭を切る代償）。</summary>
        public readonly float talentLossScale;
        /// <summary>国の空洞化と判定する長期疲弊の閾値。</summary>
        public readonly float hollowThreshold;

        public TyrantToolkitParams(float shortTermScale, float longTermScale, float talentLossScale, float hollowThreshold)
        {
            this.shortTermScale = Mathf.Clamp01(shortTermScale);
            this.longTermScale = Mathf.Clamp01(longTermScale);
            this.talentLossScale = Mathf.Clamp01(talentLossScale);
            this.hollowThreshold = Mathf.Clamp01(hollowThreshold);
        }

        /// <summary>既定＝短期維持0.6・長期疲弊0.7・人材毀損0.5・空洞化閾値0.5。</summary>
        public static TyrantToolkitParams Default => new TyrantToolkitParams(0.6f, 0.7f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 僭主維持術の純ロジック（アリストテレス『政治学』第5巻・ARIS-5 #1504）。
    /// アリストテレスが列挙した「僭主が権力を保つ伝統的手法のカタログ」＝
    /// ①傑出した人物を排除する（出る杭を切る）、②民を貧困化させ反抗の余力を奪う（重税）、
    /// ③大型公共事業で民を疲弊させ気を逸らす、④密告を奨励し相互不信を煽る、⑤民を分断する。
    /// これらは短期的に権力を保つ（脅威を抑える即効性）が、長期的には人材・経済・信頼を失い
    /// 国を空洞化させる＝「短期維持×長期疲弊」のトレードオフを式に出す。
    /// 秘密警察の摘発・抑圧（<see cref="SecurityRules"/>）／粛清の損得（<see cref="PurgeRules"/>）／
    /// ガス抜き（<see cref="BreadAndCircusesRules"/>）／逆淘汰（<see cref="AuthoritarianSelectionRules"/>＝傑物排除の人事帰結）
    /// とは別系統＝アリストテレスの僭主維持術カタログそのもの（手法ごとの短期効果と長期コストの分解）。
    /// 全入力 0..1 に clamp・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TyrantToolkitRules
    {
        /// <summary>各手法の短期的な権力維持効果(0..shortTermScale)＝強度に比例（脅威を抑える即効性）。</summary>
        public static float ShortTermControl(TyrantTactic tactic, float intensity, TyrantToolkitParams p)
        {
            return Mathf.Clamp01(intensity) * ShortTermWeight(tactic) * p.shortTermScale;
        }

        public static float ShortTermControl(TyrantTactic tactic, float intensity)
            => ShortTermControl(tactic, intensity, TyrantToolkitParams.Default);

        /// <summary>各手法の長期的な国家疲弊(0..longTermScale)＝強度に比例（人材喪失・経済疲弊・社会不信）。</summary>
        public static float LongTermDecay(TyrantTactic tactic, float intensity, TyrantToolkitParams p)
        {
            return Mathf.Clamp01(intensity) * LongTermWeight(tactic) * p.longTermScale;
        }

        public static float LongTermDecay(TyrantTactic tactic, float intensity)
            => LongTermDecay(tactic, intensity, TyrantToolkitParams.Default);

        /// <summary>
        /// 傑出者排除＝出る杭を切る。脅威(eminentThreat 0..1)を消す短期効果(out suppressed)を返しつつ、
        /// 戻り値は人材プールの毀損率(0..talentLossScale)＝有能な者ほど切られる（逆淘汰=AuthoritarianSelectionRules へ連動）。
        /// </summary>
        public static float TallPoppyElimination(float eminentThreat, float intensity, TyrantToolkitParams p, out float threatSuppressed)
        {
            float i = Mathf.Clamp01(intensity);
            threatSuppressed = Mathf.Clamp01(eminentThreat) * i; // 出る杭は折れる＝脅威が消える
            return i * p.talentLossScale;                        // だが切るほど人材を失う
        }

        public static float TallPoppyElimination(float eminentThreat, float intensity, out float threatSuppressed)
            => TallPoppyElimination(eminentThreat, intensity, TyrantToolkitParams.Default, out threatSuppressed);

        /// <summary>
        /// 民を貧困化して反抗の余力を奪う(0..1)＝重税(taxation 0..1)が民の富(populationWealth 0..1)を削るほど
        /// 蜂起できない（貧者は明日の糧で手一杯）。富があるほど課税の抑圧効果は薄い。
        /// </summary>
        public static float ImpoverishmentControl(float populationWealth, float taxation, TyrantToolkitParams p)
        {
            float wealth = Mathf.Clamp01(populationWealth);
            float tax = Mathf.Clamp01(taxation);
            // 富を削り取った割合（tax×wealth）が反抗の余力を奪う＝短期維持の重みで写す
            return Mathf.Clamp01(tax * wealth) * ShortTermWeight(TyrantTactic.貧困化) * p.shortTermScale;
        }

        public static float ImpoverishmentControl(float populationWealth, float taxation)
            => ImpoverishmentControl(populationWealth, taxation, TyrantToolkitParams.Default);

        /// <summary>
        /// 大型公共事業で民を疲弊させ気を逸らす(0..1)＝壮麗さ(grandeur 0..1)が民の目を逸らし
        /// 財政浪費(fiscalDrain 0..1)が余力を奪う（パンとサーカスの暴政版＝BreadAndCircusesRules のガス抜きとは別）。
        /// </summary>
        public static float GrandProjectDistraction(float grandeur, float fiscalDrain, TyrantToolkitParams p)
        {
            float g = Mathf.Clamp01(grandeur);
            float drain = Mathf.Clamp01(fiscalDrain);
            // 気逸らし(壮麗さ)と疲弊(浪費)の相乗＝両方あって初めて従順を引き出す
            return Mathf.Clamp01((g + drain) * 0.5f) * ShortTermWeight(TyrantTactic.大型事業) * p.shortTermScale;
        }

        public static float GrandProjectDistraction(float grandeur, float fiscalDrain)
            => GrandProjectDistraction(grandeur, fiscalDrain, TyrantToolkitParams.Default);

        /// <summary>
        /// 密告網が相互不信を生み団結を防ぐ短期維持効果(0..1)＝密告報酬(surveillanceReward 0..1)に比例。
        /// 隣人が密告者かもしれぬ社会では民は結束できない。
        /// </summary>
        public static float InformantNetwork(float surveillanceReward, TyrantToolkitParams p)
        {
            return Mathf.Clamp01(surveillanceReward) * ShortTermWeight(TyrantTactic.密告奨励) * p.shortTermScale;
        }

        public static float InformantNetwork(float surveillanceReward)
            => InformantNetwork(surveillanceReward, TyrantToolkitParams.Default);

        /// <summary>
        /// 民を分断して連帯を妨げる短期維持効果(0..1)＝派閥分断(factionalSplit 0..1)に比例。
        /// 分かたれた民は僭主に対抗する一枚岩になれない（分割統治）。
        /// </summary>
        public static float DivideAndRule(float factionalSplit, TyrantToolkitParams p)
        {
            return Mathf.Clamp01(factionalSplit) * ShortTermWeight(TyrantTactic.分断統治) * p.shortTermScale;
        }

        public static float DivideAndRule(float factionalSplit)
            => DivideAndRule(factionalSplit, TyrantToolkitParams.Default);

        /// <summary>
        /// 純持続力＝短期維持−長期疲弊。僭主術は当初は権力を保つが、疲弊が積み上がるほど
        /// 自国を食い潰し純持続力が負へ転じる（人材と経済と信頼を失った国はやがて支えられない）。
        /// </summary>
        public static float NetTyrannyDurability(float shortTermControl, float longTermDecay)
        {
            return Mathf.Clamp01(shortTermControl) - Mathf.Clamp01(longTermDecay);
        }

        /// <summary>
        /// 僭主術が国を空洞化させたか＝累積した長期疲弊が閾値を超えたか
        /// （人材・経済・信頼を失い、もはや殻だけが残った状態）。
        /// </summary>
        public static bool IsHollowingState(float longTermDecay, TyrantToolkitParams p)
        {
            return Mathf.Clamp01(longTermDecay) >= p.hollowThreshold;
        }

        public static bool IsHollowingState(float longTermDecay)
            => IsHollowingState(longTermDecay, TyrantToolkitParams.Default);

        // --- 手法ごとの効きの重み（const に集約） ---

        /// <summary>手法ごとの短期維持効果の重み（即効性の強い手法ほど大きい）。</summary>
        private static float ShortTermWeight(TyrantTactic tactic)
        {
            switch (tactic)
            {
                case TyrantTactic.傑物排除: return TallPoppyShortWeight;
                case TyrantTactic.貧困化: return ImpoverishShortWeight;
                case TyrantTactic.大型事業: return GrandProjectShortWeight;
                case TyrantTactic.密告奨励: return InformantShortWeight;
                case TyrantTactic.分断統治: return DivideShortWeight;
                default: return 1f;
            }
        }

        /// <summary>手法ごとの長期疲弊の重み（国を蝕む深さ＝出る杭排除・貧困化が最も深く国を空洞化させる）。</summary>
        private static float LongTermWeight(TyrantTactic tactic)
        {
            switch (tactic)
            {
                case TyrantTactic.傑物排除: return TallPoppyLongWeight;
                case TyrantTactic.貧困化: return ImpoverishLongWeight;
                case TyrantTactic.大型事業: return GrandProjectLongWeight;
                case TyrantTactic.密告奨励: return InformantLongWeight;
                case TyrantTactic.分断統治: return DivideLongWeight;
                default: return 1f;
            }
        }

        // --- 調整値（const に集約） ---
        /// <summary>傑物排除の短期維持重み（脅威を即座に消す＝最も即効）。</summary>
        public const float TallPoppyShortWeight = 1.0f;
        /// <summary>傑物排除の長期疲弊重み（有能な人材を失う＝最も深い空洞化）。</summary>
        public const float TallPoppyLongWeight = 1.0f;
        /// <summary>貧困化の短期維持重み（反抗の余力を奪う）。</summary>
        public const float ImpoverishShortWeight = 0.9f;
        /// <summary>貧困化の長期疲弊重み（経済を疲弊させる）。</summary>
        public const float ImpoverishLongWeight = 0.9f;
        /// <summary>大型事業の短期維持重み（気を逸らす＝即効性はやや低い）。</summary>
        public const float GrandProjectShortWeight = 0.7f;
        /// <summary>大型事業の長期疲弊重み（財政を食い潰す）。</summary>
        public const float GrandProjectLongWeight = 0.8f;
        /// <summary>密告奨励の短期維持重み（団結を防ぐ）。</summary>
        public const float InformantShortWeight = 0.8f;
        /// <summary>密告奨励の長期疲弊重み（社会不信が国を蝕む）。</summary>
        public const float InformantLongWeight = 0.7f;
        /// <summary>分断統治の短期維持重み（連帯を妨げる）。</summary>
        public const float DivideShortWeight = 0.8f;
        /// <summary>分断統治の長期疲弊重み（社会の紐帯を裂く）。</summary>
        public const float DivideLongWeight = 0.6f;
    }
}
