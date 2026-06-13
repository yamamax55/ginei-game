using UnityEngine;

namespace Ginei
{
    /// <summary>緩衝国（フェザーン型）生存戦略の調整係数。</summary>
    public readonly struct BufferStateParams
    {
        /// <summary>平時の基礎併呑リスク（均衡・不可欠・等距離が完璧でも残る地政学的な底）。</summary>
        public readonly float baseRisk;
        /// <summary>生存余地の喪失（均衡崩壊・用済み）がリスクへ効く重み。</summary>
        public readonly float collapseWeight;
        /// <summary>等距離からの偏り（肩入れ）がリスクへ効く重み。</summary>
        public readonly float tiltWeight;

        public BufferStateParams(float baseRisk, float collapseWeight, float tiltWeight)
        {
            this.baseRisk = Mathf.Clamp01(baseRisk);
            this.collapseWeight = Mathf.Clamp01(collapseWeight);
            this.tiltWeight = Mathf.Clamp01(tiltWeight);
        }

        /// <summary>既定＝基礎リスク5%・均衡崩壊の重み0.6・偏りの重み0.35（全悪化で合計1.0＝確実に併呑）。</summary>
        public static BufferStateParams Default => new BufferStateParams(0.05f, 0.6f, 0.35f);
    }

    /// <summary>
    /// 緩衝国（フェザーン型）の純ロジック＝三体問題の生存術。両大国の狭間の小国は
    /// 「強くなりすぎず・偏らず・不可欠であれ」＝弱さの外交で自立を保つ：両大国の均衡
    /// （どちらも相手を気にして手を出せない）×経済的不可欠性（潰すと双方が損をする）が生存余地。
    /// 均衡が崩れた瞬間・片方へ肩入れした瞬間・用済みになった瞬間に併呑リスクが跳ねる。
    /// <see cref="DiplomacyRules"/>（二国間の状態遷移・opinion）とは別系統＝三者の構造を扱う。
    /// <see cref="TradeRules.BrokerProfit"/>（仲介の口銭＝金額）とも分担し、ここでは仲介者の
    /// 政治的影響力（<see cref="BrokerLeverage"/>）を扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BufferStateRules
    {
        /// <summary>
        /// 両大国の均衡度（0..1、対等で1）＝弱い方÷強い方。一方が圧倒すれば0へ＝緩衝国の存在理由が消える。
        /// 両方0以下（大国不在）は便宜上1＝均衡扱い。負の国力は0にクランプ。
        /// </summary>
        public static float PowerBalance(float powerA, float powerB)
        {
            float a = Mathf.Max(0f, powerA);
            float b = Mathf.Max(0f, powerB);
            float max = Mathf.Max(a, b);
            if (max <= 0f) return 1f; // 大国不在＝脅威の非対称なし
            return Mathf.Clamp01(Mathf.Min(a, b) / max);
        }

        /// <summary>
        /// 小国の生存余地（0..1）＝均衡×経済的不可欠性。「どちらにも潰せない理由」の積＝
        /// 均衡が崩れても・用済みになっても、どちらか一方が欠けるだけで余地は消える。
        /// </summary>
        public static float SurvivalSpace(float balance, float indispensability)
        {
            return Mathf.Clamp01(balance) * Mathf.Clamp01(indispensability);
        }

        /// <summary>
        /// 肩入れの危険（0..1）＝等距離（alignment=0）からのズレの絶対値。
        /// alignment は -1（A完全従属）..+1（B完全従属）。どちらへ寄っても反対側の敵意を買う＝対称。
        /// </summary>
        public static float TiltPenalty(float alignment)
        {
            return Mathf.Abs(Mathf.Clamp(alignment, -1f, 1f));
        }

        /// <summary>
        /// 併呑リスク（0..1）＝基礎リスク＋生存余地の喪失×collapseWeight＋偏り×tiltWeight。
        /// 均衡崩壊（balance→0）・用済み（indispensability→0）・偏りすぎ（|alignment|→1）の
        /// いずれでも跳ね、全部揃えば既定値で1.0＝確実に呑まれる。
        /// </summary>
        public static float AnnexationRisk(float balance, float indispensability, float alignment, BufferStateParams p)
        {
            float survival = SurvivalSpace(balance, indispensability);
            float tilt = TiltPenalty(alignment);
            return Mathf.Clamp01(p.baseRisk + p.collapseWeight * (1f - survival) + p.tiltWeight * tilt);
        }

        public static float AnnexationRisk(float balance, float indispensability, float alignment)
            => AnnexationRisk(balance, indispensability, alignment, BufferStateParams.Default);

        /// <summary>
        /// 仲介者としての政治的影響力（0..1）＝均衡×双方の交易依存度。両大国が拮抗し、かつ双方が
        /// 仲介経由の交易に依存するほど、小国の言葉が両首都に届く。均衡が崩れれば依存も影響力も意味を失う。
        /// 口銭（金額）は <see cref="TradeRules.BrokerProfit"/> の担当＝こちらは政治。
        /// </summary>
        public static float BrokerLeverage(float balance, float tradeDependence)
        {
            return Mathf.Clamp01(balance) * Mathf.Clamp01(tradeDependence);
        }
    }
}
