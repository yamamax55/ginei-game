using UnityEngine;

namespace Ginei
{
    /// <summary>工法保持者（ネームド技術者）に宿る技術の調整係数。</summary>
    public readonly struct TechBearerParams
    {
        /// <summary>1人あたりの保持寄与（保持者が増えるほど冗長＝失われにくい）。</summary>
        public readonly float perBearerRetention;
        /// <summary>文書化が保持度へ与える重み（暗黙知を形式知へ移すと人依存が薄れる）。</summary>
        public readonly float codificationWeight;
        /// <summary>引き抜きで奪える技術価値の割合（技量に比例＝凡人を奪っても技術は来ない）。</summary>
        public readonly float poachingFactor;
        /// <summary>亡命/移籍で渡る技術価値の最大割合（低忠誠＋好条件で最大化）。</summary>
        public readonly float defectionFactor;
        /// <summary>徒弟への伝承速度（師の技量×dt の係数）。</summary>
        public readonly float apprenticeshipRate;
        /// <summary>文書化の進行速度（文書化努力×dt の係数）。</summary>
        public readonly float codificationRate;

        public TechBearerParams(float perBearerRetention, float codificationWeight, float poachingFactor,
                                float defectionFactor, float apprenticeshipRate, float codificationRate)
        {
            this.perBearerRetention = Mathf.Max(0f, perBearerRetention);
            this.codificationWeight = Mathf.Clamp01(codificationWeight);
            this.poachingFactor = Mathf.Clamp01(poachingFactor);
            this.defectionFactor = Mathf.Clamp01(defectionFactor);
            this.apprenticeshipRate = Mathf.Max(0f, apprenticeshipRate);
            this.codificationRate = Mathf.Max(0f, codificationRate);
        }

        /// <summary>既定＝保持寄与0.4・文書化重み0.5・引き抜き0.8・亡命0.7・伝承速度0.5・文書化速度0.3。</summary>
        public static TechBearerParams Default => new TechBearerParams(0.4f, 0.5f, 0.8f, 0.7f, 0.5f, 0.3f);
    }

