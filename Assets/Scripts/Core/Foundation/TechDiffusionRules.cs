using UnityEngine;

namespace Ginei
{
    /// <summary>軍事技術の勢力間拡散・技術封鎖の調整係数。</summary>
    public readonly struct TechDiffusionParams
    {
        /// <summary>技術封鎖で絞れる拡散の上限割合（完全遮断は不可能＝独占は時限、の保証）。</summary>
        public const float MaxBlockadeCap = 0.9f;

        /// <summary>基準拡散速度（格差1・接触1・封鎖0のとき per dt）。</summary>
        public readonly float baseDiffusionRate;
        /// <summary>技術封鎖1のとき拡散を絞れる最大割合（0..MaxBlockadeCap＝1未満を強制）。</summary>
        public readonly float maxBlockade;
        /// <summary>スパイ窃取で奪える技術価値の割合（スパイ網と標的価値の積に掛ける）。</summary>
        public readonly float espionageFactor;
        /// <summary>リバースエンジニアリングで得られる技術価値の割合（自前基盤に比例）。</summary>
        public readonly float reverseFactor;
        /// <summary>亡命技術者が持ち込む技術価値の割合（亡命者数×専門性に掛ける）。</summary>
        public readonly float defectorFactor;
        /// <summary>同盟移転で渡る技術価値の割合（同盟強度×共有意思に掛ける）。</summary>
        public readonly float allyFactor;

        public TechDiffusionParams(float baseDiffusionRate, float maxBlockade, float espionageFactor,
                                   float reverseFactor, float defectorFactor, float allyFactor)
        {
            this.baseDiffusionRate = Mathf.Max(0f, baseDiffusionRate);
            // 1.0 を許すと「完全封鎖＝永久独占」になり設計思想（接触があれば必ず漏れる）が崩れるため上限を切る。
            this.maxBlockade = Mathf.Clamp(maxBlockade, 0f, MaxBlockadeCap);
            this.espionageFactor = Mathf.Clamp01(espionageFactor);
            this.reverseFactor = Mathf.Clamp01(reverseFactor);
            this.defectorFactor = Mathf.Clamp01(defectorFactor);
            this.allyFactor = Mathf.Clamp01(allyFactor);
        }

        /// <summary>既定＝基準拡散0.12・封鎖最大遮断0.7・スパイ0.5・リバース0.6・亡命0.7・同盟0.8。</summary>
        public static TechDiffusionParams Default => new TechDiffusionParams(0.12f, 0.7f, 0.5f, 0.6f, 0.7f, 0.8f);
    }

