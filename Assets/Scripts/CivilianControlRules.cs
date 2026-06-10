using UnityEngine;

namespace Ginei
{
    /// <summary>文民統制の型（軍政関係＝軍と政府のどちらが上か。政体アーキタイプ #117 が決める）。</summary>
    public enum CivilianControlType
    {
        文民統制,   // 民主派：文民政府が軍を任免・軍人の政治兼任は原則不可
        君主統帥,   // 王党派：君主（元首）が統帥権
        党軍,       // 共産：党が銃を握る（政治将校 #17 が監督）
        軍部優位,   // 軍閥：軍が政府を支配（文民統制の不在）
        未分化      // 首長制：軍事＝政治指導者が同一（分化前）
    }

    /// <summary>
    /// 軍政関係（文民統制）の純ロジック（GOV-4 #145・唯一の窓口）。政体アーキタイプ（#117）ごとの
    /// 文民統制型から、<b>軍人の政治役職兼任可否・軍人事を誰が握るか・役職の既定資格・軍のクーデターリスク</b>を導く。
    /// #141 の積み残し「軍人の文民役職兼任可否」をここで裁定する。型未設定なら従来動作（制約・クーデター無し）。test-first。
    /// </summary>
    public static class CivilianControlRules
    {
        /// <summary>クーデター/独走判定の調整値。</summary>
        public readonly struct ControlParams
        {
            /// <summary>このリスク以上でクーデター/命令拒否が発火する閾値（0..1）。</summary>
            public readonly float coupThreshold;

            public ControlParams(float coupThreshold)
            {
                this.coupThreshold = Mathf.Clamp01(coupThreshold);
            }

            /// <summary>既定＝0.6 以上で発火。</summary>
            public static ControlParams Default => new ControlParams(0.6f);
        }

        /// <summary>軍人が政治（非軍事）役職を兼任できるか（軍部優位・未分化のみ可）。</summary>
        public static bool MilitaryMayHoldPoliticalOffice(CivilianControlType t)
            => t == CivilianControlType.軍部優位 || t == CivilianControlType.未分化;

        /// <summary>軍人事（提督の任免）を文民/上位が握るか（軍が自前で決めない＝true）。</summary>
        public static bool CiviliansAppointMilitary(CivilianControlType t)
            => t != CivilianControlType.軍部優位 && t != CivilianControlType.未分化;

        /// <summary>役職の既定「軍人専用」（軍事所掌は軍人専用。未分化は区別なし）。</summary>
        public static bool DefaultMilitaryOnly(OfficeDomain domain, CivilianControlType t)
            => domain == OfficeDomain.軍事 && t != CivilianControlType.未分化;

        /// <summary>役職の既定「文民専用」（非軍事の政治職は文民専用。軍部優位/未分化は軍人も占めるので不問）。</summary>
        public static bool DefaultCivilianOnly(OfficeDomain domain, CivilianControlType t)
        {
            if (MilitaryMayHoldPoliticalOffice(t)) return false; // 軍人が政治職も占める
            return domain == OfficeDomain.内政 || domain == OfficeDomain.外交 || domain == OfficeDomain.財政;
        }

        /// <summary>政体ごとのクーデター素地（軍部優位ほど高く、文民統制/党軍は低い）。</summary>
        private static float BaseCoupProneness(CivilianControlType t)
        {
            switch (t)
            {
                case CivilianControlType.軍部優位: return 0.40f;
                case CivilianControlType.未分化:   return 0.30f;
                case CivilianControlType.君主統帥: return 0.15f;
                case CivilianControlType.文民統制: return 0.10f;
                case CivilianControlType.党軍:     return 0.10f;
                default: return 0.10f;
            }
        }

        /// <summary>
        /// 軍の独走・クーデターのリスク（0..1）。統制が弱い（controlStrength↓）・支持が低い（support↓・#113）・
        /// 敗戦直後（recentDefeat）ほど上がる。統制が強いほど素地を抑え込む。係数は #106 パイプライン想定。
        /// </summary>
        public static float CoupRisk(CivilianControlType t, float controlStrength, float support, bool recentDefeat)
        {
            float strength = Mathf.Clamp01(controlStrength);
            float sup = Mathf.Clamp01(support);
            float drivers = BaseCoupProneness(t) + (1f - sup) * 0.3f + (recentDefeat ? 0.2f : 0f);
            float suppression = 1f - strength * 0.8f; // 統制が強いほどリスクを圧縮
            return Mathf.Clamp01(drivers * suppression);
        }

        /// <summary>クーデター/命令拒否が発火するか（リスクが閾値以上）。</summary>
        public static bool WouldCoup(CivilianControlType t, float controlStrength, float support, bool recentDefeat, ControlParams prm)
            => CoupRisk(t, controlStrength, support, recentDefeat) >= prm.coupThreshold;

        /// <summary>既定パラメータ版。</summary>
        public static bool WouldCoup(CivilianControlType t, float controlStrength, float support, bool recentDefeat)
            => WouldCoup(t, controlStrength, support, recentDefeat, ControlParams.Default);
    }
}
