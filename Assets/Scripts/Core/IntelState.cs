using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の諜報・情報戦の状態（諜報オーケストレータ用の最小 state）。諜報能力 capability（攻め＝
    /// 潜入・傍受・偵察の総合力）と防諜 counterIntel（守り＝自網の隠蔽・敵スパイ摘発）を 0..1 で持つ。
    /// 進行は <see cref="IntelligenceTickRules"/>（諜報予算で伸ばし、飽和でラチェットを防ぐ）。
    /// 数式は既存の諜報 Core（<see cref="EspionageRules"/>/<see cref="CounterIntelligenceRules"/>/
    /// <see cref="SignalIntelligenceRules"/>/<see cref="FogOfWarRules"/>）へ委譲し二重実装しない。純データ。
    /// </summary>
    [Serializable]
    public class IntelState
    {
        /// <summary>諜報能力 0..1（攻め＝潜入・傍受・解読・偵察の総合。可視度・工作成否の入力）。</summary>
        public float capability = 0f;

        /// <summary>防諜 0..1（守り＝自勢力の秘匿・敵スパイ摘発。相手から見た可視度を下げる）。</summary>
        public float counterIntel = 0f;

        public IntelState() { }

        public IntelState(float capability, float counterIntel)
        {
            this.capability = Mathf.Clamp01(capability);
            this.counterIntel = Mathf.Clamp01(counterIntel);
        }
    }
}