    /// <summary>
    /// 軍事技術拡散（#1377・動員）の純ロジック。軍事技術はスパイによる窃取・捕獲兵器のリバース
    /// エンジニアリング・亡命技術者の持ち込み・同盟内移転などで勢力間を伝播し、先進勢力の技術が
    /// 後発勢力へ漏れる。技術封鎖（輸出規制・機密保持）でこれを遅らせられるが、接触がある限り
    /// 完全には止められず（封鎖の遮断率に上限）、後発勢力は伝播した技術で先進勢力に追いつく
    /// （後発性の利益）。技術独占は漏出で時限的に崩れる。
    /// 分担：<see cref="InnovationDiffusionRules"/>（汎用の技術伝播＝国から国へ漏れる面）とは別の、
    /// 軍事技術に特化した勢力間R&D伝播と技術封鎖。<see cref="ResearchRules"/>（自前研究の産出）、
    /// <see cref="EspionageRules"/>（スパイ網の運用）、<see cref="TechBearerRules"/>（技術は人に宿る）
    /// と接続して使う想定。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TechDiffusionRules
    {
        /// <summary>
        /// 軍事技術が伝播する速度（per dt）。技術格差 techGap(0..1) × 接触 contactLevel(0..1) ×
        /// （1−技術封鎖の遮断率）で決まる。格差があり接触があるほど漏れ、技術封鎖が遅らせる。
        /// 格差ゼロか接触ゼロなら0、両方あれば封鎖全力でも必ず正＝独占は時限。
        /// </summary>
        public static float DiffusionRate(float techGap, float contactLevel, float blockadeStrength, TechDiffusionParams p)
        {
            float gap = Mathf.Clamp01(techGap);
            float contact = Mathf.Clamp01(contactLevel);
            float pass = 1f - p.maxBlockade * Mathf.Clamp01(blockadeStrength); // 封鎖をすり抜ける割合（常に正）
            return p.baseDiffusionRate * gap * contact * pass;
        }

        public static float DiffusionRate(float techGap, float contactLevel, float blockadeStrength)
            => DiffusionRate(techGap, contactLevel, blockadeStrength, TechDiffusionParams.Default);

        /// <summary>
        /// スパイによる技術窃取量（0..targetTechValue）。スパイ網の浸透 spyNetwork(0..1) と標的技術の
        /// 価値 targetTechValue(0..1) の積に比例＝深く入り込み価値の高い技術を狙うほど多く奪える。
        /// スパイ網が無ければ0。<see cref="EspionageRules"/>（諜報網の構築）と接続して使う。
        /// </summary>
        public static float EspionageTransfer(float spyNetwork, float targetTechValue, TechDiffusionParams p)
        {
            float spy = Mathf.Clamp01(spyNetwork);
            float value = Mathf.Clamp01(targetTechValue);
            return spy * value * p.espionageFactor;
        }

        public static float EspionageTransfer(float spyNetwork, float targetTechValue)
            => EspionageTransfer(spyNetwork, targetTechValue, TechDiffusionParams.Default);

        /// <summary>
        /// 捕獲兵器のリバースエンジニアリングで得る技術価値（0..capturedWeapons）。捕獲量
        /// capturedWeapons(0..1) × 自前の技術基盤 ownTechBase(0..1) に比例＝基盤が高いほど解析できる。
        /// 基盤がゼロなら解析できず宝の持ち腐れ（捕獲しても理解できない）。
        /// </summary>
        public static float ReverseEngineering(float capturedWeapons, float ownTechBase, TechDiffusionParams p)
        {
            float captured = Mathf.Clamp01(capturedWeapons);
            float baseline = Mathf.Clamp01(ownTechBase);
            return captured * baseline * p.reverseFactor;
        }

        public static float ReverseEngineering(float capturedWeapons, float ownTechBase)
            => ReverseEngineering(capturedWeapons, ownTechBase, TechDiffusionParams.Default);

        /// <summary>
        /// 亡命技術者が持ち込む技術価値（0..1）。亡命者の規模 defectingEngineers(0..1) × その専門性
        /// expertise(0..1) に比例＝多数の・優秀な技術者が亡命するほど技術が来る。どちらかがゼロなら0
        /// （頭数だけでも専門性だけでも技術は来ない）。<see cref="TechBearerRules"/> と接続。
        /// </summary>
        public static float DefectorTransfer(float defectingEngineers, float expertise, TechDiffusionParams p)
        {
            float defectors = Mathf.Clamp01(defectingEngineers);
            float skill = Mathf.Clamp01(expertise);
            return defectors * skill * p.defectorFactor;
        }

        public static float DefectorTransfer(float defectingEngineers, float expertise)
            => DefectorTransfer(defectingEngineers, expertise, TechDiffusionParams.Default);

        /// <summary>
        /// 同盟内の技術移転量（0..1）。同盟の強度 allianceStrength(0..1) × 共有意思 willingnessToShare(0..1)
        /// に比例＝固い同盟ほど・共有する気があるほど技術が渡る。同盟が無いか共有意思が無ければ0。
        /// <see cref="ForeignAdvisorRules"/>（外国顧問による移転）と接続して使う。
        /// </summary>
        public static float AllyTransfer(float allianceStrength, float willingnessToShare, TechDiffusionParams p)
        {
            float ally = Mathf.Clamp01(allianceStrength);
            float willing = Mathf.Clamp01(willingnessToShare);
            return ally * willing * p.allyFactor;
        }

        public static float AllyTransfer(float allianceStrength, float willingnessToShare)
            => AllyTransfer(allianceStrength, willingnessToShare, TechDiffusionParams.Default);

        /// <summary>
        /// 技術封鎖が拡散を遅らせる効果（遮断率 0..maxBlockade）。輸出規制 exportControl(0..1) と
        /// 機密保持 secrecy(0..1) を合成して遮断率を出す＝両方を厚くするほど漏れにくいが、上限
        /// maxBlockade を超えられない＝完全には止められない（接触があれば必ず漏れる）。
        /// </summary>
        public static float TechBlockadeEffect(float exportControl, float secrecy, TechDiffusionParams p)
        {
            float control = Mathf.Clamp01(exportControl);
            float sec = Mathf.Clamp01(secrecy);
            // 二経路の独立な漏れ止め＝両方すり抜けた残りを遮断率とみなし、上限でクランプ。
            float combined = 1f - (1f - control) * (1f - sec);
            return Mathf.Min(p.maxBlockade, combined);
        }

        public static float TechBlockadeEffect(float exportControl, float secrecy)
            => TechBlockadeEffect(exportControl, secrecy, TechDiffusionParams.Default);

        /// <summary>
        /// 後発勢力が伝播した技術で先進勢力に追いつく1tickの格差縮小量。技術格差 techGap(0..1) ×
        /// 拡散速度 diffusionRate × dt＝格差が大きいほど一歩が大きい後発者利益。負の入力は0でクランプし、
        /// 縮小量は元の格差を超えない（追い越しはしない＝伝播は先進水準まで）。
        /// </summary>
        public static float CatchUpAcceleration(float techGap, float diffusionRate, float dt)
        {
            float gap = Mathf.Clamp01(techGap);
            float rate = Mathf.Max(0f, diffusionRate);
            float closing = gap * rate * Mathf.Max(0f, dt);
            return Mathf.Min(gap, closing); // 格差を超えて縮められない（並走で止まる）
        }

        /// <summary>
        /// 技術独占が漏出で崩れつつあるか（拡散速度が閾値を超えたか）。拡散速度 diffusionRate が
        /// threshold 以上なら独占は崩壊しつつある＝後発が追いつき始めている。封鎖を厚くして拡散速度を
        /// 閾値未満に抑えれば独占を維持できるが、接触がある限り完全には止められない。
        /// </summary>
        public static bool IsTechMonopolyEroding(float diffusionRate, float threshold)
        {
            return Mathf.Max(0f, diffusionRate) >= Mathf.Max(0f, threshold);
        }
    }
}
