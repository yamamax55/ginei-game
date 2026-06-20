using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// バブル価格解離＝群集の信念が価格を実需（ファンダメンタル価値）から乖離させる動学の純ロジック（唯一の窓口・test-first・MNIA-2 #1622・マッカイ『狂気とバブル』参考）。
    /// 熱狂（maniaIntensity）が価格をファンダメンタルの何倍にも吊り上げ（<see cref="BubblePriceRules.BubbleMultiplier"/>→<see cref="BubblePriceRules.MarketPrice"/>）、
    /// 解離度（<see cref="BubblePriceRules.Deviation"/>）が閾値を超えればバブルと判定する（<see cref="BubblePriceRules.IsBubble"/>）。
    /// 崩壊すると恐怖が適正価格すら下回らせて底割れし（<see cref="BubblePriceRules.OvershootMultiplier"/>→<see cref="BubblePriceRules.CrashPrice"/>）、
    /// やがて行き過ぎた価格は時間で実需価格へ回帰する（<see cref="BubblePriceRules.MeanReversionTick"/>）。
    /// ＝上げも下げもファンダメンタルを行き過ぎる「両側のオーバーシュート」。
    /// 分担：`CorneringRules`＝買い占めによる人為的・投機的な吊り上げ（市場支配で値を操る）／`MarketRules`＝平時の需給均衡価格（価格は需給が決める）／
    /// `ManiaRules`＝信念感染・熱狂の伝播（同EPIC・本クラスへ maniaIntensity を供給）／
    /// **本クラス＝群集の信念がファンダメンタルから価格を解離させる（熱狂で吊り上げ崩壊で底割れする両側オーバーシュート）**。
    /// 乱数は使わない（決定論）・全入力クランプ（価格/実需は Max(0)・強度は Clamp01）・基準値非破壊（実効値パターン）。調整値は <see cref="BubblePriceParams"/>（既定 <see cref="BubblePriceParams.Default"/>）。
    /// </summary>
    public static class BubblePriceRules
    {
        /// <summary>バブル倍率（既定 Params）。</summary>
        public static float BubbleMultiplier(float maniaIntensity)
            => BubbleMultiplier(maniaIntensity, BubblePriceParams.Default);

        /// <summary>
        /// 熱狂（maniaIntensity 0..1）→価格がファンダメンタルの何倍に膨らむか（1.0..maxBubbleMultiplier）。
        /// 熱狂0で1.0倍（実需どおり）、熱狂が高まるほど非線形に跳ねて最大 maxBubbleMultiplier 倍まで吊り上がる
        /// ＝1 + (maxBubbleMultiplier−1)×t^maniaExponent。群集の信念が価格を実需より上へオーバーシュートさせる（上側の行き過ぎ）。
        /// </summary>
        public static float BubbleMultiplier(float maniaIntensity, BubblePriceParams p)
        {
            float t = Mathf.Clamp01(maniaIntensity);
            return 1f + (p.maxBubbleMultiplier - 1f) * Mathf.Pow(t, p.maniaExponent);
        }

        /// <summary>市場価格（既定 Params）。</summary>
        public static float MarketPrice(float fundamentalValue, float maniaIntensity)
            => MarketPrice(fundamentalValue, maniaIntensity, BubblePriceParams.Default);

        /// <summary>
        /// 市場価格＝実需価格（fundamentalValue）×バブル倍率（<see cref="BubbleMultiplier"/>）。
        /// 熱狂が無ければ実需そのまま、熱狂が高いほど実需の何倍にも膨らむ＝信念がファンダメンタルから価格を解離させる。
        /// </summary>
        public static float MarketPrice(float fundamentalValue, float maniaIntensity, BubblePriceParams p)
        {
            float fundamental = Mathf.Max(0f, fundamentalValue);
            return fundamental * BubbleMultiplier(maniaIntensity, p);
        }

        /// <summary>底割れ倍率（既定 Params）。</summary>
        public static float OvershootMultiplier(float crashSeverity)
            => OvershootMultiplier(crashSeverity, BubblePriceParams.Default);

        /// <summary>
        /// 崩壊の深さ（crashSeverity 0..1）→適正価格を下回る底割れ倍率（minOvershootMultiplier..1.0）。
        /// 崩壊なしで1.0倍（適正どおり）、恐怖が深いほど非線形に割れて最小 minOvershootMultiplier 倍まで底割れする
        /// ＝1 − (1−minOvershootMultiplier)×t^panicExponent。恐怖が価格を実需より下へオーバーシュートさせる（下側の行き過ぎ）。1.0未満。
        /// </summary>
        public static float OvershootMultiplier(float crashSeverity, BubblePriceParams p)
        {
            float t = Mathf.Clamp01(crashSeverity);
            return 1f - (1f - p.minOvershootMultiplier) * Mathf.Pow(t, p.panicExponent);
        }

        /// <summary>崩壊後の底割れ価格（既定 Params）。</summary>
        public static float CrashPrice(float fundamentalValue, float crashSeverity)
            => CrashPrice(fundamentalValue, crashSeverity, BubblePriceParams.Default);

        /// <summary>
        /// 崩壊後の底割れ価格＝実需価格（fundamentalValue）×底割れ倍率（<see cref="OvershootMultiplier"/>）。
        /// 恐怖が深いほど適正価格すら下回る＝行き過ぎた上げ（バブル）の後の行き過ぎた下げ（底割れ）。実需以下になりうる。
        /// </summary>
        public static float CrashPrice(float fundamentalValue, float crashSeverity, BubblePriceParams p)
        {
            float fundamental = Mathf.Max(0f, fundamentalValue);
            return fundamental * OvershootMultiplier(crashSeverity, p);
        }

        /// <summary>
        /// 解離度＝市場価格が実需価格からどれだけ離れたか（marketPrice/fundamentalValue − 1）。
        /// 0なら実需どおり、正なら割高（バブル方向）、負なら割安（底割れ方向）。実需≤0は基盤なし＝解離0（評価不能）。
        /// </summary>
        public static float Deviation(float marketPrice, float fundamentalValue)
        {
            float fundamental = Mathf.Max(0f, fundamentalValue);
            if (fundamental <= 0f) return 0f;
            float price = Mathf.Max(0f, marketPrice);
            return price / fundamental - 1f;
        }

        /// <summary>
        /// バブル判定＝解離度（<see cref="Deviation"/>）が閾値を超えて割高か。
        /// deviation &gt; threshold で true＝実需から閾値ぶん以上に吊り上がっていればバブル（割安＝負の解離はバブルでない）。
        /// </summary>
        public static bool IsBubble(float deviation, float threshold)
        {
            return deviation > Mathf.Max(0f, threshold);
        }

        /// <summary>既定の実需回帰速度（1.0＝dt=1で実需へ完全回帰）。</summary>
        public const float DefaultReversionSpeed = 1f;

        /// <summary>実需回帰（既定速度 <see cref="DefaultReversionSpeed"/>）。</summary>
        public static float MeanReversionTick(float price, float fundamentalValue, float dt)
            => MeanReversionTick(price, fundamentalValue, dt, DefaultReversionSpeed);

        /// <summary>
        /// 1tick の実需回帰：行き過ぎた価格（割高でも割安でも）を実需価格（fundamentalValue）へ滑らかに引き戻す
        /// （MoveTowards 風・移動量＝差×speed×dt＝指数的に寄る＝timeScale 追従）。
        /// バブルも底割れもいずれ均衡（実需）へ回帰する＝両側のオーバーシュートは時間で解消される。dt≤0/speed≤0 は据え置き。
        /// </summary>
        public static float MeanReversionTick(float price, float fundamentalValue, float dt, float speed)
        {
            float current = Mathf.Max(0f, price);
            float fundamental = Mathf.Max(0f, fundamentalValue);
            float s = Mathf.Max(0f, speed);
            if (dt <= 0f || s <= 0f) return current;
            float step = Mathf.Abs(fundamental - current) * s * dt;
            return Mathf.Max(0f, Mathf.MoveTowards(current, fundamental, step));
        }
    }

    /// <summary>
    /// バブル価格の調整値（熱狂の最大吊り上げ倍率と非線形度・恐怖の最小底割れ倍率と非線形度）。ctor で全てクランプ（#1622）。
    /// </summary>
    public readonly struct BubblePriceParams
    {
        /// <summary>熱狂最大時の最大バブル倍率（≥1。例 5.0＝価格が実需の5倍まで吊り上がる）。</summary>
        public readonly float maxBubbleMultiplier;
        /// <summary>熱狂→吊り上げの非線形指数（≥1。大きいほど熱狂が極まってから一気に跳ねる）。</summary>
        public readonly float maniaExponent;
        /// <summary>崩壊最深時の最小底割れ倍率（0..1。例 0.4＝適正の4割まで底割れ）。</summary>
        public readonly float minOvershootMultiplier;
        /// <summary>恐怖→底割れの非線形指数（≥1。大きいほど恐怖が深まってから一気に割れる）。</summary>
        public readonly float panicExponent;

        public BubblePriceParams(
            float maxBubbleMultiplier, float maniaExponent, float minOvershootMultiplier, float panicExponent)
        {
            this.maxBubbleMultiplier = Mathf.Max(1f, maxBubbleMultiplier);     // 実需を下回る吊り上げにはしない
            this.maniaExponent = Mathf.Max(1f, maniaExponent);                 // 線形未満にしない＝非線形の跳ねを保証
            this.minOvershootMultiplier = Mathf.Clamp01(minOvershootMultiplier);
            this.panicExponent = Mathf.Max(1f, panicExponent);                 // 線形未満にしない＝非線形の割れを保証
        }

        /// <summary>
        /// 既定＝最大バブル倍率5（実需の5倍まで膨らむ）・熱狂指数2（極まってから跳ねる）・
        /// 最小底割れ倍率0.4（適正の4割まで底割れ）・恐怖指数2（深まってから割れる）。
        /// </summary>
        public static BubblePriceParams Default => new BubblePriceParams(5f, 2f, 0.4f, 2f);
    }
}