    /// <summary>
    /// 技術は人に宿る（#1092・大聖堂の石工＝Pillars of the Earth）の純ロジック。先進技術は文書でなく
    /// ネームド技術者の頭の中にあり、最後の名工が死ねば工法も死ぬ。保持者が多く文書化されているほど
    /// 失われにくく（冗長化）、1人の頭にしかない技術は脆い。引き抜きは技術者ごと技術を奪い、低忠誠＋
    /// 敵の好条件は亡命/移籍で技術を敵へ渡す。生きているうちに弟子へ伝承し（<see cref="MentorshipRules"/>
    /// と接続）暗黙知を文書化すれば（人依存からの脱却）喪失リスクを下げられる。
    /// 分担：<see cref="InnovationDiffusionRules"/> は国レベルの拡散（接触で漏れる面）、こちらは「人という
    /// 乗り物」で技術が動く点を扱う。<see cref="ResearchRules"/> は自前研究の産出、
    /// <see cref="CareerPipelineRules"/> はテクノクラートの出自経路。乱数なし・決定論（必要なら roll 引数）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TechBearerRules
    {
        /// <summary>
        /// 技術の保持度（0..1）。保持者数×1人あたり寄与＋文書化の寄与を1でクランプ。保持者0かつ
        /// 文書化0なら0＝失伝。1人の頭にしかない技術は文書化が無ければ脆い。
        /// </summary>
        public static float TechRetention(int bearerCount, float codification, TechBearerParams p)
        {
            int n = Mathf.Max(0, bearerCount);
            float doc = Mathf.Clamp01(codification);
            float human = n * p.perBearerRetention;        // 頭数による冗長性
            float written = doc * p.codificationWeight;     // 文書化による下支え
            return Mathf.Clamp01(human + written);
        }

        public static float TechRetention(int bearerCount, float codification)
            => TechRetention(bearerCount, codification, TechBearerParams.Default);

        /// <summary>
        /// 保持者1人の死による技術喪失量（0..techValue）。死で頭数が1減ったぶん保持度が落ち、その
        /// 低下幅×技術価値が失われる。最後の1人（保持者1・文書化0）の死は工法を消し最大の喪失＝
        /// 「最後の名工が死ねば工法も死ぬ」。文書化や予備の保持者がいれば緩和される。
        /// </summary>
        public static float LossOnDeath(float techValue, int bearerCount, float codification, TechBearerParams p)
        {
            float value = Mathf.Max(0f, techValue);
            int n = Mathf.Max(0, bearerCount);
            if (n <= 0) return 0f; // 既に保持者がいない＝これ以上失うものはない
            float before = TechRetention(n, codification, p);
            float after = TechRetention(n - 1, codification, p);
            return Mathf.Max(0f, before - after) * value;
        }

        public static float LossOnDeath(float techValue, int bearerCount, float codification)
            => LossOnDeath(techValue, bearerCount, codification, TechBearerParams.Default);

        /// <summary>
        /// 引き抜きで手に入る技術価値。敵の技術者を奪えば技術ごと来る＝技量(0..1)に比例。技量0の
        /// 凡人を引き抜いても技術は来ない（頭の中身が空）。
        /// </summary>
        public static float PoachingValue(float techValue, float bearerSkill, TechBearerParams p)
        {
            float value = Mathf.Max(0f, techValue);
            float skill = Mathf.Clamp01(bearerSkill);
            return value * skill * p.poachingFactor;
        }

        public static float PoachingValue(float techValue, float bearerSkill)
            => PoachingValue(techValue, bearerSkill, TechBearerParams.Default);

        /// <summary>
        /// 亡命/移籍で敵へ渡る技術価値。低忠誠（1-loyalty）と敵の好条件(0..1)の積に比例＝忠誠が高ければ
        /// 好条件でも動かず、好条件が無ければ低忠誠でも動かない。両方そろうと技術者が技術ごと敵へ。
        /// </summary>
        public static float DefectionTransfer(float techValue, float bearerLoyalty, float enemyOffer, TechBearerParams p)
        {
            float value = Mathf.Max(0f, techValue);
            float disaffection = 1f - Mathf.Clamp01(bearerLoyalty); // 低忠誠ほど大
            float offer = Mathf.Clamp01(enemyOffer);
            return value * disaffection * offer * p.defectionFactor;
        }

        public static float DefectionTransfer(float techValue, float bearerLoyalty, float enemyOffer)
            => DefectionTransfer(techValue, bearerLoyalty, enemyOffer, TechBearerParams.Default);

        /// <summary>
        /// 徒弟への伝承の1tick後の進捗（0..1）。師が生きているうちに弟子へ移せば保持者が増え冗長化する。
        /// 伝承速度は師の技量に比例（名工ほどよく教えられる）＝師を超えはしない（技量でクランプ）。
        /// <see cref="MentorshipRules"/> の関係性ボーナスと接続して使う想定。
        /// </summary>
        public static float ApprenticeshipTick(float masterSkill, float apprenticeProgress, float dt, TechBearerParams p)
        {
            float master = Mathf.Clamp01(masterSkill);
            float progress = Mathf.Clamp01(apprenticeProgress);
            if (progress >= master) return progress; // 師の水準に達した＝これ以上は独り立ちが要る
            float gain = master * p.apprenticeshipRate * Mathf.Max(0f, dt);
            return Mathf.Min(master, progress + gain);
        }

        public static float ApprenticeshipTick(float masterSkill, float apprenticeProgress, float dt)
            => ApprenticeshipTick(masterSkill, apprenticeProgress, dt, TechBearerParams.Default);

        /// <summary>
        /// 文書化の1tick後の度合い（0..1）。文書化努力(0..1)に比例して暗黙知を形式知へ移す＝人依存からの
        /// 脱却。完全文書化(1)に漸近し、努力ゼロでは進まない（誰かが書き留めねば形式知にならない）。
        /// </summary>
        public static float CodificationTick(float codification, float documentationEffort, float dt, TechBearerParams p)
        {
            float doc = Mathf.Clamp01(codification);
            float effort = Mathf.Clamp01(documentationEffort);
            float gain = (1f - doc) * effort * p.codificationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(doc + gain);
        }

        public static float CodificationTick(float codification, float documentationEffort, float dt)
            => CodificationTick(codification, documentationEffort, dt, TechBearerParams.Default);
    }
}
