using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// プレイヤー勢力の資産運用（アセットマネジメント）の純ロジック。資産クラス別の額から
    /// 総資産・配分比率・純資産（負債控除後）・集中度（ハーフィンダール）・実効分散クラス数・レバレッジ比を
    /// 決定論で算出する唯一の窓口。台帳（国庫=FiscalState／持分=NetWorthRules／企業=Enterprise 等）は持たず
    /// 値を受け取って計算するだけ＝二重実装しない。<see cref="AssetManagementOverlay"/> が描画に使う。test-first。
    /// </summary>
    public static class AssetPortfolioRules
    {
        /// <summary>資産クラス（プレイヤーのポートフォリオを分ける軸）。</summary>
        public enum AssetClass { 現金, 金融資産, 土地, 事業, その他 }

        /// <summary>総資産＝各クラスの非負合計（負値は0扱い）。</summary>
        public static float Gross(float[] values)
        {
            if (values == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < values.Length; i++) sum += Mathf.Max(0f, values[i]);
            return sum;
        }

        /// <summary>あるクラスの配分比率（part/gross を 0..1 にクランプ。gross≤0 は0）。</summary>
        public static float Fraction(float part, float gross)
            => gross <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, part) / gross);

        /// <summary>純資産＝総資産−負債（負債が総資産を超えれば負＝債務超過）。</summary>
        public static float NetWorth(float gross, float liabilities)
            => gross - Mathf.Max(0f, liabilities);

        /// <summary>レバレッジ比＝負債/総資産（0..1+。gross≤0 かつ負債ありは1）。</summary>
        public static float LeverageRatio(float liabilities, float gross)
        {
            float d = Mathf.Max(0f, liabilities);
            if (gross <= 0f) return d > 0f ? 1f : 0f;
            return d / gross;
        }

        /// <summary>集中度（ハーフィンダール指数）＝Σ(配分比率²)。1=一極集中／小=分散。gross≤0 は0。</summary>
        public static float Herfindahl(float[] values)
        {
            float gross = Gross(values);
            if (gross <= 0f || values == null) return 0f;
            float h = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                float f = Mathf.Max(0f, values[i]) / gross;
                h += f * f;
            }
            return h;
        }

        /// <summary>実効分散クラス数＝1/HHI（1=一極／大=よく分散）。gross≤0 は0。</summary>
        public static float EffectiveClasses(float[] values)
        {
            float h = Herfindahl(values);
            return h <= 0f ? 0f : 1f / h;
        }
    }
}
