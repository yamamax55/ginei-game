using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 買い占め・投機・バブル＝商品コーナリング動学の純ロジック（唯一の窓口・test-first・狼と香辛料・#1076）。
    /// ある商品を買い占めて市場を支配し（<see cref="CorneringRules.MarketControl"/>）、支配率が高いほど価格を
    /// 非線形に吊り上げられる（<see cref="CorneringRules.PriceManipulation"/>）が、支配を強めるほど自分の買いが
    /// 値を押し上げてコストが嵩む（<see cref="CorneringRules.CornerCost"/>）。実需の裏付けなく吊り上げた価格は
    /// 砂上の楼閣で脆く（<see cref="CorneringRules.BubbleFragility"/>）、引き金を引けば支えきれず暴落し
    /// （<see cref="CorneringRules.Burst"/>）、崩壊時に売り抜けられず買い占めた在庫もろとも大損する
    /// （<see cref="CorneringRules.UnwindLoss"/>＝ハント兄弟の銀＝コーナリングの失敗は破産）。
    /// 分担：`MarketRules`＝平時の需給均衡（価格は需給が決める）／`MonopolyRules`＝構造的独占（支配シェアが
    /// 恒常的に価格・政治・革新を歪める）／`MerchantCreditRules`＝商人のレバレッジ・信用（同Wave並行・買い占めの原資）／
    /// **本クラス＝投機的な買い占めとバブル崩壊**（一時的に市場を握って吊り上げ、実需なきバブルが自壊する動学）。
    /// 乱数は roll 引数で決定論・全入力クランプ・基準値非破壊（実効値パターン）。調整値は <see cref="CorneringParams"/>（既定 <see cref="CorneringParams.Default"/>）。
    /// </summary>
    public static class CorneringRules
    {
        /// <summary>
        /// 市場支配率＝買い占めた割合（holdings/totalSupply・0..1）。売り手在庫の何割を握ったか＝価格を操れる度合いの基盤。
        /// totalSupply≤0 は支配0（市場が存在しない）。
        /// </summary>
        public static float MarketControl(float holdings, float totalSupply)
        {
            float supply = Mathf.Max(0f, totalSupply);
            if (supply <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, holdings) / supply);
        }

        /// <summary>価格吊り上げ率（既定 Params）。</summary>
        public static float PriceManipulation(float marketControl) => PriceManipulation(marketControl, CorneringParams.Default);

        /// <summary>
        /// 市場支配率→価格吊り上げ率（0..maxManipulation）。買い占めた割合が高いほど価格を操れる（非線形）＝
        /// 売り手が居なくなり買い占め側が値を付けられる。支配率 t に対し maxManipulation×t^manipulationExponent
        /// （支配的になってから一気に跳ねる＝買い占めの利得）。実効価格は呼び出し側が「基準価格×(1+manipulation)」で求める（基準非破壊）。
        /// </summary>
        public static float PriceManipulation(float marketControl, CorneringParams p)
        {
            float c = Mathf.Clamp01(marketControl);
            return p.maxManipulation * Mathf.Pow(c, p.manipulationExponent);
        }

        /// <summary>買い占めコスト（既定 Params）。</summary>
        public static float CornerCost(float targetControl, float currentPrice, float totalSupply)
            => CornerCost(targetControl, currentPrice, totalSupply, CorneringParams.Default);

        /// <summary>
        /// 目標支配率まで買い占めるコスト＝自分の買いが値を押し上げるため支配を強めるほど割高（凸関数）。
        /// 必要数量 = targetControl×totalSupply、これに「基準価格×(1+slippageScale×targetControl^slippageExponent)」を
        /// 掛ける＝買い進むほど平均取得単価が上がる（マーケットインパクト＝自分の買いが値を上げる）。
        /// 支配を強めるほど在庫拡大とスリッページの二重で高くつく＝コーナリングは元手を食う。
        /// </summary>
        public static float CornerCost(float targetControl, float currentPrice, float totalSupply, CorneringParams p)
        {
            float c = Mathf.Clamp01(targetControl);
            float price = Mathf.Max(0f, currentPrice);
            float supply = Mathf.Max(0f, totalSupply);
            float quantity = c * supply;
            float avgPriceFactor = 1f + p.slippageScale * Mathf.Pow(c, p.slippageExponent); // 買い占めが値を上げる
            return quantity * price * avgPriceFactor;
        }

        /// <summary>バブルの脆さ（既定 Params）。</summary>
        public static float BubbleFragility(float marketControl, float fundamentalValue)
            => BubbleFragility(marketControl, fundamentalValue, CorneringParams.Default);

        /// <summary>
        /// バブルの脆さ（0..1）＝高く吊り上げたほど・実需の裏付け（fundamentalValue 0..1）が薄いほど脆い。
        /// 吊り上げ率（<see cref="PriceManipulation"/>）が大きく実需が無いほど砂上の楼閣＝
        /// fragility = manipulation×(1−fundamental)×fragilityScale。実需が満点(1)なら脆さ0＝裏付けある価格は崩れない。
        /// </summary>
        public static float BubbleFragility(float marketControl, float fundamentalValue, CorneringParams p)
        {
            float fundamental = Mathf.Clamp01(fundamentalValue);
            float manipulation = PriceManipulation(marketControl, p);
            return Mathf.Clamp01(manipulation * (1f - fundamental) * p.fragilityScale);
        }

        /// <summary>バブル崩壊判定（既定 Params）。</summary>
        public static bool Burst(float bubbleFragility, bool triggerEvent, float roll)
            => Burst(bubbleFragility, triggerEvent, roll, CorneringParams.Default);

        /// <summary>
        /// バブルが崩壊するか＝脆さに応じて roll(0..1) で決定論判定。脆いほど崩れやすく、引き金（triggerEvent＝
        /// 規制・大口の売り・実需の露呈など）があれば崩壊確率が triggerBoost ぶん跳ね上がる（支えきれず暴落）。
        /// roll &lt; 崩壊確率 で true。実需なく吊り上げたバブルは些細な引き金で自壊する。
        /// </summary>
        public static bool Burst(float bubbleFragility, bool triggerEvent, float roll, CorneringParams p)
        {
            float fragility = Mathf.Clamp01(bubbleFragility);
            float chance = fragility * (triggerEvent ? p.triggerBoost : 1f);
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>手仕舞いの損失（既定 Params）。</summary>
        public static float UnwindLoss(float holdings, float peakPrice, float crashPrice)
            => UnwindLoss(holdings, peakPrice, crashPrice, CorneringParams.Default);

        /// <summary>
        /// 崩壊時の手仕舞い損失＝吊り上げた天井(peakPrice)で買い占めた在庫(holdings)が暴落価格(crashPrice)まで
        /// 値を失う＝(peak−crash)×holdings。さらに一気に投げ売れば自分の売りが値を崩すため liquidationPenalty で
        /// 損失が拡大する（売り抜けられない＝コーナリングの失敗は破産＝ハント兄弟の銀）。crash≥peak なら損失0。
        /// </summary>
        public static float UnwindLoss(float holdings, float peakPrice, float crashPrice, CorneringParams p)
        {
            float qty = Mathf.Max(0f, holdings);
            float peak = Mathf.Max(0f, peakPrice);
            float crash = Mathf.Max(0f, crashPrice);
            float drop = Mathf.Max(0f, peak - crash); // 値上がり中なら損失0
            return qty * drop * (1f + p.liquidationPenalty); // 投げ売りが値をさらに崩す
        }
    }

    /// <summary>
    /// コーナリングの調整値（吊り上げの非線形度・買い占めのスリッページ・バブルの脆さと崩壊・投げ売り罰）。ctor で全てクランプ（#1076）。
    /// </summary>
    public readonly struct CorneringParams
    {
        /// <summary>支配率1（完全買い占め）での最大吊り上げ率（例 2.0＝価格+200%）。</summary>
        public readonly float maxManipulation;
        /// <summary>吊り上げの非線形指数（≥1。大きいほど買い占めが進んでから一気に跳ねる）。</summary>
        public readonly float manipulationExponent;
        /// <summary>買い占めのスリッページ感度（支配率に応じ取得単価を押し上げる＝マーケットインパクト）。</summary>
        public readonly float slippageScale;
        /// <summary>スリッページの非線形指数（≥1。買い進むほど割高に）。</summary>
        public readonly float slippageExponent;
        /// <summary>バブルの脆さ感度（吊り上げ×実需薄に乗算）。</summary>
        public readonly float fragilityScale;
        /// <summary>引き金時の崩壊確率ブースト（≥1。規制・大口売り等で暴落しやすくなる）。</summary>
        public readonly float triggerBoost;
        /// <summary>投げ売りの追加損失率（崩壊時に売り抜けられず自分の売りが値を崩す）。</summary>
        public readonly float liquidationPenalty;

        public CorneringParams(
            float maxManipulation, float manipulationExponent, float slippageScale,
            float slippageExponent, float fragilityScale, float triggerBoost, float liquidationPenalty)
        {
            this.maxManipulation = Mathf.Max(0f, maxManipulation);
            this.manipulationExponent = Mathf.Max(1f, manipulationExponent);   // 線形未満にしない＝非線形の跳ねを保証
            this.slippageScale = Mathf.Max(0f, slippageScale);
            this.slippageExponent = Mathf.Max(1f, slippageExponent);
            this.fragilityScale = Mathf.Max(0f, fragilityScale);
            this.triggerBoost = Mathf.Max(1f, triggerBoost);                   // 引き金は崩壊を弱めない
            this.liquidationPenalty = Mathf.Max(0f, liquidationPenalty);
        }

        /// <summary>
        /// 既定＝最大吊り上げ+200%・吊り上げ指数2（買い占めが進んでから跳ねる）・スリッページ感度1・スリッページ指数2・
        /// 脆さ感度1・引き金ブースト2・投げ売り罰0.3。
        /// </summary>
        public static CorneringParams Default => new CorneringParams(2f, 2f, 1f, 2f, 1f, 2f, 0.3f);
    }
}
