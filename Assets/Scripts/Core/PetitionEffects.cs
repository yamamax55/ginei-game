using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 陳情の効果レジストリ（MEYASU 世界接続）。<see cref="Petition.effectKey"/>（直列化可・決定論保存）を、
    /// 実際に世界（<see cref="FactionState"/>）を動かす操作へ解決する。執行時の倍率 magnitude(0..1) は
    /// <see cref="WorkflowRules.Execute"/> が返す<b>実効適用量</b>（官僚の骨抜き＝<see cref="PetitionFlowRules.ExecutionFidelity"/>）。
    /// ＝「通っても満額は効かない」を世界に反映する。基準値非破壊（レバー値を直接動かすが、効果は magnitude 倍）。
    /// 既定効果は <see cref="RegisterDefaults"/>。新効果は <see cref="Register"/> で足す（並行新設しない＝WF と共有）。
    /// </summary>
    public static class PetitionEffects
    {
        /// <summary>満額執行での税率変更幅（pt）。実際の変更は ×magnitude。</summary>
        public const float TaxStepFull = 0.1f;

        private static readonly Dictionary<string, Action<FactionState, float>> registry = BuildDefaults();

        private static Dictionary<string, Action<FactionState, float>> BuildDefaults()
        {
            var d = new Dictionary<string, Action<FactionState, float>>();
            // 減税：税率を下げる（民心は上がりやすくなるが税収は減る＝後続で接続）
            d["tax.cut"] = (fs, mag) => fs.taxRate = Mathf.Clamp01(fs.taxRate - TaxStepFull * Mathf.Clamp01(mag));
            // 増税：税率を上げる
            d["tax.hike"] = (fs, mag) => fs.taxRate = Mathf.Clamp01(fs.taxRate + TaxStepFull * Mathf.Clamp01(mag));
            return d;
        }

        /// <summary>効果を登録/上書きする（既存キーは差し替え）。</summary>
        public static void Register(string effectKey, Action<FactionState, float> effect)
        {
            if (string.IsNullOrEmpty(effectKey) || effect == null) return;
            registry[effectKey] = effect;
        }

        /// <summary>キーが登録済みか。</summary>
        public static bool Has(string effectKey)
            => !string.IsNullOrEmpty(effectKey) && registry.ContainsKey(effectKey);

        /// <summary>効果を勢力国家状態へ適用する（magnitude=実効適用量0..1）。未登録/不正は false。</summary>
        public static bool Apply(string effectKey, FactionState fs, float magnitude)
        {
            if (fs == null || string.IsNullOrEmpty(effectKey)) return false;
            if (!registry.TryGetValue(effectKey, out var effect)) return false;
            effect(fs, Mathf.Clamp01(magnitude));
            return true;
        }

        /// <summary>世界（<see cref="CampaignState"/>）の対象勢力へ適用する（effectKey→CampaignState が動く）。</summary>
        public static bool Apply(string effectKey, CampaignState campaign, Faction faction, float magnitude)
        {
            if (campaign == null || campaign.states == null) return false;
            for (int i = 0; i < campaign.states.Count; i++)
            {
                var s = campaign.states[i];
                if (s != null && s.faction == faction)
                    return Apply(effectKey, s, magnitude);
            }
            return false;
        }
    }
}
