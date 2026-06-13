using UnityEngine;

namespace Ginei
{
    /// <summary>経済の動機＝家政術（必要を満たす自然な経済）か蓄財術（貨幣のための貨幣＝無制限の収奪）か（#1502・アリストテレス）。</summary>
    public enum EconomicMotive
    {
        /// <summary>家政術（oikonomia）＝必要を満たす自然な経済（足れば止まる管理型）。</summary>
        家政術,
        /// <summary>蓄財術（chrematistike）＝貨幣のための貨幣・無制限の富の追求（不自然な収奪型）。</summary>
        蓄財術,
    }

    /// <summary>
    /// 収奪経済志向＝アリストテレス『政治学』の家政術 vs 蓄財術の純ロジック（ARIS-3 #1502・唯一の窓口・test-first）。
    /// 経済が「必要を満たす管理型（家政術＝足れば止まる）」か「無制限に奪う収奪型（蓄財術＝貨幣のための貨幣・止まらない）」か
    /// で社会の健全性が分かれる。取得モードを軸で測り（<see cref="AcquisitionMode"/>）、家政術には自然な充足の限界があり
    /// （<see cref="NaturalLimit"/>）、蓄財術は際限なく富を追い（<see cref="UnboundedAccumulation"/>）、無制限の蓄財が
    /// 共同体の紐帯を腐食し（<see cref="SocialCorrosion"/>）、高利貸し＝貨幣が貨幣を生む不自然さ（<see cref="UsuryUnnaturalness"/>＝
    /// アリストテレスが最も腐敗的と見たもの）が突出し、収奪型の貪欲が**別経路で**腐敗を加速し（<see cref="CorruptionViaGreed"/>）、
    /// 生産的経済と収奪的経済の比が社会の痩せを決め（<see cref="ProductiveVsExtractive"/>）、蓄財術が支配的になったか判定する（<see cref="IsChrematisticDominant"/>）。
    /// 分担：`MonopolyRules`＝独占の価格吊り上げ（市場構造の失敗）／`MeritRankRules.ExtractiveDecay`＝法家の収奪（短期最強・長期崩壊の制度疲労）／
    /// `CapitalRules`＝資本集中（r>g のストック偏在）／`CommonGoodOrientationRules`＝公益志向（同EPIC ARIS の正側）／
    /// **本クラス＝アリストテレスの蓄財術批判**（管理型 vs 収奪型の**動機**区別→腐敗の別回路。価格・株価・ストックそのものは扱わない）。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。調整値は <see cref="ChrematisticsParams"/>（既定 <see cref="ChrematisticsParams.Default"/>）。
    /// </summary>
    public static class ChrematisticsRules
    {
        /// <summary>取得モードのしきい値（−1家政術〜+1蓄財術）。蓄財術と判定する境界（既定 0）。</summary>
        public const float DefaultDominanceThreshold = 0f;

        /// <summary>家政術＝取得モードが負側。</summary>
        public static EconomicMotive MotiveOf(float acquisitionMode)
            => acquisitionMode >= DefaultDominanceThreshold ? EconomicMotive.蓄財術 : EconomicMotive.家政術;

        /// <summary>
        /// 取得モードの軸（−1家政術〜+1蓄財術）。必要充足の重み（needSatisfaction）が強いほど家政術（−）へ、
        /// 無制限の利得追求（unlimitedGain）が強いほど蓄財術（+）へ。差分そのものが軸＝必要を満たす経済と
        /// 無制限に奪う経済のどちらに傾くか（基準非破壊・呼び出し側はこの軸を各害の入力に使う）。
        /// </summary>
        public static float AcquisitionMode(float needSatisfaction, float unlimitedGain)
        {
            float need = Mathf.Clamp01(needSatisfaction);
            float gain = Mathf.Clamp01(unlimitedGain);
            return Mathf.Clamp(gain - need, -1f, 1f);
        }

        /// <summary>自然な充足の限界（既定 Params）。</summary>
        public static float NaturalLimit(float needSatisfaction) => NaturalLimit(needSatisfaction, ChrematisticsParams.Default);

        /// <summary>
        /// 家政術の自然な充足の限界（0..1＝どれだけ「止まる」か）。必要が満たされるほど追求は止まる
        /// （足れば止まる管理型）。needSatisfaction が `limitOnset` を超えた分だけ非線形に飽和へ向かう＝
        /// 自然な経済には終点がある（蓄財術にはこれが無い）。
        /// </summary>
        public static float NaturalLimit(float needSatisfaction, ChrematisticsParams p)
        {
            float need = Mathf.Clamp01(needSatisfaction);
            if (need <= p.limitOnset) return 0f;
            float t = (need - p.limitOnset) / (1f - p.limitOnset); // 充足開始域超過の正規化(0..1)
            return Mathf.Clamp01(Mathf.Pow(t, p.satiationExponent));
        }

