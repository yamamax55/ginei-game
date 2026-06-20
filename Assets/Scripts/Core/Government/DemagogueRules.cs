using UnityEngine;

namespace Ginei
{
    /// <summary>扇動政治家の調整係数（トリューニヒト型）。</summary>
    public readonly struct DemagogueParams
    {
        /// <summary>雄弁が支持に換わる基礎係数。</summary>
        public readonly float eloquenceScale;
        /// <summary>公衆の恐怖が扇動の燃料になる増幅率（怖がる群衆ほど扇動が効く）。</summary>
        public readonly float fearAmplifier;
        /// <summary>スケープゴート（敵作り）が支持に上乗せする幅。</summary>
        public readonly float scapegoatBonus;
        /// <summary>実績の空洞が統治を蝕む速度（per dt・扇動家の地位1のとき）。</summary>
        public readonly float governanceErosionRate;

        public DemagogueParams(float eloquenceScale, float fearAmplifier, float scapegoatBonus, float governanceErosionRate)
        {
            this.eloquenceScale = Mathf.Max(0f, eloquenceScale);
            this.fearAmplifier = Mathf.Max(0f, fearAmplifier);
            this.scapegoatBonus = Mathf.Max(0f, scapegoatBonus);
            this.governanceErosionRate = Mathf.Max(0f, governanceErosionRate);
        }

        /// <summary>既定＝雄弁0.4・恐怖増幅1.0・敵作り0.2・統治浸食0.01。</summary>
        public static DemagogueParams Default => new DemagogueParams(0.4f, 1f, 0.2f, 0.01f);
    }

    /// <summary>
    /// 扇動政治家の純ロジック（トリューニヒト型＝有能な無能の生存力）。扇動の訴求力は
    /// 「雄弁×（1＋公衆の恐怖）＋敵作り」＝**怖がる群衆と憎める標的がいる限り扇動は効く**。
    /// 危機では責任ある地位から音もなく消えて失点を避け（責任回避＝説明責任の低い体制ほど成功する）、
    /// 嵐が過ぎれば戻ってくる。ただし実績は空洞なので、扇動家が要職を占める時間に比例して統治は静かに
    /// 蝕まれる。政党の勢力計算（<see cref="PartyRules"/>）は read-only で参照する想定。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DemagogueRules
    {
        /// <summary>
        /// 扇動の訴求力（0..1）＝雄弁 eloquence(0..1)×係数×（1＋恐怖 publicFear(0..1)×増幅）
        /// ＋スケープゴートの有無 hasScapegoat の上乗せ。平時（恐怖0・標的なし）の扇動家はただの雄弁家。
        /// </summary>
        public static float Appeal(float eloquence, float publicFear, bool hasScapegoat, DemagogueParams p)
        {
            float baseAppeal = Mathf.Clamp01(eloquence) * p.eloquenceScale * (1f + Mathf.Clamp01(publicFear) * p.fearAmplifier);
            if (hasScapegoat) baseAppeal += p.scapegoatBonus;
            return Mathf.Clamp01(baseAppeal);
        }

        public static float Appeal(float eloquence, float publicFear, bool hasScapegoat)
            => Appeal(eloquence, publicFear, hasScapegoat, DemagogueParams.Default);

        /// <summary>
        /// 責任回避の成功率（0..1）＝1−説明責任 accountability(0..1)。制度が緩いほど「あの時いなかった」
        /// が通る。roll∈[0,1) 未満で逃げ切り＝危機の失点を負わない（決定論）。
        /// </summary>
        public static float DodgeChance(float accountability)
        {
            return 1f - Mathf.Clamp01(accountability);
        }

        /// <summary>逃げ切り判定（決定論）。</summary>
        public static bool DodgesResponsibility(float accountability, float roll)
        {
            return roll < DodgeChance(accountability);
        }

        /// <summary>
        /// 危機後の支持残存率（0..1）。逃げ切れば無傷（1.0）、捕まれば危機の深さ crisisSeverity(0..1) の
        /// 分だけ削られる＝説明責任だけが扇動家を殺せる。
        /// </summary>
        public static float SupportRetention(bool dodged, float crisisSeverity)
        {
            if (dodged) return 1f;
            return 1f - Mathf.Clamp01(crisisSeverity);
        }

        /// <summary>
        /// 統治の浸食（per dt）＝扇動家の占める地位の重さ officeWeight(0..1)×浸食率×dt。
        /// 雄弁は政策を作らない＝在任が長いほど行政の質が静かに下がる。
        /// </summary>
        public static float GovernanceErosion(float officeWeight, float dt, DemagogueParams p)
        {
            return Mathf.Clamp01(officeWeight) * p.governanceErosionRate * Mathf.Max(0f, dt);
        }

        public static float GovernanceErosion(float officeWeight, float dt)
            => GovernanceErosion(officeWeight, dt, DemagogueParams.Default);
    }
}
