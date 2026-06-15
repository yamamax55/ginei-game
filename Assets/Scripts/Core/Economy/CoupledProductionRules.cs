using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 連産品レシピ（#1110・純データ）。1工程が複数の財を<b>固定比で同時に</b>産む不可分なレシピ。
    /// 投入財の係数列 <see cref="inputs"/>（1稼働あたりの消費量）と出力財の係数列 <see cref="outputs"/>（1稼働あたりの産出量）を持つ。
    /// 「石油を精製すれば軽油も重油も同時に出る」型＝主産物を1つ作れば従産物も望むと望まざるとに関わらず出てくる。
    /// 解決ロジックは <see cref="CoupledProductionRules"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class JointRecipe
    {
        /// <summary>レシピ名（任意）。</summary>
        public string recipeName;
        /// <summary>1稼働あたりの投入消費量（財インデックスごと・負はクランプ）。空＝無投入。</summary>
        public float[] inputs;
        /// <summary>1稼働あたりの固定比産出量（財インデックスごと・負はクランプ）。要素0が主産物、以降が従産物の慣例。</summary>
        public float[] outputs;

        public JointRecipe() { }

        public JointRecipe(float[] inputs, float[] outputs, string recipeName = null)
        {
            this.inputs = inputs;
            this.outputs = outputs;
            this.recipeName = recipeName;
        }

        /// <summary>出力財の本数。</summary>
        public int OutputCount => outputs == null ? 0 : outputs.Length;
        /// <summary>投入財の本数。</summary>
        public int InputCount => inputs == null ? 0 : inputs.Length;
    }

    /// <summary>連産品生産の調整係数。</summary>
    public readonly struct CoupledProductionParams
    {
        /// <summary>1稼働あたりの設備占有量（稼働率の分母 maxCapacity と同じ単位＝既定1.0＝1稼働=1キャパ）。</summary>
        public readonly float capacityPerRun;

        public CoupledProductionParams(float capacityPerRun)
        {
            this.capacityPerRun = Mathf.Max(0f, capacityPerRun);
        }

        /// <summary>既定＝1稼働あたり設備占有1.0。</summary>
        public static CoupledProductionParams Default => new CoupledProductionParams(1f);
    }

    /// <summary>
    /// 連産品（連産・同時産出）の純ロジック（#1110・唯一の窓口）。1工程が複数財を<b>不可分な固定比で同時に</b>産む
    /// ＝銀英伝の戦時生産網の連鎖の起点。主産物の需要に投入を合わせれば、従産物は固定比で強制的に湧き出る
    /// （<see cref="ForcedByproduct"/>＝連産の宿命＝1つを欲しがれば全部が出てくる）。投入は最も制約的な財が律速
    /// （リービッヒの最小律＝<see cref="MaxRuns"/>/<see cref="BottleneckInput"/>）。
    /// <see cref="MarketRules"/>（単一財の需給・価格均衡）とは別＝こちらは<b>生産の結合</b>（複数財の同時産出）を扱う。
    /// <see cref="ResourceProductionRules"/>（類型別の単純産出）とも別＝固定比の連鎖を初めて式にする。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CoupledProductionRules
    {
        /// <summary>
        /// 投入で回せる最大稼働回数（端数切り捨て）＝最も制約的な投入財が律速（リービッヒの最小律）。
        /// availableInputs[i] が recipe.inputs[i] の何倍あるかを各財で見て、その最小が稼働上限。
        /// 投入が無いレシピ（inputs 空＝全係数0）は availableInputs に縛られず int.MaxValue（無限稼働可）を返す。
        /// </summary>
        public static int MaxRuns(JointRecipe recipe, float[] availableInputs)
        {
            if (recipe == null) return 0;
            int n = recipe.InputCount;
            if (n == 0) return int.MaxValue; // 無投入＝律速なし

            float limit = float.PositiveInfinity;
            bool anyConstraint = false;
            for (int i = 0; i < n; i++)
            {
                float per = Mathf.Max(0f, recipe.inputs[i]);
                if (per <= 0f) continue; // この財は消費しない＝律速にならない
                anyConstraint = true;
                float avail = (availableInputs != null && i < availableInputs.Length)
                    ? Mathf.Max(0f, availableInputs[i]) : 0f;
                float runs = avail / per;
                if (runs < limit) limit = runs;
            }
            if (!anyConstraint) return int.MaxValue; // 全係数0＝実質無投入
            return Mathf.Max(0, Mathf.FloorToInt(limit));
        }

        /// <summary>
        /// 律速になっている投入財のインデックス（最も稼働回数を絞っている財）。-1＝律速なし（無投入 or 各財十分）。
        /// </summary>
        public static int BottleneckInput(JointRecipe recipe, float[] availableInputs)
        {
            if (recipe == null) return -1;
            int n = recipe.InputCount;
            int idx = -1;
            float limit = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                float per = Mathf.Max(0f, recipe.inputs[i]);
                if (per <= 0f) continue;
                float avail = (availableInputs != null && i < availableInputs.Length)
                    ? Mathf.Max(0f, availableInputs[i]) : 0f;
                float runs = avail / per;
                if (runs < limit)
                {
                    limit = runs;
                    idx = i;
                }
            }
            return idx;
        }

        /// <summary>
        /// 指定稼働回数での固定比同時産出（出力財の配列を返す＝石油精製で軽油も重油も同時に出る型）。
        /// 各出力財 = outputs[i] × runs（不可分＝個別に止められない）。runs 負はクランプ。
        /// </summary>
        public static float[] Produce(JointRecipe recipe, int runs)
        {
            int m = recipe == null ? 0 : recipe.OutputCount;
            float[] result = new float[m];
            if (m == 0) return result;
            int r = Mathf.Max(0, runs);
            for (int i = 0; i < m; i++)
                result[i] = Mathf.Max(0f, recipe.outputs[i]) * r;
            return result;
        }

        /// <summary>
        /// 指定稼働回数での投入消費（投入財の配列を返す）。各投入財 = inputs[i] × runs。runs 負はクランプ。
        /// </summary>
        public static float[] InputConsumption(JointRecipe recipe, int runs)
        {
            int n = recipe == null ? 0 : recipe.InputCount;
            float[] result = new float[n];
            if (n == 0) return result;
            int r = Mathf.Max(0, runs);
            for (int i = 0; i < n; i++)
                result[i] = Mathf.Max(0f, recipe.inputs[i]) * r;
            return result;
        }

        /// <summary>
        /// 主産物（outputs[primaryIndex]）の需要 primaryDemand を満たすのに必要な稼働で、
        /// <b>道連れに出てくる従産物の量</b>を返す（出力財の配列＝従産物=その他のインデックス）。
        /// 連産の宿命＝主産物を作れば従産物は望むと望まざるとに関わらず固定比で湧く（過剰在庫の起点）。
        /// 必要稼働は端数切り上げ（需要を下回らないため）。主産物の産出係数が0、または需要0以下なら全0。
        /// </summary>
        public static float[] ForcedByproduct(JointRecipe recipe, float primaryDemand, int primaryIndex = 0)
        {
            int m = recipe == null ? 0 : recipe.OutputCount;
            float[] result = new float[m];
            if (m == 0 || primaryIndex < 0 || primaryIndex >= m) return result;

            float primaryPerRun = Mathf.Max(0f, recipe.outputs[primaryIndex]);
            float demand = Mathf.Max(0f, primaryDemand);
            if (primaryPerRun <= 0f || demand <= 0f) return result;

            int runs = Mathf.CeilToInt(demand / primaryPerRun);
            return Produce(recipe, runs);
        }

        /// <summary>
        /// 設備稼働率（0..1）＝稼働回数×1稼働あたり設備占有 ÷ 設備上限 maxCapacity。
        /// maxCapacity 以下にクランプ（過負荷は1.0頭打ち）。maxCapacity≤0 は0。
        /// </summary>
        public static float CapacityUtilization(int runs, float maxCapacity, CoupledProductionParams p)
        {
            if (maxCapacity <= 0f) return 0f;
            float used = Mathf.Max(0, runs) * p.capacityPerRun;
            return Mathf.Clamp01(used / maxCapacity);
        }

        public static float CapacityUtilization(int runs, float maxCapacity)
            => CapacityUtilization(runs, maxCapacity, CoupledProductionParams.Default);
    }
}
