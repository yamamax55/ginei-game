using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 独占・財閥＝市場の失敗の純ロジック（唯一の窓口・test-first）。
    /// 市場支配シェアが競争域を超えると価格を非線形に吊り上げ（<see cref="MonopolyRules.PriceMarkup"/>）、
    /// 生活水準を害し（<see cref="MonopolyRules.ConsumerHarm"/>）、富で政治を買収して規制を骨抜きにし
    /// （<see cref="MonopolyRules.PoliticalCapture"/>）、競争圧なき停滞を生む（<see cref="MonopolyRules.InnovationStagnation"/>）。
    /// 解体は可能だが大きく育ててからでは反発が高くつき（<see cref="MonopolyRules.BreakupBacklash"/>）、
    /// 放置すればシェアは規模の自己強化で寡占へ漂う（<see cref="MonopolyRules.ShareTick"/>）＝**独占は放置の帰結・早い介入は安い**。
    /// 分担：`MarketRules`＝競争市場の需給均衡（価格は需給が決める）／`StockMarketRules`＝企業の株価・配当／
    /// **本クラス＝市場の失敗**（支配シェアが価格・政治・革新を歪める層。需給そのものは扱わない）。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。調整値は <see cref="MonopolyParams"/>（既定 <see cref="MonopolyParams.Default"/>）。
    /// </summary>
    public static class MonopolyRules
    {
        /// <summary>独占の調整値（競争域の境界・吊り上げの非線形度・各害の感度）。ctor で全てクランプ。</summary>
        public readonly struct MonopolyParams
        {
            /// <summary>競争的とみなすシェア上限（これ以下は吊り上げ0＝健全な市場。0..0.95）。</summary>
            public readonly float competitiveShare;
            /// <summary>吊り上げの非線形指数（≥1。大きいほど支配的になってから一気に跳ねる）。</summary>
            public readonly float markupExponent;
            /// <summary>シェア1（完全独占）での最大吊り上げ率（例 1.0＝価格+100%）。</summary>
            public readonly float maxMarkup;
            /// <summary>吊り上げ率→生活水準への害の感度。</summary>
            public readonly float harmScale;
            /// <summary>政治買収力の感度（シェア×富に乗算）。</summary>
            public readonly float captureScale;
            /// <summary>競争圧なき停滞の感度（競争域超過の正規化値に乗算）。</summary>
            public readonly float stagnationScale;
            /// <summary>解体反発の感度（シェアの2乗に乗算＝育つほど高くつく）。</summary>
            public readonly float backlashScale;
            /// <summary>シェアドリフト速度（/単位時間。規模の自己強化で成長側はさらに加速）。</summary>
            public readonly float driftRate;

            public MonopolyParams(
                float competitiveShare, float markupExponent, float maxMarkup, float harmScale,
                float captureScale, float stagnationScale, float backlashScale, float driftRate)
            {
                this.competitiveShare = Mathf.Clamp(competitiveShare, 0f, 0.95f); // 1だと超過域が消える
                this.markupExponent = Mathf.Max(1f, markupExponent);              // 線形未満にしない＝非線形の跳ねを保証
                this.maxMarkup = Mathf.Max(0f, maxMarkup);
                this.harmScale = Mathf.Max(0f, harmScale);
                this.captureScale = Mathf.Max(0f, captureScale);
                this.stagnationScale = Mathf.Max(0f, stagnationScale);
                this.backlashScale = Mathf.Max(0f, backlashScale);
                this.driftRate = Mathf.Max(0f, driftRate);
            }

            /// <summary>
            /// 既定＝競争域0.4・指数2（支配的になってから跳ねる）・最大吊り上げ+100%・害感度0.8・
            /// 買収感度1・停滞感度1・反発感度1・ドリフト速度0.1。
            /// </summary>
            public static MonopolyParams Default => new MonopolyParams(0.4f, 2f, 1f, 0.8f, 1f, 1f, 1f, 0.1f);
        }

        /// <summary>価格吊り上げ率（既定 Params）。</summary>
        public static float PriceMarkup(float marketShare) => PriceMarkup(marketShare, MonopolyParams.Default);

        /// <summary>
        /// 支配シェア→価格吊り上げ率（0..maxMarkup）。競争域（share≤competitiveShare）では0＝競争が価格を抑える。
        /// 超過分を正規化した t に対し maxMarkup×t^markupExponent ＝支配的になるほど非線形に跳ねる（市場の失敗）。
        /// 吊り上げ後の実効価格は呼び出し側が「基準価格×(1+markup)」で求める（基準非破壊）。
        /// </summary>
        public static float PriceMarkup(float marketShare, MonopolyParams p)
        {
            float share = Mathf.Clamp01(marketShare);
            if (share <= p.competitiveShare) return 0f;
            float t = (share - p.competitiveShare) / (1f - p.competitiveShare); // 競争域超過の正規化(0..1)
            return p.maxMarkup * Mathf.Pow(t, p.markupExponent);
        }

        /// <summary>生活水準への害（既定 Params）。</summary>
        public static float ConsumerHarm(float markup) => ConsumerHarm(markup, MonopolyParams.Default);

        /// <summary>
        /// 吊り上げ率→生活水準への害（0..1）。独占価格は消費を圧迫する＝呼び出し側が
        /// `MarketRules.StandardOfLiving` 等の生活水準からこの分を差し引く想定（基準非破壊）。
        /// </summary>
        public static float ConsumerHarm(float markup, MonopolyParams p)
            => Mathf.Clamp01(Mathf.Max(0f, markup) * p.harmScale);

        /// <summary>政治買収力（既定 Params）。</summary>
        public static float PoliticalCapture(float marketShare, float wealth)
            => PoliticalCapture(marketShare, wealth, MonopolyParams.Default);

        /// <summary>
        /// 政治買収力（0..1）＝支配シェア×富×感度。市場支配と富が揃って初めて政治が買える（片方0なら0）。
        /// 呼び出し側は規制の実効性に (1−capture) を掛ける想定＝規制が骨抜きになる（内部勢力#113・腐敗#867 の火種）。
        /// </summary>
        public static float PoliticalCapture(float marketShare, float wealth, MonopolyParams p)
            => Mathf.Clamp01(Mathf.Clamp01(marketShare) * Mathf.Clamp01(wealth) * p.captureScale);

        /// <summary>競争圧なき停滞（既定 Params）。</summary>
        public static float InnovationStagnation(float marketShare)
            => InnovationStagnation(marketShare, MonopolyParams.Default);

        /// <summary>
        /// 競争圧なき停滞（0..1）＝研究・生産性の低下割合。競争域では0、超過分の正規化値×感度で増える
        /// （完全独占で最大）。呼び出し側が研究#123/生産#93 の係数へ (1−stagnation) を掛ける想定（基準非破壊）。
        /// </summary>
        public static float InnovationStagnation(float marketShare, MonopolyParams p)
        {
            float share = Mathf.Clamp01(marketShare);
            if (share <= p.competitiveShare) return 0f;
            float t = (share - p.competitiveShare) / (1f - p.competitiveShare);
            return Mathf.Clamp01(t * p.stagnationScale);
        }

        /// <summary>解体の反発（既定 Params）。</summary>
        public static float BreakupBacklash(float marketShare)
            => BreakupBacklash(marketShare, MonopolyParams.Default);

        /// <summary>
        /// 解体の政治反発（0..1）＝シェアの2乗×感度。小さいうちの介入は安く、大きく育ててからでは
        /// 雇用・株主・買収済み政治家が抵抗して高くつく＝**早い介入は安い**を式に出す。
        /// 解体自体の効率益（停滞解消）と引き換えのコストとして支持#113/安定#109 から差し引く想定。
        /// </summary>
        public static float BreakupBacklash(float marketShare, MonopolyParams p)
        {
            float share = Mathf.Clamp01(marketShare);
            return Mathf.Clamp01(share * share * p.backlashScale);
        }

        /// <summary>シェアの1tick更新（既定 Params）。</summary>
        public static float ShareTick(float share, float competitiveness, float dt)
            => ShareTick(share, competitiveness, dt, MonopolyParams.Default);

        /// <summary>
        /// シェアの1tick更新＝放置すると寡占へ漂う（規模の自己強化）。市場の競争性（competitiveness 0..1＝
        /// 規制・新規参入の強さ）が低いほど目標シェアは1（独占）へ、高いほど競争域（competitiveShare）へ向かう。
        /// 成長側はシェアが大きいほど速い（0.5+share 倍）＝大きいものはより大きくなる。新しいシェアを返す（引数非破壊）。
        /// </summary>
        public static float ShareTick(float share, float competitiveness, float dt, MonopolyParams p)
        {
            float s = Mathf.Clamp01(share);
            float c = Mathf.Clamp01(competitiveness);
            float target = Mathf.Lerp(1f, p.competitiveShare, c); // 競争性0＝独占へ・1＝競争域へ
            float rate = p.driftRate * (target > s ? 0.5f + s : 1f); // 規模の自己強化＝成長側のみ加速
            return Mathf.MoveTowards(s, target, rate * Mathf.Max(0f, dt));
        }
    }
}