        /// <summary>際限なき蓄積（既定 Params）。</summary>
        public static float UnboundedAccumulation(float acquisitionMode) => UnboundedAccumulation(acquisitionMode, ChrematisticsParams.Default);

        /// <summary>
        /// 蓄財術の際限なき蓄積（0..1）＝貨幣のための貨幣で止まらない度合い。取得モードの蓄財側（正）だけが効く
        /// （家政術側＝負は0＝足れば止まる）。蓄財術には自然な限界が無いので、正規化した蓄財傾向×感度で増える
        /// （完全な蓄財志向で最大）。呼び出し側が富の追求が止まらない圧として消費する想定。
        /// </summary>
        public static float UnboundedAccumulation(float acquisitionMode, ChrematisticsParams p)
        {
            float mode = Mathf.Clamp(acquisitionMode, -1f, 1f);
            if (mode <= 0f) return 0f; // 家政術側＝充足の限界で止まる
            return Mathf.Clamp01(mode * p.accumulationScale);
        }

        /// <summary>共同体の腐食（既定 Params）。</summary>
        public static float SocialCorrosion(float unlimitedGain, float communityBond)
            => SocialCorrosion(unlimitedGain, communityBond, ChrematisticsParams.Default);

        /// <summary>
        /// 無制限の蓄財が共同体の紐帯を腐食する量（0..1）＝守銭奴が社会を蝕む。無制限の利得追求（unlimitedGain）が
        /// 紐帯を削るが、共同体の絆（communityBond）が強いほど抵抗して削りを和らげる（(1−bond) を乗算）。
        /// 呼び出し側が結束#113・合意・希望からこの分を差し引く想定（基準非破壊）。
        /// </summary>
        public static float SocialCorrosion(float unlimitedGain, float communityBond, ChrematisticsParams p)
        {
            float gain = Mathf.Clamp01(unlimitedGain);
            float bond = Mathf.Clamp01(communityBond);
            return Mathf.Clamp01(gain * (1f - bond) * p.corrosionScale);
        }

        /// <summary>高利貸しの不自然さ（既定 Params）。</summary>
        public static float UsuryUnnaturalness(float interestExtraction) => UsuryUnnaturalness(interestExtraction, ChrematisticsParams.Default);

        /// <summary>
        /// 高利貸しの不自然さ（0..maxUsury）＝貨幣が貨幣を生む不自然さ＝アリストテレスが最も腐敗的と見たもの。
        /// 利息収奪（interestExtraction）が `usuryExponent` 乗で非線形に跳ね、`usuryWeight` で重み付け＝
        /// 蓄財術の中でも突出して不自然（他の蓄財より重く効く）。呼び出し側が腐敗・正統性へ最も重い害として渡す想定。
        /// </summary>
        public static float UsuryUnnaturalness(float interestExtraction, ChrematisticsParams p)
        {
            float ext = Mathf.Clamp01(interestExtraction);
            return Mathf.Clamp(Mathf.Pow(ext, p.usuryExponent) * p.usuryWeight, 0f, p.maxUsury);
        }

        /// <summary>貪欲による腐敗加速（既定 Params）。</summary>
        public static float CorruptionViaGreed(float acquisitionMode, float dt)
            => CorruptionViaGreed(acquisitionMode, dt, ChrematisticsParams.Default);

        /// <summary>
        /// 収奪型の貪欲が**別経路で**腐敗を加速する1tickの増分（非負）。取得モードの蓄財側（正）だけが腐敗を進め
        /// （家政術＝必要充足型は腐敗を加速しない＝0）、正規化した蓄財傾向×速度×dt。`MeritRankRules.ExtractiveDecay`
        /// （制度疲労の収奪）とは別経路＝動機としての貪欲が腐敗を進める分。呼び出し側が `Regime.corruption` 等へ加算する想定。
        /// </summary>
        public static float CorruptionViaGreed(float acquisitionMode, float dt, ChrematisticsParams p)
        {
            float mode = Mathf.Clamp(acquisitionMode, -1f, 1f);
            if (mode <= 0f) return 0f; // 家政術は腐敗を加速しない
            return Mathf.Max(0f, mode * p.greedCorruptionRate * Mathf.Max(0f, dt));
        }

        /// <summary>生産的 vs 収奪的の比（既定 Params）。</summary>
        public static float ProductiveVsExtractive(float productiveShare, float extractiveShare)
            => ProductiveVsExtractive(productiveShare, extractiveShare, ChrematisticsParams.Default);

