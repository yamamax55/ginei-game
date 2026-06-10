using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// バリューチェーンの1加工段（#1023・純データ）。森→木→製材→家のような付加価値の流れの一段＝
    /// 投入価値 <see cref="inputValue"/>（前段から受け取る原料/中間財の価値）に、加工で価値を上乗せし（<see cref="valueAdded"/>）、
    /// 歩留まり <see cref="yieldRatio"/>（0..1＝この段で歩留まる割合・端材ロスや不良）で目減りする。
    /// 段の連鎖（森→木→製材→家）の解決ロジックは <see cref="ValueChainRules"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class ValueChainStage
    {
        /// <summary>段名（任意・森/木/製材/家など）。</summary>
        public string stageName;
        /// <summary>前段から受け取る投入価値（原料・中間財の価値・負はクランプ）。連鎖の起点（森）では原料賦存の価値。</summary>
        public float inputValue;
        /// <summary>この段の加工で上乗せする付加価値（製材なら丸太を板に挽く手間ぶん・負はクランプ）。</summary>
        public float valueAdded;
        /// <summary>この段の歩留まり（0..1＝端材ロス等で投入のうち先へ通る割合・1で無損失）。</summary>
        public float yieldRatio;

        public ValueChainStage() { }

        public ValueChainStage(float inputValue, float valueAdded, float yieldRatio, string stageName = null)
        {
            this.inputValue = inputValue;
            this.valueAdded = valueAdded;
            this.yieldRatio = yieldRatio;
            this.stageName = stageName;
        }
    }

    /// <summary>バリューチェーンの調整係数。</summary>
    public readonly struct ValueChainParams
    {
        /// <summary>垂直統合で取り込める1段あたりの中間マージン率の上限（既定0.30＝中間業者の取り分の頭打ち）。</summary>
        public readonly float maxMarginPerStage;

        public ValueChainParams(float maxMarginPerStage)
        {
            this.maxMarginPerStage = Mathf.Clamp01(maxMarginPerStage);
        }

        /// <summary>既定＝1段あたり中間マージン取り込み上限0.30。</summary>
        public static ValueChainParams Default => new ValueChainParams(0.30f);
    }

    /// <summary>
    /// バリューチェーン（価値連鎖・#1023・唯一の窓口）。森→木→製材→家の加工段連鎖＝各段が原料に価値を上乗せして
    /// 最終財まで価値が積み上がる網（<see cref="CumulativeValue"/>）。各段の歩留まりは積で効き、1段でも悪いと全体が痩せる
    /// （<see cref="ChainYield"/>＝<see cref="BottleneckStageByYield"/> が律速段）。付加価値が川上(原料)と川下(最終財)の
    /// どちらに偏るか＝スマイルカーブ（<see cref="ValueCaptureByStage"/>＝どの段を押さえるかが利益を決める）。各段を自社で持てば
    /// 中間マージンを取り込める（<see cref="VerticalIntegrationGain"/>＝森から家まで一貫生産）。
    /// <see cref="CoupledProductionRules"/>（単一工程の連産＝1工程が複数財を同時に産む）の各段を時系列に<b>繋ぐ</b>網がこちら。
    /// <see cref="MarketRules"/>（単一財の需給・価格均衡）・<see cref="FirmRules"/>（企業の生産/採算・同Wave並行）とは別。
    /// 乱数なし・決定論・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ValueChainRules
    {
        /// <summary>
        /// 1段の付加価値（原料を加工して上乗せする価値）＝<see cref="ValueChainStage.valueAdded"/> をクランプして返す純関数。
        /// null は0。
        /// </summary>
        public static float AddedValue(ValueChainStage stage)
        {
            if (stage == null) return 0f;
            return Mathf.Max(0f, stage.valueAdded);
        }

        /// <summary>
        /// 連鎖全体の累積価値（最終財までの価値）＝森→木→製材→家と各段の付加価値が積み上がった総和。
        /// 第1段の投入価値（原料賦存）＋各段の付加価値の合計（投入は連鎖内で受け渡すので二重計上しない）。
        /// stages が空/null は0。
        /// </summary>
        public static float CumulativeValue(ValueChainStage[] stages)
        {
            if (stages == null || stages.Length == 0) return 0f;
            float total = 0f;
            bool baseTaken = false;
            for (int i = 0; i < stages.Length; i++)
            {
                ValueChainStage s = stages[i];
                if (s == null) continue;
                if (!baseTaken)
                {
                    total += Mathf.Max(0f, s.inputValue); // 連鎖起点の原料価値を一度だけ
                    baseTaken = true;
                }
                total += Mathf.Max(0f, s.valueAdded);
            }
            return total;
        }

        /// <summary>
        /// 連鎖全体の歩留まり（各段の歩留まりの積＝1段でも歩留まりが悪いと全体が痩せる）。0..1。
        /// 段が無い/全 null は1.0（無損失）。
        /// </summary>
        public static float ChainYield(ValueChainStage[] stages)
        {
            if (stages == null || stages.Length == 0) return 1f;
            float product = 1f;
            for (int i = 0; i < stages.Length; i++)
            {
                ValueChainStage s = stages[i];
                if (s == null) continue;
                product *= Mathf.Clamp01(s.yieldRatio);
            }
            return Mathf.Clamp01(product);
        }

        /// <summary>
        /// 各段の価値の取り分（付加価値の分布＝スマイルカーブ）。各段 = その段の付加価値 ÷ 全段の付加価値合計（0..1・総和1）。
        /// 川上(原料)と川下(最終財)のどちらが儲かるか＝どの段を押さえれば利益を取れるかを表す。
        /// 付加価値合計が0（全段0）なら各段0。stages のサイズで配列を返す（null 段は0）。
        /// </summary>
        public static float[] ValueCaptureByStage(ValueChainStage[] stages)
        {
            int n = stages == null ? 0 : stages.Length;
            float[] share = new float[n];
            if (n == 0) return share;

            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                ValueChainStage s = stages[i];
                if (s == null) continue;
                total += Mathf.Max(0f, s.valueAdded);
            }
            if (total <= 0f) return share; // 付加価値ゼロ＝取り分は全0

            for (int i = 0; i < n; i++)
            {
                ValueChainStage s = stages[i];
                if (s == null) continue;
                share[i] = Mathf.Max(0f, s.valueAdded) / total;
            }
            return share;
        }

        /// <summary>
        /// 歩留まりが最悪の律速段のインデックス（連鎖全体を最も痩せさせている段）。同率なら先勝ち。
        /// stages が空/全 null は -1。
        /// </summary>
        public static int BottleneckStageByYield(ValueChainStage[] stages)
        {
            int n = stages == null ? 0 : stages.Length;
            int idx = -1;
            float worst = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                ValueChainStage s = stages[i];
                if (s == null) continue;
                float y = Mathf.Clamp01(s.yieldRatio);
                if (y < worst)
                {
                    worst = y;
                    idx = i;
                }
            }
            return idx;
        }

        /// <summary>
        /// 垂直統合の利得＝各段を自社で持てば中間業者に払うマージンを取り込める（森から家まで一貫生産）。
        /// 段の境界の数（段数-1＝受け渡し回数）× 各段の付加価値合計 × marginPerStage（0..maxMarginPerStage にクランプ）。
        /// 段が1以下なら受け渡しが無い＝利得0。付加価値が0でも0。
        /// </summary>
        public static float VerticalIntegrationGain(ValueChainStage[] stages, float marginPerStage, ValueChainParams p)
        {
            int n = stages == null ? 0 : stages.Length;
            if (n <= 1) return 0f;

            int handoffs = n - 1; // 段間の受け渡し（中間マージンが発生する境界）の数
            float addedTotal = 0f;
            for (int i = 0; i < n; i++)
            {
                ValueChainStage s = stages[i];
                if (s == null) continue;
                addedTotal += Mathf.Max(0f, s.valueAdded);
            }
            float margin = Mathf.Clamp(marginPerStage, 0f, p.maxMarginPerStage);
            return handoffs * addedTotal * margin;
        }

        public static float VerticalIntegrationGain(ValueChainStage[] stages, float marginPerStage)
            => VerticalIntegrationGain(stages, marginPerStage, ValueChainParams.Default);
    }
}
