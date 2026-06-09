using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 統治体＝「権力は借り物」（ガンジー #835/#836/#837）の単位。少数の支配側(<see cref="rulerForce"/>)が
    /// 多数の被支配者(<see cref="population"/>)を統べられるのは、被支配者の<see cref="cooperation"/>（協力/同意）が
    /// 行政・徴税・軍という“プログラム”を実行しているから。協力が引き上がる（非協力）と、戦わずに統治不能になる。
    /// 解決は <see cref="ConsentRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Polity
    {
        public int id;
        public Faction ruler;
        public int population;        // 被支配者の規模
        public int rulerForce;        // 支配側の直接戦力（少数）
        public float cooperation = 1f; // 被支配者の協力/同意 0..1（統治の耐荷重壁）
        public float legitimacy = 1f;  // 統治の正統性 0..1（協力を支える）
        public float oppression = 0f;  // 収奪/抑圧 0..1（協力を蝕む＝収奪的制度 GEO-2 #843）

        public Polity() { }

        public Polity(int id, Faction ruler, int population, int rulerForce,
            float cooperation = 1f, float legitimacy = 1f, float oppression = 0f)
        {
            this.id = id;
            this.ruler = ruler;
            this.population = Mathf.Max(0, population);
            this.rulerForce = Mathf.Max(0, rulerForce);
            this.cooperation = Mathf.Clamp01(cooperation);
            this.legitimacy = Mathf.Clamp01(legitimacy);
            this.oppression = Mathf.Clamp01(oppression);
        }
    }
}
