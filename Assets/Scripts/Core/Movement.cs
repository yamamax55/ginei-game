using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 非暴力運動（公民権 #829・ガンジー #835/#836／CIVIL-2/3 #831/#832）。献身する少数(<see cref="commitment"/>)が
    /// 暴力に暴力で返さず、敵の弾圧を可視化することで沈黙の多数の<see cref="support"/>（世論の支持）を獲得する。
    /// 支持が閾値を超えると、戦わずに勝つ（権力は借り物 #837＝統治者は譲歩せざるを得ない）。
    /// 解決は <see cref="NonviolenceRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Movement
    {
        public int id;
        public Faction cause;          // 運動が代表する側（任意）
        public float support = 0.1f;   // 世論の支持 0..1（沈黙の多数）
        public float commitment = 0.5f; // 献身する少数の覚悟/規模 0..1（投獄も辞さぬ核）

        public Movement() { }

        public Movement(int id, Faction cause, float support = 0.1f, float commitment = 0.5f)
        {
            this.id = id;
            this.cause = cause;
            this.support = Mathf.Clamp01(support);
            this.commitment = Mathf.Clamp01(commitment);
        }
    }
}
