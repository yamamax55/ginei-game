using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 職業の大分類（<b>日本標準職業分類 JSOC の大分類 A〜K を参考</b>・#110 職業版の標準化）。
    /// POP の職業（<see cref="Occupation"/>＝ゲーム駆動の少数6種）と人物（<see cref="Person"/>）の双方を、現実の職業分類体系へ写像する
    /// <b>標準分類レイヤー</b>。大分類（11群＋無職）に留める＝中分類73/小分類329へは降りない（タイクン回避・集約）。
    /// 解決は <see cref="OccupationClassificationRules"/> が唯一の窓口。純データ（非 MonoBehaviour）。
    /// </summary>
    /// <remarks>
    /// JSOC 大分類との対応：A 管理／B 専門的・技術的／C 事務／D 販売／E サービス／F 保安／G 農林漁業／
    /// H 生産工程／I 輸送・機械運転／J 建設・採掘／K 運搬・清掃・包装等。<see cref="無職"/> は JSOC 上の職業ではなく未就業状態
    /// （既存 <see cref="Occupation.無職"/> と平仄を合わせるための12番目のバケット）。
    /// </remarks>
    public enum OccupationCategory
    {
        管理,           // A 管理的職業従事者（会社役員・管理的公務員・議員）
        専門技術,       // B 専門的・技術的職業従事者（技術者・研究者・医師・教員）
        事務,           // C 事務従事者（一般事務・会計）
        販売,           // D 販売従事者（商業・営業）
        サービス,       // E サービス職業従事者（接客・生活衛生・介護）
        保安,           // F 保安職業従事者（自衛官・警察＝#96 徴募源）
        農林漁業,       // G 農林漁業従事者
        生産工程,       // H 生産工程従事者（製造・組立）
        輸送機械運転,   // I 輸送・機械運転従事者
        建設採掘,       // J 建設・採掘従事者（採掘＝鉱員はここ）
        運搬清掃包装,   // K 運搬・清掃・包装等従事者
        無職            // （JSOC外＝未就業。Occupation.無職 と対応）
    }

    /// <summary>
    /// JSOC 大分類ベースの職業構成（<see cref="Workforce"/> の標準分類版・純データ）。シェアは0..1で合計≒1。
    /// POP の少数6種（<see cref="Workforce"/>）を <see cref="OccupationClassificationRules.Classify"/> で写像、または
    /// 惑星類型から <see cref="OccupationClassificationRules.Default"/> で生成する。<b>Province には保存しない（派生ビュー）</b>＝
    /// シリアライズ非破壊・タイクン回避（集約・観測層の思想）。
    /// </summary>
    [System.Serializable]
    public class OccupationProfile
    {
        public const int Count = 12; // OccupationCategory の要素数
        public float[] shares = new float[Count];

        public OccupationProfile() { }

        public OccupationProfile(float[] src)
        {
            shares = new float[Count];
            if (src != null)
                for (int i = 0; i < Count && i < src.Length; i++) shares[i] = Mathf.Max(0f, src[i]);
        }

        public float Share(OccupationCategory c) => shares[(int)c];

        public void SetShare(OccupationCategory c, float v) => shares[(int)c] = Mathf.Max(0f, v);

        public void AddShare(OccupationCategory c, float v) => shares[(int)c] += Mathf.Max(0f, v);

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

        /// <summary>最大シェアの大分類（無職を除く就業者の最多群。全0なら無職）。</summary>
        public OccupationCategory Dominant()
        {
            int best = (int)OccupationCategory.無職;
            float bestShare = -1f;
            for (int i = 0; i < Count; i++)
            {
                if (i == (int)OccupationCategory.無職) continue;
                if (shares[i] > bestShare) { bestShare = shares[i]; best = i; }
            }
            return bestShare <= 0f ? OccupationCategory.無職 : (OccupationCategory)best;
        }
    }
}
