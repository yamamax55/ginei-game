using UnityEngine;

namespace Ginei
{
    /// <summary>1ステップの伝播結果（MEYASU-3 #1299）。通過＝次の階へ／握り潰し＝強い抵抗で却下／黙殺＝無視。</summary>
    public enum PetitionStep { 通過, 握り潰し, 黙殺 }

    /// <summary>建白の伝播・執行の調整係数（MEYASU-3 #1299）。</summary>
    public readonly struct PetitionFlowParams
    {
        /// <summary>失敗時、摩擦がこれ以上なら握り潰し（強い抵抗）、未満なら黙殺（無視）。</summary>
        public readonly float buryThreshold;
        /// <summary>通過時、摩擦がこれ以上なら歪んで伝播する。</summary>
        public readonly float distortionThreshold;
        /// <summary>黙殺が再浮上する基礎確率（正しさが判明した場合）。</summary>
        public readonly float resurfaceChance;
        /// <summary>官僚の基礎執行忠実度（摩擦0のときの上限）。</summary>
        public readonly float baseFidelity;

        public PetitionFlowParams(float buryThreshold, float distortionThreshold, float resurfaceChance, float baseFidelity)
        {
            this.buryThreshold = buryThreshold;
            this.distortionThreshold = distortionThreshold;
            this.resurfaceChance = resurfaceChance;
            this.baseFidelity = baseFidelity;
        }

        public static PetitionFlowParams Default => new PetitionFlowParams(0.5f, 0.4f, 0.3f, 1f);
    }

    /// <summary>
    /// 建白の伝播と官僚（執行の壁）の唯一の窓口（MEYASU-3 #1299）。投書は自動実行されない＝多エージェント官僚機構を
    /// 通らないと上に行かず、大半は勝手に死ぬ（内生的スロットル）。摩擦は<b>二段</b>（プリンシパル・エージェント問題）：
    /// ①権力者が取り上げるか＝<see cref="SurvivalChance"/>（箱の信認 <see cref="CredibilityRules.Heed"/> × 省益/閥の摩擦 × 正統性）→
    /// <see cref="Step"/> で 通過/握り潰し/黙殺 ②官僚が忠実に執行するか＝<see cref="ExecutionFidelity"/>（省益/現状維持で骨抜き
    /// ＝「通ったのに効かない」）。生存・伝播・執行は Core（安いロール・決定論）、文面の歪み・口調は LLM 層。test-first。
    /// </summary>
    public static class PetitionFlowRules
    {
        /// <summary>次の階へ通る確率＝箱の信認(heed) × (1-摩擦) × 正統性係数(0.5〜1.0)。0..1。</summary>
        public static float SurvivalChance(float heed, float friction, float legitimacy)
        {
            float h = Mathf.Clamp01(heed);
            float f = Mathf.Clamp01(friction);
            float l = Mathf.Clamp01(legitimacy);
            return Mathf.Clamp01(h * (1f - f) * (0.5f + 0.5f * l));
        }

        /// <summary>
        /// 1階ぶん伝播させる（決定論 roll∈[0,1)）。通過なら status=伝播中・中継者を hops に記録・高摩擦なら歪む。
        /// 失敗なら摩擦の強さで握り潰し（却下）か黙殺に分岐。
        /// </summary>
        public static PetitionStep Step(Petition pet, float heed, float friction, float legitimacy, float roll, PetitionFlowParams prm)
        {
            if (pet == null) return PetitionStep.黙殺;
            float survival = SurvivalChance(heed, friction, legitimacy);
            float f = Mathf.Clamp01(friction);

            if (roll < survival)
            {
                pet.status = PetitionStatus.伝播中;
                if (pet.carrierId != 0) pet.hops.Add(pet.carrierId);
                if (f >= prm.distortionThreshold) pet.distorted = true;
                return PetitionStep.通過;
            }

            if (f >= prm.buryThreshold)
            {
                pet.status = PetitionStatus.却下; // 強い抵抗で握り潰される
                return PetitionStep.握り潰し;
            }

            pet.status = PetitionStatus.黙殺; // ただ無視される（後に再浮上しうる）
            return PetitionStep.黙殺;
        }

        public static PetitionStep Step(Petition pet, float heed, float friction, float legitimacy, float roll)
            => Step(pet, heed, friction, legitimacy, roll, PetitionFlowParams.Default);

        /// <summary>官僚機構を抜け、権力者の決裁待ちへ（伝播中/起案/再浮上から）。</summary>
        public static void MarkAwaitingDecision(Petition pet)
        {
            if (pet == null) return;
            if (pet.status == PetitionStatus.伝播中 || pet.status == PetitionStatus.起案 || pet.status == PetitionStatus.再浮上)
                pet.status = PetitionStatus.決裁待ち;
        }

        /// <summary>黙殺された建白が後に「正しかった」と判明し再浮上するか（決定論 roll）。再浮上なら status=再浮上。</summary>
        public static bool Resurface(Petition pet, float roll, PetitionFlowParams prm)
        {
            if (pet == null || pet.status != PetitionStatus.黙殺 || !pet.vindicated) return false;
            if (roll < prm.resurfaceChance)
            {
                pet.status = PetitionStatus.再浮上;
                return true;
            }
            return false;
        }

        public static bool Resurface(Petition pet, float roll) => Resurface(pet, roll, PetitionFlowParams.Default);

        /// <summary>
        /// 官僚の執行忠実度（0..1）。省益/現状維持の摩擦で骨抜きになる＝1未満なら「通ったのに効かない」。
        /// 効果の実適用量はこの係数を掛けて算出する（実効値パターン・基準値非破壊）。
        /// </summary>
        public static float ExecutionFidelity(float friction, PetitionFlowParams prm)
            => Mathf.Clamp01(prm.baseFidelity * (1f - Mathf.Clamp01(friction)));

        public static float ExecutionFidelity(float friction) => ExecutionFidelity(friction, PetitionFlowParams.Default);
    }
}