        /// <summary>
        /// 生産的経済と収奪的経済の比（−1収奪優位〜+1生産優位）。生産（productiveShare）が勝てば社会が富み（+）、
        /// 収奪（extractiveShare）が勝れば社会が痩せる（−）。両者の合計で正規化した差＝どちらが社会を養うか
        /// （両0は中立0）。呼び出し側が産出・安定の係数の符号として読む想定（収奪過多で痩せる）。
        /// </summary>
        public static float ProductiveVsExtractive(float productiveShare, float extractiveShare, ChrematisticsParams p)
        {
            float prod = Mathf.Clamp01(productiveShare);
            float extr = Mathf.Clamp01(extractiveShare);
            float total = prod + extr;
            if (total <= 0f) return 0f; // 経済活動なし＝中立
            return Mathf.Clamp((prod - extr) / total * p.balanceScale, -1f, 1f);
        }

        /// <summary>蓄財術が支配的か（既定しきい値 <see cref="DefaultDominanceThreshold"/>）。</summary>
        public static bool IsChrematisticDominant(float acquisitionMode)
            => IsChrematisticDominant(acquisitionMode, DefaultDominanceThreshold);

        /// <summary>
        /// 蓄財術（収奪志向）が支配的になったか＝取得モードがしきい値以上（既定0＝蓄財側に傾く）。
        /// 支配的になると上記の害（腐食・高利貸し・貪欲腐敗）が効き、社会が不健全へ転ぶ＝経済の動機が分水嶺。
        /// </summary>
        public static bool IsChrematisticDominant(float acquisitionMode, float threshold)
            => Mathf.Clamp(acquisitionMode, -1f, 1f) >= threshold;

        /// <summary>蓄財術批判の調整値（充足の限界・蓄積/腐食/貪欲腐敗の感度・高利貸しの突出度）。ctor で全てクランプ。</summary>
        public readonly struct ChrematisticsParams
        {
            /// <summary>自然な充足の限界が効き始める必要充足（これ以下は限界0＝まだ足りない。0..0.95）。</summary>
            public readonly float limitOnset;
            /// <summary>充足の飽和指数（≥1。大きいほど満たされてから一気に止まる）。</summary>
            public readonly float satiationExponent;
            /// <summary>際限なき蓄積の感度（蓄財傾向に乗算）。</summary>
            public readonly float accumulationScale;
            /// <summary>共同体腐食の感度（無制限の利得×(1−絆) に乗算）。</summary>
            public readonly float corrosionScale;
            /// <summary>高利貸しの非線形指数（≥1。利息収奪が高いほど跳ねる）。</summary>
            public readonly float usuryExponent;
            /// <summary>高利貸しの重み（蓄財の中でも突出＝最も不自然）。</summary>
            public readonly float usuryWeight;
            /// <summary>高利貸しの不自然さ上限（突出を許す＝1超）。</summary>
            public readonly float maxUsury;
            /// <summary>貪欲による腐敗加速速度（/単位時間・別経路）。</summary>
            public readonly float greedCorruptionRate;
            /// <summary>生産的 vs 収奪的の比の感度（正規化差に乗算）。</summary>
            public readonly float balanceScale;

            public ChrematisticsParams(
                float limitOnset, float satiationExponent, float accumulationScale, float corrosionScale,
                float usuryExponent, float usuryWeight, float maxUsury, float greedCorruptionRate, float balanceScale)
            {
                this.limitOnset = Mathf.Clamp(limitOnset, 0f, 0.95f);        // 1だと充足域が消える
                this.satiationExponent = Mathf.Max(1f, satiationExponent);  // 線形未満にしない
                this.accumulationScale = Mathf.Max(0f, accumulationScale);
                this.corrosionScale = Mathf.Max(0f, corrosionScale);
                this.usuryExponent = Mathf.Max(1f, usuryExponent);          // 非線形の跳ねを保証
                this.usuryWeight = Mathf.Max(0f, usuryWeight);
                this.maxUsury = Mathf.Max(0f, maxUsury);
                this.greedCorruptionRate = Mathf.Max(0f, greedCorruptionRate);
                this.balanceScale = Mathf.Max(0f, balanceScale);
            }

            /// <summary>
            /// 既定＝充足の限界開始0.5・飽和指数2（満たされてから止まる）・蓄積感度1・腐食感度1・
            /// 高利貸し指数2・高利貸し重み1.5・上限1.5（突出）・貪欲腐敗速度0.1・比の感度1。
            /// </summary>
            public static ChrematisticsParams Default => new ChrematisticsParams(0.5f, 2f, 1f, 1f, 2f, 1.5f, 1.5f, 0.1f, 1f);
        }
    }
}
