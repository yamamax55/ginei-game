using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>決裁駆動キャンペーンの4本柱メーター（決裁ゲーム MVP）。いずれかが尽きると敗北。</summary>
    public enum CampaignPillar { 軍事, 民心, 国庫, 統制 }

    /// <summary>
    /// 決裁だけでゲームが進行する縦スライスの<b>マクロメーター状態</b>（決裁ゲーム MVP・純データ）。
    /// 4本柱（<see cref="CampaignPillar"/>＝軍事/民心/国庫/統制）はいずれも 0..1。0 まで尽きると敗北。
    /// 別枠の <see cref="hegemony"/>（覇権・勝利進捗 0..1）が目標に達すると勝利。決裁の裁可で増減し（<see cref="DecisionMeterEffects"/>）、
    /// 裏の戦略シム（支配率）も <see cref="control"/>/<see cref="hegemony"/> を緩やかに引く（ハイブリッド）。
    /// 評価・適用は <see cref="CampaignMeterRules"/> が唯一の窓口。Reigns 型のトレードオフ（どの決裁も無料でない）を保つ。
    /// </summary>
    [Serializable]
    public class CampaignMeters
    {
        [Range(0f, 1f)] public float military = 0.5f;  // 軍事
        [Range(0f, 1f)] public float support = 0.5f;   // 民心
        [Range(0f, 1f)] public float treasury = 0.5f;  // 国庫
        [Range(0f, 1f)] public float control = 0.5f;   // 統制
        [Range(0f, 1f)] public float hegemony = 0f;    // 覇権（勝利進捗）

        public float Get(CampaignPillar p)
        {
            switch (p)
            {
                case CampaignPillar.軍事: return military;
                case CampaignPillar.民心: return support;
                case CampaignPillar.国庫: return treasury;
                default: return control; // 統制
            }
        }

        public void Set(CampaignPillar p, float v)
        {
            v = Mathf.Clamp01(v);
            switch (p)
            {
                case CampaignPillar.軍事: military = v; break;
                case CampaignPillar.民心: support = v; break;
                case CampaignPillar.国庫: treasury = v; break;
                default: control = v; break; // 統制
            }
        }
    }
}
