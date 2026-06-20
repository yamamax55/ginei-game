using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 決裁駆動キャンペーンのマクロメーター（<see cref="CampaignMeters"/>）の<b>適用・勝敗評価の唯一の窓口</b>（決裁ゲーム MVP・純ロジック）。
    /// 4本柱（軍事/民心/国庫/統制）のいずれかが 0 に尽きれば敗北、覇権が <see cref="HegemonyTarget"/> に達すれば勝利。
    /// 勝利判定を敗北より優先（既存 <see cref="CampaignVictoryRules"/> と同方針）。決定論・test-first。
    /// </summary>
    public static class CampaignMeterRules
    {
        /// <summary>柱がこれ以下なら崩壊（敗北）。</summary>
        public const float DefeatFloor = 0f;
        /// <summary>覇権がこれ以上なら勝利。</summary>
        public const float HegemonyTarget = 1f;

        /// <summary>全メーターを 0..1 にクランプする。</summary>
        public static void Clamp(CampaignMeters m)
        {
            if (m == null) return;
            m.military = Mathf.Clamp01(m.military);
            m.support = Mathf.Clamp01(m.support);
            m.treasury = Mathf.Clamp01(m.treasury);
            m.control = Mathf.Clamp01(m.control);
            m.hegemony = Mathf.Clamp01(m.hegemony);
        }

        /// <summary>増減ベクトルを scale 倍（＝実効適用量0..1）で適用しクランプする。</summary>
        public static void Apply(CampaignMeters m, in MeterDelta d, float scale = 1f)
        {
            if (m == null) return;
            m.military += d.military * scale;
            m.support += d.support * scale;
            m.treasury += d.treasury * scale;
            m.control += d.control * scale;
            m.hegemony += d.hegemony * scale;
            Clamp(m);
        }

        /// <summary>崩壊した柱（&lt;= <see cref="DefeatFloor"/>）があれば true＋その柱を返す。</summary>
        public static bool TryGetCollapsedPillar(CampaignMeters m, out CampaignPillar pillar)
        {
            pillar = CampaignPillar.軍事;
            if (m == null) return false;
            // 軍事→民心→国庫→統制 の順で最初に崩壊した柱を返す
            if (m.military <= DefeatFloor) { pillar = CampaignPillar.軍事; return true; }
            if (m.support <= DefeatFloor) { pillar = CampaignPillar.民心; return true; }
            if (m.treasury <= DefeatFloor) { pillar = CampaignPillar.国庫; return true; }
            if (m.control <= DefeatFloor) { pillar = CampaignPillar.統制; return true; }
            return false;
        }

        /// <summary>勝敗を評価する（継続/勝利/敗北）。reason に決着理由（敗北＝崩壊した柱／勝利＝覇権達成）を返す。</summary>
        public static CampaignOutcome Evaluate(CampaignMeters m, out string reason)
        {
            reason = "";
            if (m == null) return CampaignOutcome.継続;

            // 勝利を敗北より優先（既存 CampaignVictoryRules と同方針）
            if (m.hegemony >= HegemonyTarget)
            {
                reason = "覇権の確立";
                return CampaignOutcome.勝利;
            }
            if (TryGetCollapsedPillar(m, out var pillar))
            {
                reason = $"{pillar}の崩壊";
                return CampaignOutcome.敗北;
            }
            return CampaignOutcome.継続;
        }

        /// <summary>reason を伴わない簡易版。</summary>
        public static CampaignOutcome Evaluate(CampaignMeters m) => Evaluate(m, out _);
    }
}
