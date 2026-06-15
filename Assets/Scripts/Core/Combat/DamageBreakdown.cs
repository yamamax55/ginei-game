using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦ダメージの総合倍率クランプ（#2252）。多数の修飾子が乗算で積み上がっても与ダメが発散しないよう上下限を設ける。
    /// `ShipCombat.ComputeDamage` が最終倍率に必ず適用する（ホットパスは無確保）。test-first。
    /// </summary>
    public static class DamageClampRules
    {
        /// <summary>総合倍率の下限（劣勢でもこれ以下に潰れない）。</summary>
        public const float MinTotal = 0.2f;
        /// <summary>総合倍率の上限（修飾子が積み上がっても暴れない）。</summary>
        public const float MaxTotal = 5.0f;

        /// <summary>総合倍率をクランプする（負は0扱い→下限へ）。</summary>
        public static float Clamp(float total) => Mathf.Clamp(Mathf.Max(0f, total), MinTotal, MaxTotal);
    }

    /// <summary>
    /// 会戦ダメージの内訳（#2252・可視化用）。基本ダメージ＋各修飾子（ラベル×倍率）を記録し、総合倍率と最終ダメージを出す。
    /// 「基本N ×攻撃1.2 ×士気0.9 …」を見せ、バランス調整を容易にする。<b>ホットパスでは作らない</b>＝オンデマンド/サンプリングで使う。test-first。
    /// </summary>
    public class DamageBreakdown
    {
        public int baseDamage;
        public readonly List<KeyValuePair<string, float>> entries = new List<KeyValuePair<string, float>>();

        public void Reset(int baseDmg) { baseDamage = baseDmg; entries.Clear(); }

        /// <summary>等倍(1.0)以外の修飾子を記録する（等倍は内訳から省く）。</summary>
        public void Add(string label, float factor)
        {
            if (Mathf.Approximately(factor, 1f)) return;
            entries.Add(new KeyValuePair<string, float>(label, factor));
        }

        /// <summary>クランプ前の総合倍率（各修飾子の積）。</summary>
        public float RawMultiplier
        {
            get { float p = 1f; for (int i = 0; i < entries.Count; i++) p *= entries[i].Value; return p; }
        }

        /// <summary>クランプ後の総合倍率（実際に適用される倍率）。</summary>
        public float ClampedMultiplier => DamageClampRules.Clamp(RawMultiplier);

        /// <summary>最終ダメージ（基本×クランプ後倍率・非負）。</summary>
        public int Result => Mathf.Max(0, Mathf.RoundToInt(baseDamage * ClampedMultiplier));

        /// <summary>クランプが効いた（生の倍率が上下限で頭打ち/底上げされた）か。</summary>
        public bool WasClamped => !Mathf.Approximately(RawMultiplier, ClampedMultiplier);

        /// <summary>「基本N ×攻撃1.20 ×士気0.90 … ＝最終M」形式の説明文。</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("基本").Append(baseDamage);
            for (int i = 0; i < entries.Count; i++)
                sb.Append(" ×").Append(entries[i].Key).Append(entries[i].Value.ToString("0.00"));
            if (WasClamped) sb.Append("（クランプ）");
            sb.Append(" ＝").Append(Result);
            return sb.ToString();
        }
    }
}
