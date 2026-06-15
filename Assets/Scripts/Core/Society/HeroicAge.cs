using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 時代の局面（キングダム＝王騎の語る「英雄の時代」）。
    /// 英雄時代＝個の武・将才が戦を決める／英雄なき時代＝数と兵站と組織が戦を決める／移行期＝その狭間。
    /// </summary>
    public enum HeroicEra { 英雄なき時代, 移行期, 英雄時代 }

    /// <summary>
    /// 時代の英雄度の状態（#英雄時代）。世界の「英雄度」を 0..1 で保持する純データ。
    /// 高いほど英雄時代（個の将才が戦場を支配）、低いほど英雄なき時代（数・兵站・組織が支配）。
    /// 英雄（軍神/傑物）が世にあれば上がり、死に絶えれば下がる＝<see cref="HeroicAgeRules"/> が時代を駆動する。
    /// </summary>
    [System.Serializable]
    public class HeroicAgeState
    {
        /// <summary>英雄度（0..1）。既定 0.5＝移行期から始まる。</summary>
        public float heroism = 0.5f;

        public HeroicAgeState() { }
        public HeroicAgeState(float heroism) { this.heroism = Mathf.Clamp01(heroism); }

        /// <summary>現在の時代局面（英雄度から導出）。</summary>
        public HeroicEra Era => HeroicAgeRules.EraFor(heroism);
    }
}
