using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宗教の純データ（#172-175・R-1 創発とPOP宗教＝#173 中心。聖戦/神権は最小スタブ）。
    /// 住民の信仰として星系/惑星に紐づく想定で、信仰の強さ <see cref="devotion"/>(0..1) が
    /// 社会効果（安定度・士気）に効く土台。占領しても即は改宗せず、時間で支配勢力の信仰へ寄る
    /// （#173 創発）。数値の解決は <see cref="ReligionRules"/>(static) が唯一の窓口。
    /// 所有勢力は StarSystem.owner が出所＝ここには持たない（住民信仰の状態のみ保持）。
    /// </summary>
    [System.Serializable]
    public class Religion
    {
        /// <summary>宗教の名称（信仰の識別子。空＝無信仰）。</summary>
        public string faithName = "";

        /// <summary>信仰の強さ（0＝無関心 .. 1＝熱狂）。社会効果と改宗速度の源。</summary>
        public float devotion = 0.5f;

        /// <summary>聖地のある星系ID（任意。-1＝聖地なし）。聖戦圧力の判定に使う。</summary>
        public int holySiteSystemId = -1;

        /// <summary>思想親和（FactionData.ideology 文字列。一致勢力の改宗が進みやすい）。</summary>
        public string ideologyAffinity = "";

        public Religion() { }

        public Religion(string faithName, float devotion = 0.5f, int holySiteSystemId = -1, string ideologyAffinity = "")
        {
            this.faithName = faithName ?? "";
            this.devotion = Mathf.Clamp01(devotion);
            this.holySiteSystemId = holySiteSystemId;
            this.ideologyAffinity = ideologyAffinity ?? "";
        }
    }
}
