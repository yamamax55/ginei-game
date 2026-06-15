using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>主命カスケードの一段（TKO-11・#2488）。指揮系統の各階層が担う目標の規模（噛み砕かれた職責）。</summary>
    public class CascadeLevel
    {
        /// <summary>この段を担う人物（ICharacter.id）。</summary>
        public int holderId;
        /// <summary>階級 tier（指揮容量＝規模スケールの根拠）。</summary>
        public int tier;
        /// <summary>この段の目標規模（隻・上位ほど大きく下位ほど小さく具体的）。</summary>
        public float scope;
        /// <summary>親の段の添字（-1=最上位＝君主の主命そのもの）。</summary>
        public int parentIndex;

        public CascadeLevel() { }
        public CascadeLevel(int holderId, int tier, float scope, int parentIndex)
        {
            this.holderId = holderId;
            this.tier = tier;
            this.scope = scope;
            this.parentIndex = parentIndex;
        }
    }

    /// <summary>
    /// 主命の MBO カスケードの純ロジック（TKO-11・目標管理〔Management by Objectives〕・#2488・唯一の窓口）。君主の主命（TKO-9）を
    /// <b>指揮系統に沿って各階層が噛み砕き</b>、直属の部下→さらに部下→…と落とし込んで、末端のプレイヤーが<b>自分の職責に見合った具体的目標</b>として
    /// 受け取る。各段の目標規模は<b>指揮容量比</b>（<see cref="CommandCapacityRules.MaxStrengthForTier"/>）で縮小＝下位ほど小さく具体的に。
    /// 末端の具体的主命は<b>直属の上官</b>が委譲発令する（<see cref="SovereignMandateRules.IssueDelegated"/>）。以後の遂行/達成/失敗・武勲・恩義は
    /// <see cref="SovereignMandateRules"/> のまま（二重実装しない＝additive）。指揮容量は <see cref="CommandCapacityRules"/> へ委譲。決定論・test-first。
    /// </summary>
    public static class MandateCascadeRules
    {
        /// <summary>カスケードの調整値。</summary>
        public readonly struct CascadeParams
        {
            /// <summary>1段で割り当てる目標規模の下限（これ未満には縮まない＝末端でも意味のある任務量を保つ）。</summary>
            public readonly float minScope;

            public CascadeParams(float minScope) { this.minScope = Mathf.Max(0f, minScope); }

            /// <summary>既定＝最小規模500隻（戦隊規模の下限目安）。</summary>
            public static CascadeParams Default => new CascadeParams(500f);
        }

        /// <summary>
        /// 親の目標規模を子の職責へ噛み砕く＝子の規模＝親規模×(子の指揮容量/親の指揮容量)。
        /// 子の階級が上位（容量比&gt;1）なら縮めない（クランプ1）。最小規模 <see cref="CascadeParams.minScope"/> で床。
        /// </summary>
        public static float Decompose(float parentScope, int parentTier, int childTier, CascadeParams prm)
        {
            int parentCap = CommandCapacityRules.MaxStrengthForTier(parentTier);
            int childCap = CommandCapacityRules.MaxStrengthForTier(childTier);
            if (parentCap <= 0) return Mathf.Max(prm.minScope, 0f);
            float ratio = Mathf.Clamp01((float)childCap / parentCap);
            return Mathf.Max(prm.minScope, parentScope * ratio);
        }

        /// <summary>
        /// 指揮系統チェーン（<paramref name="chainTopToBottom"/>＝君主→…→プレイヤーの順）に沿って各段の目標規模を決める。
        /// 最上位は君主の主命そのもの（規模＝<paramref name="topScope"/>）、以降は <see cref="Decompose"/> で段ごとに噛み砕く。
        /// null/空エントリは飛ばす。各段は親の添字を持つ（ロールアップ用）。
        /// </summary>
        public static List<CascadeLevel> Build(float topScope, IReadOnlyList<Person> chainTopToBottom, CascadeParams prm)
        {
            var levels = new List<CascadeLevel>();
            if (chainTopToBottom == null) return levels;
            for (int i = 0; i < chainTopToBottom.Count; i++)
            {
                Person p = chainTopToBottom[i];
                if (p == null) continue;
                if (levels.Count == 0)
                {
                    levels.Add(new CascadeLevel(p.id, p.rankTier, Mathf.Max(prm.minScope, topScope), -1));
                }
                else
                {
                    CascadeLevel parent = levels[levels.Count - 1];
                    float scope = Decompose(parent.scope, parent.tier, p.rankTier, prm);
                    levels.Add(new CascadeLevel(p.id, p.rankTier, scope, levels.Count - 1));
                }
            }
            return levels;
        }

        /// <summary>既定パラメータでチェーンを噛み砕く。</summary>
        public static List<CascadeLevel> Build(float topScope, IReadOnlyList<Person> chainTopToBottom)
            => Build(topScope, chainTopToBottom, CascadeParams.Default);

        /// <summary>末端の段（＝プレイヤーが受け取る具体目標）。空なら null。</summary>
        public static CascadeLevel Leaf(List<CascadeLevel> levels)
            => levels == null || levels.Count == 0 ? null : levels[levels.Count - 1];

        /// <summary>子の親への寄与率（規模比 0..1）＝末端達成が上位目標の何割を満たすか（ロールアップ）。</summary>
        public static float Contribution(CascadeLevel child, CascadeLevel parent)
        {
            if (child == null || parent == null || parent.scope <= 0f) return 0f;
            return Mathf.Clamp01(child.scope / parent.scope);
        }

        /// <summary>
        /// 末端の段を「プレイヤーが<b>直属の上官</b>から拝命する具体的な主命」へ変換する（<see cref="SovereignMandateRules.IssueDelegated"/>）。
        /// 発令者＝末端の1つ上の段（直属上官）、拝命者＝末端（プレイヤー）。段が1つ（君主＝プレイヤー直結）しかなければ null＝
        /// カスケードを介さず君主が直接 <see cref="SovereignMandateRules.Issue"/> する想定。
        /// </summary>
        public static SovereignMandate ToLeafMandate(List<CascadeLevel> levels, int leafMandateId, Faction faction,
            MandateKind kind, string objective, int currentMonth, SovereignMandateRules.MandateParams prm)
        {
            if (levels == null || levels.Count < 2) return null;
            CascadeLevel leaf = levels[levels.Count - 1];
            CascadeLevel superior = levels[levels.Count - 2];
            return SovereignMandateRules.IssueDelegated(leafMandateId, superior.holderId, leaf.holderId,
                faction, kind, objective, currentMonth, prm);
        }
    }
}
