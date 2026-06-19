using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 欺瞞計画（偽の作戦・偽の主攻方向・偽の戦力を一貫して見せ、敵に誤った全体像を信じ込ませる＝ボディガード作戦型）の調整値（#欺瞞計画）。
    /// 筋の一貫性・敵の信じやすさ・判断の誤らせ・誤配置・本作戦の秘匿・見破りリスクの各係数を ctor で安全側へクランプ。
    /// </summary>
    public readonly struct DeceptionPlanParams
    {
        /// <summary>一致した信号1本ぶんが筋の一貫性に積み増す寄与（0..1）。</summary>
        public readonly float signalWeight;
        /// <summary>矛盾した漏れ1件が一貫性をどれだけ削るか（綻びは一致より重く効く）。</summary>
        public readonly float leakPenalty;
        /// <summary>敵の信じやすさに占める「先入観への適合」の寄与（0..1。1.0＝先入観だけで決まる）。</summary>
        public readonly float preconceptionWeight;
        /// <summary>欺瞞が本作戦の秘匿に与える注意逸らし効果の係数（欺瞞が敵の眼を引きつけ本物を覆う）。</summary>
        public readonly float distractionScale;
        /// <summary>矛盾漏れと敵の防諜から見破りリスクへの係数。</summary>
        public readonly float exposureScale;

        public DeceptionPlanParams(float signalWeight, float leakPenalty,
            float preconceptionWeight, float distractionScale, float exposureScale)
        {
            this.signalWeight = Mathf.Clamp01(signalWeight);
            this.leakPenalty = Mathf.Clamp(leakPenalty, 0f, 10f);
            this.preconceptionWeight = Mathf.Clamp01(preconceptionWeight);
            this.distractionScale = Mathf.Clamp(distractionScale, 0f, 10f);
            this.exposureScale = Mathf.Clamp(exposureScale, 0f, 10f);
        }

        /// <summary>既定：信号寄与0.2・漏れ罰0.3・先入観寄与0.5・注意逸らし0.5・露見係数1.0。</summary>
        public static DeceptionPlanParams Default => new DeceptionPlanParams(
            DefaultSignalWeight, DefaultLeakPenalty,
            DefaultPreconceptionWeight, DefaultDistractionScale, DefaultExposureScale);

        public const float DefaultSignalWeight = 0.2f;
        public const float DefaultLeakPenalty = 0.3f;
        public const float DefaultPreconceptionWeight = 0.5f;
        public const float DefaultDistractionScale = 0.5f;
        public const float DefaultExposureScale = 1.0f;
    }

    /// <summary>
    /// 欺瞞計画の純ロジック（#欺瞞計画・唯一の窓口）＝<b>偽の作戦・偽の主攻方向・偽の戦力を一貫して見せ、敵に誤った全体像を信じ込ませる（D-Day のボディガード作戦型）</b>。
    /// 一致した信号を重ね矛盾の漏れを抑えるほど筋が通り（<see cref="StoryConsistency"/>）、敵の先入観に沿うほど信じられ（<see cref="Believability"/>）、信じた敵は判断を誤り（<see cref="EnemyMisdirection"/>）、誤った重点へ戦力を割く（<see cref="ResourceMisallocation"/>）。
    /// 欺瞞は敵の眼を引きつけ本作戦を覆い（<see cref="RealOperationSecurity"/>）、矛盾の漏れと敵の防諜で見破られ（<see cref="ExposureRisk"/>）、誤配置の利得から露見リスクを差し引いて欺瞞の正味効果を見て（<see cref="DeceptionPayoff"/>）、欺瞞が信じられたかを判定する（<see cref="IsDeceptionBelieved"/>）。
    /// <b>分担</b>：囮部隊（<see cref="DecoyForceRules"/>＝偽の主攻・囮艦隊で戦力を別方向へ引きつける）／偽装退却（<see cref="FeignedRetreatRules"/>＝局所の引きずり出しの演技）とは別＝こちらは<b>作戦全体の整合した欺瞞計画（偽の全体像を信じ込ませる）</b>。
    /// 心理戦（<see cref="PsyOpsRules"/>＝士気・意志への影響）とも別＝こちらは<b>敵の状況認識（判断・予備配置・防御重点）を誤らせる</b>。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・test-first。
    /// </summary>
    public static class DeceptionPlanRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        /// <summary>誤配置を兵力換算する基準（誤らせ1.0＝敵戦力の全部を誤った重点へ割きうる上限割合の基準）。</summary>
        public const float MisallocationReference = 1f;

        // ── 欺瞞の筋の一貫性 ──────────────────────────────────────

        /// <summary>欺瞞の筋の一貫性（0..1）＝一致した信号で上がり、矛盾した漏れで下がる。</summary>
        public static float StoryConsistency(int coordinatedSignals, int contradictoryLeaks)
            => StoryConsistency(coordinatedSignals, contradictoryLeaks, DeceptionPlanParams.Default);

        /// <summary>一貫性（0..1）＝clamp01(max(0,信号数)×signalWeight − max(0,漏れ数)×leakPenalty)。綻び（漏れ）は一致より重く効く。</summary>
        public static float StoryConsistency(int coordinatedSignals, int contradictoryLeaks, DeceptionPlanParams p)
        {
            float signals = Mathf.Max(0, coordinatedSignals);
            float leaks = Mathf.Max(0, contradictoryLeaks);
            return Mathf.Clamp01(signals * p.signalWeight - leaks * p.leakPenalty);
        }

        // ── 敵が信じる度合い ──────────────────────────────────────

        /// <summary>敵が欺瞞を信じる度合い（0..1）＝筋の一貫性と先入観への適合の混合。先入観に沿うほど信じる。</summary>
        public static float Believability(float storyConsistency, float enemyPreconception)
            => Believability(storyConsistency, enemyPreconception, DeceptionPlanParams.Default);

        /// <summary>信じやすさ（0..1）＝clamp01(lerp(clamp01(一貫性), clamp01(先入観), preconceptionWeight))。整合した筋＋先入観適合の両輪。</summary>
        public static float Believability(float storyConsistency, float enemyPreconception, DeceptionPlanParams p)
        {
            float consistency = Mathf.Clamp01(storyConsistency);
            float precon = Mathf.Clamp01(enemyPreconception);
            return Mathf.Clamp01(Mathf.Lerp(consistency, precon, p.preconceptionWeight));
        }

        // ── 敵の判断を誤らせる度合い ──────────────────────────────

        /// <summary>敵の判断を誤らせる度合い（0..1）＝信じやすさ×欺瞞の大胆さ（規模）。信じた敵ほど大きく誤る。</summary>
        public static float EnemyMisdirection(float believability, float deceptionMagnitude)
        {
            float believe = Mathf.Clamp01(believability);
            float magnitude = Mathf.Clamp01(deceptionMagnitude);
            return Mathf.Clamp01(believe * magnitude);
        }

        // ── 敵の誤配置 ────────────────────────────────────────────

        /// <summary>敵が誤った重点へ割く戦力（絶対戦力）＝誤らせた割合×敵総戦力。誤った全体像のぶんだけ別方向へ配置される。</summary>
        public static float ResourceMisallocation(float enemyMisdirection, float enemyForce)
        {
            float misdirect = Mathf.Clamp01(enemyMisdirection);
            float force = Mathf.Max(0f, enemyForce);
            return misdirect * force;
        }

        // ── 本作戦の秘匿 ──────────────────────────────────────────

        /// <summary>本物の作戦の秘匿（0..1）＝欺瞞による注意逸らしで素の秘匿を底上げ。欺瞞が敵の眼を引きつけ本物を覆う。</summary>
        public static float RealOperationSecurity(float deceptionFocus, float realOpConcealment)
            => RealOperationSecurity(deceptionFocus, realOpConcealment, DeceptionPlanParams.Default);

        /// <summary>秘匿（0..1）＝clamp01(clamp01(秘匿) + clamp01(欺瞞注目度)×distractionScale×(1−clamp01(秘匿)))。残り余地ぶんを欺瞞が埋める（上限1.0）。</summary>
        public static float RealOperationSecurity(float deceptionFocus, float realOpConcealment, DeceptionPlanParams p)
        {
            float baseConceal = Mathf.Clamp01(realOpConcealment);
            float focus = Mathf.Clamp01(deceptionFocus);
            float headroom = 1f - baseConceal;
            return Mathf.Clamp01(baseConceal + focus * p.distractionScale * headroom);
        }

        // ── 見破りリスク ──────────────────────────────────────────

        /// <summary>欺瞞が見破られるリスク（0..1）＝矛盾の漏れ×敵の防諜。綻びが多く敵の眼が利くほど露見する。</summary>
        public static float ExposureRisk(float contradictoryLeaks, float enemyCounterIntel)
            => ExposureRisk(contradictoryLeaks, enemyCounterIntel, DeceptionPlanParams.Default);

        /// <summary>露見リスク（0..1）＝clamp01(clamp01(漏れ度)×clamp01(防諜)×exposureScale)。</summary>
        public static float ExposureRisk(float contradictoryLeaks, float enemyCounterIntel, DeceptionPlanParams p)
        {
            float leaks = Mathf.Clamp01(contradictoryLeaks);
            float counter = Mathf.Clamp01(enemyCounterIntel);
            return Mathf.Clamp01(leaks * counter * p.exposureScale);
        }

        // ── 欺瞞の正味効果 ────────────────────────────────────────

        /// <summary>欺瞞の正味効果（0..1）＝敵の誤配置の利得を露見リスクで割り引いた値。見破られれば効果が消える。</summary>
        public static float DeceptionPayoff(float resourceMisallocation, float exposureRisk)
        {
            float gain = Mathf.Clamp01(resourceMisallocation);
            float risk = Mathf.Clamp01(exposureRisk);
            return Mathf.Clamp01(gain * (1f - risk));
        }

        // ── 欺瞞成功の判定 ────────────────────────────────────────

        /// <summary>欺瞞が信じられたか＝信じやすさが既定閾値以上（既定 `BelievedThreshold`=0.5）。</summary>
        public static bool IsDeceptionBelieved(float believability)
            => IsDeceptionBelieved(believability, BelievedThreshold);

        /// <summary>信じられた（bool）＝clamp01(信じやすさ)≥clamp01(threshold)。</summary>
        public static bool IsDeceptionBelieved(float believability, float threshold)
        {
            float believe = Mathf.Clamp01(believability);
            return believe >= Mathf.Clamp01(threshold);
        }

        /// <summary>欺瞞が信じられたとみなす信じやすさの既定閾値。</summary>
        public const float BelievedThreshold = 0.5f;
    }
}
