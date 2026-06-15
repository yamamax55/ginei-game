using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文化・民族・ナショナリズムの最小核（#194・宗教 #172 の姉妹）。地理/歴史から創発する民族集団の純データ。
    /// 占領しても住民の文化は即は変わらない＝不安定・分離独立の源。多数派文化への <see cref="assimilation"/>(同化度)が
    /// 進むほど統合され、低いまま安定が割れると <see cref="isMinority"/> な民族は分離独立リスクが高まる。
    /// ナショナリズムは結束/士気の実効係数として効く。解決は <see cref="CultureRules"/>（static）が唯一の窓口。
    /// 純データ（非 MonoBehaviour・test-first）。<see cref="Province"/>(内政)の文化版・姉妹データ。
    /// </summary>
    [System.Serializable]
    public class Culture
    {
        /// <summary>文化・民族名（識別子。多数派文化との一致判定に使う）。</summary>
        public string cultureName = "";

        /// <summary>人口規模（Pop。民族の重み・士気スケール）。</summary>
        public float population = 100f;

        /// <summary>多数派文化への同化度（0＝占領直後/未同化 .. 1＝完全同化）。低いほど分離独立しやすい。</summary>
        public float assimilation = 1f;

        /// <summary>少数民族か（true＝支配勢力と異なる被支配民族＝分離独立の主体）。</summary>
        public bool isMinority;

        public Culture() { }

        public Culture(string cultureName, float population = 100f, float assimilation = 1f, bool isMinority = false)
        {
            this.cultureName = cultureName ?? "";
            this.population = Mathf.Max(0f, population);
            this.assimilation = Mathf.Clamp01(assimilation);
            this.isMinority = isMinority;
        }
    }
}
