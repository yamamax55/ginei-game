using UnityEngine;

namespace Ginei
{
    /// <summary>王朝サイクルの調整係数（#867/#801/#814）。</summary>
    public readonly struct DynastyParams
    {
        /// <summary>徳0のときの腐敗進行/秒。</summary>
        public readonly float corruptionRate;
        /// <summary>正統性がこれ未満で天命を失う（易姓革命の機）。</summary>
        public readonly float mandateThreshold;
        /// <summary>腐敗がこれ以上で改革者/異端者が立つ（ルター #824）。</summary>
        public readonly float reformThreshold;

        public DynastyParams(float corruptionRate, float mandateThreshold, float reformThreshold)
        {
            this.corruptionRate = corruptionRate;
            this.mandateThreshold = mandateThreshold;
            this.reformThreshold = reformThreshold;
        }

        public static DynastyParams Default => new DynastyParams(0.1f, 0.3f, 0.6f);
    }

    /// <summary>
    /// 王朝サイクル＝天命と易姓革命の純ロジック（孔子 #867・転換エンジン #801/#823・日常化 #814）。
    /// 腐敗（制度疲労）が徳の分だけ遅く進み正統性を蝕む。正統性を失えば天命を失い（革命の機）、腐敗が
    /// 高じれば改革者が立つ。<b>Reform</b>（制度更新＝ルターの改革/明治の近代化）で腐敗を下げ正統性を回復、
    /// <b>Revolution</b>（易姓革命）で王朝交代。東西の秩序転換を回す共通エンジン。test-first。
    /// </summary>
    public static class DynastyRules
    {
        /// <summary>時間を dt 進める。腐敗が(1-徳)に比例して進み、その分だけ正統性が下がる。</summary>
        public static void Tick(Regime r, float dt, DynastyParams p)
        {
            if (r == null || dt <= 0f) return;
            float rise = p.corruptionRate * (1f - Mathf.Clamp01(r.virtue)) * dt;
            r.corruption = Mathf.Clamp01(r.corruption + rise);
            r.legitimacy = Mathf.Clamp01(r.legitimacy - rise);
        }

        public static void Tick(Regime r, float dt) => Tick(r, dt, DynastyParams.Default);

        /// <summary>天命を失ったか（正統性が閾値割れ＝易姓革命の機が熟す）。</summary>
        public static bool MandateLost(Regime r, DynastyParams p) => r != null && r.legitimacy < p.mandateThreshold;
        public static bool MandateLost(Regime r) => MandateLost(r, DynastyParams.Default);

        /// <summary>改革者/異端者が立つか（腐敗が閾値超え＝制度疲労への破壊的更新 ルター #824）。</summary>
        public static bool ReformerArises(Regime r, DynastyParams p) => r != null && r.corruption >= p.reformThreshold;
        public static bool ReformerArises(Regime r) => ReformerArises(r, DynastyParams.Default);

        /// <summary>制度更新（ルターの改革/明治の近代化 #806）：腐敗を下げ正統性を回復する。</summary>
        public static void Reform(Regime r, float amount)
        {
            if (r == null) return;
            float a = Mathf.Abs(amount);
            r.corruption = Mathf.Max(0f, r.corruption - a);
            r.legitimacy = Mathf.Clamp01(r.legitimacy + a);
        }

        /// <summary>易姓革命＝王朝交代。正統性 max・腐敗 0・新たな徳で再起動（天命が新王朝へ移る）。</summary>
        public static void Revolution(Regime r, Faction newFaction, float newVirtue)
        {
            if (r == null) return;
            r.faction = newFaction;
            r.legitimacy = 1f;
            r.corruption = 0f;
            r.virtue = Mathf.Clamp01(newVirtue);
        }
    }
}
