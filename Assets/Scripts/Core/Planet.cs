using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星攻城の対象（#131 惑星戦／PB-3〜PB-7）。星系(StarSystem)に紐づく攻城状態を持つ純データ。
    /// 制空権＝<see cref="orbitalDefense"/>（M.A.S.S. の侵食尖塔=ピラー・ドメイン／人類側はアルテミスの首飾り）。
    /// これが健在(>0)な間は艦隊は接近限界で止まり、惑星を獲れない。S-AV(#757)が制空権を段階制圧して
    /// 0 にする＝ドメイン・ダウン。ダウン後に侵略値(<see cref="invasionProgress"/>)を蓄積し、閾値で占領。
    /// 攻略の解決は <see cref="PlanetSiegeRules"/>（static・#208 自動解決の数値モデル）が唯一の窓口。
    /// コロニー・要塞も同じ枠で扱える（PB-6・規模で各値をスケール）。
    /// </summary>
    [System.Serializable]
    public class Planet
    {
        /// <summary>
        /// 攻城対象の種別（PB-6）。規模で既定値が異なる：惑星＞要塞＞コロニー。
        /// 既定=惑星＝後方互換（既存の生成は従来どおり）。既定スケールは <see cref="PlanetSiegeRules.DefaultProfile"/>。
        /// </summary>
        public enum SiegeTargetKind { 惑星, 要塞, コロニー }

        /// <summary>紐づく星系ID（StarSystem.id）。</summary>
        public int systemId;

        /// <summary>攻城対象の種別（既定=惑星）。表示・既定スケールの分岐に使う（数値ロジックは値駆動なので種別非依存）。</summary>
        public SiegeTargetKind kind = SiegeTargetKind.惑星;

        /// <summary>所有勢力（占領で書き換わる。StarSystem.owner と整合させるのは戦略側の責務）。</summary>
        public Faction owner;

        /// <summary>制空権の健在度（ピラー・ドメイン／軌道超兵器）。&gt;0 の間は艦隊接近不可。0 でドメイン・ダウン。</summary>
        public float orbitalDefense;

        /// <summary>制空権の最大値（再建の上限）。</summary>
        public float maxOrbitalDefense;

        /// <summary>侵略値（ドメイン・ダウン後に蓄積。<see cref="invasionThreshold"/> 到達で占領）。</summary>
        public float invasionProgress;

        /// <summary>占領に必要な侵略値の閾値。</summary>
        public float invasionThreshold;

        /// <summary>ドメイン・ダウン（制空権崩壊）したか。艦隊接近・占領の解禁条件。</summary>
        public bool DomainDown => orbitalDefense <= 0f;

        /// <summary>占領済みか（侵略値が閾値到達）。</summary>
        public bool Captured => invasionProgress >= invasionThreshold;

        /// <summary>艦隊の接近が制空権で阻まれているか（ドメイン健在中＝接近限界 PB-5）。</summary>
        public bool FleetApproachBlocked => !DomainDown;

        /// <summary>種別の表示名（"惑星"/"要塞"/"コロニー"）。</summary>
        public string KindName => kind.ToString();

        public Planet() { }

        public Planet(int systemId, Faction owner, float maxOrbitalDefense, float invasionThreshold)
            : this(systemId, owner, maxOrbitalDefense, invasionThreshold, SiegeTargetKind.惑星) { }

        public Planet(int systemId, Faction owner, float maxOrbitalDefense, float invasionThreshold, SiegeTargetKind kind)
        {
            this.systemId = systemId;
            this.owner = owner;
            this.kind = kind;
            this.maxOrbitalDefense = Mathf.Max(0f, maxOrbitalDefense);
            this.orbitalDefense = this.maxOrbitalDefense;
            this.invasionThreshold = Mathf.Max(0.0001f, invasionThreshold);
            this.invasionProgress = 0f;
        }
    }
}
