using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力レベルの「集合記憶×市民宗教」の合成状態（薄いオーケストレータ <see cref="CulturalMemoryTickRules"/> 用の自作 state）。
    /// 既存の未配線 Core（<see cref="GenerationalMemoryRules"/>＝戦争記憶の世代風化、<see cref="CivicFaithRules"/>＝市民宗教）を
    /// 1つの器に束ね、民心（支持）への背景modifier・占領同化・実効開戦閾値の源にする。
    /// 数値の解決は委譲先（<see cref="GenerationalMemoryRules"/>/<see cref="CivicFaithRules"/>）へ委ね、ここは器（純データ）に徹する。
    /// per-system の <see cref="Culture"/>（#194・惑星単位の民族）や、勢力全体の結束/信仰を持つ
    /// <see cref="CultureState"/>（<see cref="CultureSynthesisTickRules"/> 用）とは別物＝こちらは「記憶×紐帯」の薄い合成。
    /// 純データ（非 MonoBehaviour・test-first）。決定論・bounded。
    /// </summary>
    [System.Serializable]
    public class CulturalMemoryState
    {
        /// <summary>市民宗教への信奉（0..1）＝国家への情緒的紐帯。<see cref="CivicFaithRules"/> が回す。</summary>
        public float devotion = 0.5f;

        /// <summary>内面の真摯さ（0..1）＝本心からの信仰。低いと形骸化＝紐帯（支持）に効かない。</summary>
        public float sincerity = 0.5f;

        /// <summary>終戦からの経過年。大きいほど集合記憶が風化し、開戦の敷居が下がる。</summary>
        public float yearsSinceWar = 0f;

        /// <summary>直近の戦争の苛烈さ（0..1）＝集合記憶の初期強度（酷い戦争ほど深く刻まれる）。</summary>
        public float warSeverity = 0.5f;

        public CulturalMemoryState() { }

        public CulturalMemoryState(float devotion, float sincerity, float yearsSinceWar, float warSeverity)
        {
            this.devotion = Mathf.Clamp01(devotion);
            this.sincerity = Mathf.Clamp01(sincerity);
            this.yearsSinceWar = Mathf.Max(0f, yearsSinceWar);
            this.warSeverity = Mathf.Clamp01(warSeverity);
        }
    }
}
