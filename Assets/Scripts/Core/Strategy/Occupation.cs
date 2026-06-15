using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP の職業（生産年齢人口の就労先・#110/#96 連携）。<b>少数に絞る</b>（タイクン回避）。
    /// 一次（農民/鉱員）・二次（工員）・三次（官吏）＋兵（軍属）＋無職。生産年齢(<see cref="Population.working"/>)を職に割り振る。
    /// 解決は <see cref="OccupationRules"/> が唯一の窓口。
    /// </summary>
    public enum Occupation { 農民, 工員, 鉱員, 官吏, 軍属, 無職 }

    /// <summary>
    /// 労働力構成＝生産年齢人口の職業別シェア（#110 P-1 の職業版・純データ）。シェアは0..1で合計≒1（<see cref="Normalize"/>）。
    /// 実数（人）は <see cref="OccupationRules.Workers"/> がシェア×生産年齢で出す。所有勢力/人口規模は Province 側が持つ。
    /// </summary>
    [System.Serializable]
    public class Workforce
    {
        // Occupation のインデックスで引くシェア配列（長さ＝enum 要素数）。
        public const int Count = 6;
        public float[] shares = new float[Count];

        public Workforce() { }

        public Workforce(float[] src)
        {
            shares = new float[Count];
            if (src != null)
                for (int i = 0; i < Count && i < src.Length; i++) shares[i] = Mathf.Max(0f, src[i]);
        }

        public float Share(Occupation o) => shares[(int)o];

        public void SetShare(Occupation o, float v) => shares[(int)o] = Mathf.Max(0f, v);

        /// <summary>シェア合計（正規化前は1とは限らない）。</summary>
        public float Total
        {
            get { float s = 0f; for (int i = 0; i < Count; i++) s += shares[i]; return s; }
        }

        /// <summary>合計が1になるよう正規化（合計0なら何もしない）。</summary>
        public void Normalize()
        {
            float t = Total;
            if (t <= 0f) return;
            for (int i = 0; i < Count; i++) shares[i] /= t;
        }
    }
}
