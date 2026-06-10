using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 共同体の希望（フロストパンク「ロンドン派」のメカニクス化・#852/#853/#854）。希望(<see cref="hope"/>)が
    /// 尽きると末人＝ロンドン派(<see cref="dissent"/>)が内部に立つ（意味を失い崩壊する）。対抗手段は
    /// 信仰ルート（意味を捏造して希望を上げる）か秩序ルート（力で抑える＝<see cref="repression"/>を上げる）で、
    /// 秩序を進めすぎると専制（虚構の暴走 #856）になる。FRONT 末人 #847 を遊べる形にする土台。
    /// 解決は <see cref="HopeRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Community
    {
        public int id;
        public float hope = 1f;        // 希望 0..1（フロストパンクの Hope）
        public float repression = 0f;  // 秩序ルートの抑圧 0..1（力で不満を抑える）
        public bool dissent;           // 末人（ロンドン派）が立ったか

        public Community() { }

        public Community(int id, float hope = 1f, float repression = 0f)
        {
            this.id = id;
            this.hope = Mathf.Clamp01(hope);
            this.repression = Mathf.Clamp01(repression);
        }
    }
}
