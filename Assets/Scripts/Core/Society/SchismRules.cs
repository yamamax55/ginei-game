using UnityEngine;

namespace Ginei
{
    /// <summary>教団分裂（シスム）の調整係数（宗教・思想組織の分派と内部対立）。</summary>
    public readonly struct SchismParams
    {
        /// <summary>教義対立の鋭さ（解釈の隔たり×硬直の効きを増幅する指数。大きいほど小さな差でも激化）。</summary>
        public readonly float doctrineSharpness;
        /// <summary>指導権争いの飽和の速さ（候補が増えるほど争いが頭打ちになる飽和係数）。</summary>
        public readonly float rivalrySaturation;
        /// <summary>正統性の争奪が拮抗（五分五分）するときに鋭くなる度合い（接戦ほど争奪が深刻）。</summary>
        public readonly float contestSharpness;
        /// <summary>和解が分派を打ち消す効き（共通の核と調停が分裂を鎮める強さ）。</summary>
        public readonly float reconciliationWeight;

        public SchismParams(float doctrineSharpness, float rivalrySaturation,
            float contestSharpness, float reconciliationWeight)
        {
            this.doctrineSharpness = Mathf.Clamp(doctrineSharpness, 0.5f, 3f);
            this.rivalrySaturation = Mathf.Clamp(rivalrySaturation, 0.5f, 8f);
            this.contestSharpness = Mathf.Clamp01(contestSharpness);
            this.reconciliationWeight = Mathf.Clamp01(reconciliationWeight);
        }

        /// <summary>既定＝教義鋭さ1.2・争い飽和3.0・争奪鋭さ0.5・和解効き0.7。</summary>
        public static SchismParams Default =>
            new SchismParams(1.2f, 3f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 教団分裂（シスム）の純ロジック。宗教団体・思想組織が教義解釈の対立と指導権争いから分派を生み、
    /// 正統性を争奪し、組織が弱体化し、和解か決裂かを経て分派が独立する流れを式に出す。
    /// 教義対立（<see cref="DoctrinalDispute"/>）×指導権争い（<see cref="LeadershipRivalry"/>）→分派の形成
    /// （<see cref="FactionFormation"/>）、正統性の争奪（<see cref="LegitimacyContest"/>）、組織の弱体化
    /// （<see cref="OrganizationalWeakening"/>）、和解の見込み（<see cref="ReconciliationChance"/>）から
    /// 分裂の深刻さ（<see cref="SchismSeverity"/>＝分派−和解）と決定的か（<see cref="IsSchismTerminal"/>）を導く。
    /// 全入力クランプ・乱数なし決定論・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SchismRules
    {
        /// <summary>分裂が決定的（修復不能）とみなす既定閾値。</summary>
        public const float DefaultTerminalThreshold = 0.6f;

        /// <summary>
        /// 教義対立＝解釈の隔たり×教義の硬直で生じる対立度（0..1）。
        /// 解釈の隔たりが大きいほど、また教義が硬直しているほど対立が鋭く立ち上がる。
        /// </summary>
        public static float DoctrinalDispute(float interpretationGap, float theologicalRigidity, SchismParams p)
        {
            float gap = Mathf.Clamp01(interpretationGap);
            float rigid = Mathf.Clamp01(theologicalRigidity);
            // 隔たりと硬直の積を鋭さ指数で曲げる（拮抗より両方高いときに跳ねる）
            float core = gap * Mathf.Lerp(0.4f, 1f, rigid);
            return Mathf.Clamp01(Mathf.Pow(core, 1f / p.doctrineSharpness));
        }

        /// <summary>既定 Params 版。</summary>
        public static float DoctrinalDispute(float interpretationGap, float theologicalRigidity) =>
            DoctrinalDispute(interpretationGap, theologicalRigidity, SchismParams.Default);

        /// <summary>
        /// 指導権争い＝後継者候補数×野心で生じる権力闘争（0..1）。
        /// 候補が複数いるほど争いは増すが飽和し、野心が高いほど激しくなる。候補1名以下なら争い無し。
        /// </summary>
        public static float LeadershipRivalry(int claimantCount, float ambition, SchismParams p)
        {
            int n = Mathf.Max(0, claimantCount);
            float amb = Mathf.Clamp01(ambition);
            if (n <= 1) return 0f; // 単独後継なら争いは起きない
            // 競合候補数（=n-1）を飽和でならす：rivals/(rivals+saturation)
            float rivals = n - 1;
            float pressure = rivals / (rivals + p.rivalrySaturation);
            return Mathf.Clamp01(pressure * Mathf.Lerp(0.3f, 1f, amb));
        }

        /// <summary>既定 Params 版。</summary>
        public static float LeadershipRivalry(int claimantCount, float ambition) =>
            LeadershipRivalry(claimantCount, ambition, SchismParams.Default);

        /// <summary>
        /// 分派の形成＝教義対立×指導権争いで分派が生まれる度合い（0..1）。
        /// 教義の対立だけ、または権力争いだけでは割れにくく、両方が揃うと分派が結晶化する（積で効く）。
        /// </summary>
        public static float FactionFormation(float doctrinalDispute, float leadershipRivalry, SchismParams p)
        {
            float doc = Mathf.Clamp01(doctrinalDispute);
            float riv = Mathf.Clamp01(leadershipRivalry);
            // 主因は積（両輪が揃うと割れる）＋単独要因の弱い底上げ
            float both = doc * riv;
            float either = Mathf.Max(doc, riv);
            return Mathf.Clamp01(both * 0.7f + either * 0.3f);
        }

        /// <summary>既定 Params 版。</summary>
        public static float FactionFormation(float doctrinalDispute, float leadershipRivalry) =>
            FactionFormation(doctrinalDispute, leadershipRivalry, SchismParams.Default);

        /// <summary>
        /// 正統性の争奪＝正統の主張×改革派の主張で争われる激しさ（0..1）。
        /// 双方が強く主張し、かつ拮抗（互角）するほど争奪は深刻になる（一方が圧倒すれば決着して鎮まる）。
        /// </summary>
        public static float LegitimacyContest(float orthodoxyClaim, float reformistClaim, SchismParams p)
        {
            float ortho = Mathf.Clamp01(orthodoxyClaim);
            float reform = Mathf.Clamp01(reformistClaim);
            float intensity = ortho * reform;          // 双方強いほど火種が大きい
            float balance = 1f - Mathf.Abs(ortho - reform); // 拮抗度（差0で1、片寄りで小）
            // 接戦ほど鋭くする（contestSharpness で拮抗の効きを混ぜる）
            float weighted = Mathf.Lerp(1f, balance, p.contestSharpness);
            return Mathf.Clamp01(intensity * weighted);
        }

        /// <summary>既定 Params 版。</summary>
        public static float LegitimacyContest(float orthodoxyClaim, float reformistClaim) =>
            LegitimacyContest(orthodoxyClaim, reformistClaim, SchismParams.Default);

        /// <summary>
        /// 組織の弱体化＝分派×資源分散で組織が衰える度合い（0..1）。
        /// 分派が形成され、人員・財・拠点といった資源が割れるほど組織全体が弱る。
        /// </summary>
        public static float OrganizationalWeakening(float factionFormation, float resourceSplit, SchismParams p)
        {
            float faction = Mathf.Clamp01(factionFormation);
            float split = Mathf.Clamp01(resourceSplit);
            // 分派が無ければ資源が割れても弱らない（分派が主因・分散が増幅）
            return Mathf.Clamp01(faction * Mathf.Lerp(0.5f, 1f, split));
        }

        /// <summary>既定 Params 版。</summary>
        public static float OrganizationalWeakening(float factionFormation, float resourceSplit) =>
            OrganizationalWeakening(factionFormation, resourceSplit, SchismParams.Default);

        /// <summary>
        /// 和解の見込み＝共通の核×調停努力で分裂が鎮まる見込み（0..1）。
        /// 共有する信条の核が太く、調停の努力が払われるほど和解しやすい。核が無ければ調停も効きにくい。
        /// </summary>
        public static float ReconciliationChance(float sharedCore, float mediationEffort, SchismParams p)
        {
            float core = Mathf.Clamp01(sharedCore);
            float effort = Mathf.Clamp01(mediationEffort);
            // 共通の核が前提（核が薄いと調停が空回り）＝幾何平均的に効かせる
            return Mathf.Clamp01(Mathf.Sqrt(core * effort));
        }

        /// <summary>既定 Params 版。</summary>
        public static float ReconciliationChance(float sharedCore, float mediationEffort) =>
            ReconciliationChance(sharedCore, mediationEffort, SchismParams.Default);

        /// <summary>
        /// 分裂の深刻さ＝分派の形成−和解の見込みで決まる正味の分裂度（-1..1）。
        /// 正で分裂が進む（分派が和解を上回る）、負で結束へ向かう（和解が分派を上回る）。
        /// </summary>
        public static float SchismSeverity(float factionFormation, float reconciliationChance, SchismParams p)
        {
            float faction = Mathf.Clamp01(factionFormation);
            float reconcile = Mathf.Clamp01(reconciliationChance);
            float severity = faction - reconcile * p.reconciliationWeight;
            return Mathf.Clamp(severity, -1f, 1f);
        }

        /// <summary>既定 Params 版。</summary>
        public static float SchismSeverity(float factionFormation, float reconciliationChance) =>
            SchismSeverity(factionFormation, reconciliationChance, SchismParams.Default);

        /// <summary>
        /// 分裂が決定的（修復不能＝分派の独立）か。深刻さが閾値以上なら決裂し分派が独立する。
        /// </summary>
        public static bool IsSchismTerminal(float schismSeverity, float threshold)
        {
            float sev = Mathf.Clamp(schismSeverity, -1f, 1f);
            float thr = Mathf.Clamp(threshold, -1f, 1f);
            return sev >= thr;
        }

        /// <summary>既定閾値版。</summary>
        public static bool IsSchismTerminal(float schismSeverity) =>
            IsSchismTerminal(schismSeverity, DefaultTerminalThreshold);
    }
}
