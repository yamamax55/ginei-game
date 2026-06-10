using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// カリスマの日常化＝英雄の死後に組織が存続するか（#812/#814/#816 SHINGEN・本線 SPINE-1 #795）。
    /// 結束(<see cref="cohesion"/>)のうち、制度化(<see cref="institutionalization"/>)分は法・制度が支えて
    /// 継承で残り、残りはリーダー個人のカリスマ(<see cref="leaderCharisma"/>)が支えるため、継承時に後継者の
    /// 力でしか引き継げない。属人すぎる組織は英雄と共に滅ぶ（信玄の轍）／制度化した組織は超えて続く（幕藩）。
    /// 解決は <see cref="SuccessionRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Organization
    {
        public int id;
        public Faction faction;
        public float cohesion = 1f;              // 結束 0..1
        public float institutionalization = 0f;  // 日常化への投資 0..1（法・後継者育成・権限委譲）
        public float leaderCharisma = 1f;        // 現リーダーの個人カリスマ 0..1
        public bool fragmented;                  // 崩壊したか（結束が閾値割れ）

        public Organization() { }

        public Organization(int id, Faction faction, float cohesion = 1f, float institutionalization = 0f, float leaderCharisma = 1f)
        {
            this.id = id;
            this.faction = faction;
            this.cohesion = Mathf.Clamp01(cohesion);
            this.institutionalization = Mathf.Clamp01(institutionalization);
            this.leaderCharisma = Mathf.Clamp01(leaderCharisma);
        }
    }
}
