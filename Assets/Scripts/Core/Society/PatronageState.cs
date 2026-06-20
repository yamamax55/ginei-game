using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 後見依存の状態（CEND-2 #2358）。外部の庇護者から受け続ける援助の蓄積と、
    /// それが生む<b>依存度</b>(0..1)を保持する。依存度が高いほど短期の指標は底上げされるが、
    /// 自律的（有機的）発展の成長上限が下がる＝<b>依存の罠</b>。`PatronageTrapRules` が更新・解釈する純データ。
    /// 直列化可能な plain struct（乱数なし・決定論）。
    /// </summary>
    [System.Serializable]
    public struct PatronageState
    {
        /// <summary>依存度(0..1)。1＝完全に庇護者頼み＝自力発展が止まる。</summary>
        public float dependencyRatio;

        /// <summary>受領援助の累積カウンタ（無次元・援助量の積み上げ）。依存度の供給源。</summary>
        public float aidAccumulator;

        public PatronageState(float dependencyRatio, float aidAccumulator)
        {
            this.dependencyRatio = Mathf.Clamp01(dependencyRatio);
            this.aidAccumulator = Mathf.Max(0f, aidAccumulator);
        }

        /// <summary>依存ゼロの初期状態（自力でやってきた勢力＝従来動作）。</summary>
        public static PatronageState None => new PatronageState(0f, 0f);
    }
}
