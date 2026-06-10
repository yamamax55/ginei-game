using UnityEngine;

namespace Ginei
{
    /// <summary>研究分野（#123-127）。政体（思想）で研究効率が偏る軸。</summary>
    public enum ResearchField { 軍事, 生産, 情報, 社会 }

    /// <summary>研究の調整係数（#123-127・マジックナンバー禁止＝Params＋Default に集約）。</summary>
    public readonly struct ResearchParams
    {
        /// <summary>研究力あたりの基準産出（研究ポイント/戦略秒）。</summary>
        public readonly float outputPerPower;
        /// <summary>得意分野の効率倍率（政体と研究分野が合う）。</summary>
        public readonly float biasMatch;
        /// <summary>不得意分野の効率倍率（政体と研究分野が合わない）。</summary>
        public readonly float biasMismatch;
        /// <summary>中立（政体不明・該当なし）の効率倍率。</summary>
        public readonly float biasNeutral;

        public ResearchParams(float outputPerPower, float biasMatch, float biasMismatch, float biasNeutral)
        {
            this.outputPerPower = outputPerPower;
            this.biasMatch = biasMatch;
            this.biasMismatch = biasMismatch;
            this.biasNeutral = biasNeutral;
        }

        /// <summary>既定係数（産出=1・得意1.3・不得意0.7・中立1.0）。</summary>
        public static ResearchParams Default => new ResearchParams(1f, 1.3f, 0.7f, 1f);
    }

    /// <summary>
    /// 研究ツリーの数値解決（#123-127・純ロジック test-first）。唯一の窓口。
    /// 研究力(researchPower)と生産力(productionFactor＝内政 <see cref="GovernanceRules.OutputFactor"/>)から
    /// 研究産出(<see cref="ResearchOutput"/>)を出し、<see cref="Tick"/> でプロジェクトの進捗を積み、
    /// <see cref="IsComplete"/> で完成判定する。政体（思想）で研究が偏る＝<see cref="IdeologyBias"/>。
    /// 建設マイクロ・通貨経済は持たない（タイクン回避）。調整値は <see cref="ResearchParams"/> に集約。
    /// </summary>
    public static class ResearchRules
    {
        // --- 政体（思想）と得意分野の対応（#123-127・偏りの出所） ---
        // 専制＝軍事に強い、民主＝社会に強い、商業＝生産に強い、技術＝情報に強い（思想文字列で照合）。
        public const string Ideology専制 = "専制";
        public const string Ideology民主 = "民主";
        public const string Ideology商業 = "商業";
        public const string Ideology技術 = "技術";

        /// <summary>
        /// 研究産出（研究ポイント/戦略秒）。研究力×生産力×基準産出。
        /// productionFactor＝内政の安定度比例産出（支配≠即研究）。負値はクランプ。
        /// </summary>
        public static float ResearchOutput(float researchPower, float productionFactor, ResearchParams prm)
        {
            float power = Mathf.Max(0f, researchPower);
            float factor = Mathf.Max(0f, productionFactor);
            return power * factor * prm.outputPerPower;
        }

        /// <summary>既定係数での研究産出。</summary>
        public static float ResearchOutput(float researchPower, float productionFactor)
            => ResearchOutput(researchPower, productionFactor, ResearchParams.Default);

        /// <summary>
        /// 研究を deltaTime 進める。output＝<see cref="ResearchOutput"/> の産出（負値はクランプ）。
        /// 進捗は cost を上限にクランプ（過剰積み増し防止）。null/非正の dt は無視。
        /// </summary>
        public static void Tick(ResearchProject project, float output, float deltaTime)
        {
            if (project == null || deltaTime <= 0f) return;
            float add = Mathf.Max(0f, output) * deltaTime;
            project.progress = Mathf.Min(project.cost, project.progress + add);
        }

        /// <summary>研究が完成したか（進捗が必要研究量に達した）。null は false。</summary>
        public static bool IsComplete(ResearchProject project)
            => project != null && project.cost > 0f && project.progress >= project.cost;

        /// <summary>
        /// 政体（思想文字列）で研究効率が偏る倍率（#123-127）。
        /// 思想と研究分野が得意の組＝<see cref="ResearchParams.biasMatch"/>、不得意の組＝<see cref="ResearchParams.biasMismatch"/>、
        /// 思想不明・該当なし＝<see cref="ResearchParams.biasNeutral"/>（中立）。基準値非破壊（倍率を返すだけ）。
        /// </summary>
        public static float IdeologyBias(ResearchField field, string ideology, ResearchParams prm)
        {
            if (string.IsNullOrEmpty(ideology)) return prm.biasNeutral; // 思想不明＝中立
            ResearchField? favored = FavoredField(ideology);
            if (favored == null) return prm.biasNeutral;                // 該当なし＝中立
            return favored.Value == field ? prm.biasMatch : prm.biasMismatch;
        }

        /// <summary>既定係数での政体偏り倍率。</summary>
        public static float IdeologyBias(ResearchField field, string ideology)
            => IdeologyBias(field, ideology, ResearchParams.Default);

        /// <summary>思想が得意とする研究分野（#123-127・偏りの対応表）。該当なしは null。</summary>
        private static ResearchField? FavoredField(string ideology)
        {
            switch (ideology)
            {
                case Ideology専制: return ResearchField.軍事;
                case Ideology民主: return ResearchField.社会;
                case Ideology商業: return ResearchField.生産;
                case Ideology技術: return ResearchField.情報;
                default:          return null;
            }
        }
    }
}
