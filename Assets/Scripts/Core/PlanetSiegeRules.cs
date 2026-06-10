using UnityEngine;

namespace Ginei
{
    /// <summary>惑星攻城の調整係数（#208 自動解決の数値モデル）。</summary>
    public readonly struct SiegeParams
    {
        /// <summary>S-AV戦力あたりの制空権抑制速度（ドメイン・ダウンまでの段階制圧）。</summary>
        public readonly float suppressRate;
        /// <summary>ドメイン・ダウン後の S-AV戦力あたり侵略値蓄積速度。</summary>
        public readonly float invadeRate;
        /// <summary>非交戦時の制空権再建速度（M.A.S.S. が尖塔を補修／防衛側の再武装）。</summary>
        public readonly float defenseRegen;

        public SiegeParams(float suppressRate, float invadeRate, float defenseRegen)
        {
            this.suppressRate = suppressRate;
            this.invadeRate = invadeRate;
            this.defenseRegen = defenseRegen;
        }

        /// <summary>既定係数（抑制=1・侵攻=1・再建=0）。tick は「戦力×係数×dt」で進む。</summary>
        public static SiegeParams Default => new SiegeParams(1f, 1f, 0f);
    }

    /// <summary>惑星攻城を1tick進めた結果の状態遷移（このtickで起きた事だけを報告）。</summary>
    public readonly struct SiegeTickResult
    {
        /// <summary>このtickでドメイン・ダウン（制空権崩壊）した。</summary>
        public readonly bool domainWentDown;
        /// <summary>このtickで占領に至った（所有が攻撃側へフリップ）。</summary>
        public readonly bool captured;

        public SiegeTickResult(bool domainWentDown, bool captured)
        {
            this.domainWentDown = domainWentDown;
            this.captured = captured;
        }
    }

    /// <summary>
    /// 惑星攻城の解決ルール（#131 惑星戦・PB-3〜PB-7 の純ロジック）。攻略は二段階：
    /// ①制圧＝S-AV(#757) が制空権(ピラー・ドメイン/超兵器)を抑制して 0 に → ドメイン・ダウン。
    /// ②侵攻＝ドメイン・ダウン後に侵略値を蓄積 → 閾値で占領（所有を攻撃側へフリップ）。
    /// リアルタイム個艦操作はせず、戦力×係数×時間で自動解決する（#208 流用の数値モデル）。
    /// 戦闘式・占領判定をここへ集約し、各所に重複実装しない。純ロジック（test-first）。
    /// </summary>
    public static class PlanetSiegeRules
    {
        // ===== 種別ごとの既定スケール（PB-6・コロニー/要塞の同枠適用。作者調整可）=====
        // 規模差：要塞＞惑星＞コロニー。コロニーは軌道超兵器(制空権)を持たない＝接近限界なしで即侵攻。
        /// <summary>惑星の制空権最大／占領閾値。</summary>
        public const float PlanetDefense = 100f, PlanetInvasion = 40f;
        /// <summary>要塞：制空権最強・侵略も重い（最も堅い）。</summary>
        public const float FortressDefense = 180f, FortressInvasion = 60f;
        /// <summary>コロニー：軌道超兵器なし(制空権0＝即ドメイン・ダウン)・侵略は軽い。</summary>
        public const float ColonyDefense = 0f, ColonyInvasion = 18f;

        /// <summary>攻城対象の規模プロファイル（制空権の最大＝防衛、占領閾値＝侵略）。</summary>
        public readonly struct SiegeProfile
        {
            /// <summary>制空権(超兵器/ドメイン)の最大値。0＝超兵器なし＝接近限界なしで即侵攻（コロニー）。</summary>
            public readonly float maxOrbitalDefense;
            /// <summary>占領に必要な侵略値の閾値。</summary>
            public readonly float invasionThreshold;

            public SiegeProfile(float maxOrbitalDefense, float invasionThreshold)
            {
                this.maxOrbitalDefense = maxOrbitalDefense;
                this.invasionThreshold = invasionThreshold;
            }
        }

        /// <summary>種別ごとの既定スケールを返す（PB-6・規模差の唯一の出所）。</summary>
        public static SiegeProfile DefaultProfile(Planet.SiegeTargetKind kind)
        {
            switch (kind)
            {
                case Planet.SiegeTargetKind.要塞:   return new SiegeProfile(FortressDefense, FortressInvasion);
                case Planet.SiegeTargetKind.コロニー: return new SiegeProfile(ColonyDefense, ColonyInvasion);
                default:                            return new SiegeProfile(PlanetDefense, PlanetInvasion);
            }
        }

        /// <summary>
        /// 種別の既定スケールで攻城対象(<see cref="Planet"/>)を生成する（PB-6・惑星/要塞/コロニーの統一生成窓口）。
        /// 規模差は <see cref="DefaultProfile"/> に集約。生成後の Tick/接近限界/占領は惑星と完全に同枠で扱える。
        /// </summary>
        public static Planet CreateTarget(int systemId, Faction owner, Planet.SiegeTargetKind kind)
        {
            SiegeProfile prof = DefaultProfile(kind);
            return new Planet(systemId, owner, prof.maxOrbitalDefense, prof.invasionThreshold, kind);
        }

        /// <summary>
        /// 攻城を deltaTime 進める。attackerSAV＝送り込んだ S-AV の戦力（0以下＝非交戦）。
        /// ドメイン健在中は制空権を抑制（侵略値は進まない）、ダウン後は侵略値を蓄積。1tickにつき一段階のみ。
        /// 占領に至ったら planet.owner を attacker へフリップする。状態遷移を返す。
        /// </summary>
        public static SiegeTickResult Tick(Planet planet, Faction attacker, float attackerSAV, float deltaTime, SiegeParams prm)
        {
            if (planet == null || deltaTime <= 0f) return default;

            bool wasDown = planet.DomainDown;
            bool wasCaptured = planet.Captured;

            if (attackerSAV <= 0f)
            {
                // 非交戦：制空権を再建（ドメイン健在時のみ・上限 max）。侵略値は維持（途中占領は据え置き）。
                if (!planet.DomainDown && prm.defenseRegen > 0f)
                    planet.orbitalDefense = Mathf.Min(planet.maxOrbitalDefense,
                        planet.orbitalDefense + prm.defenseRegen * deltaTime);
                return default;
            }

            if (!planet.DomainDown)
            {
                // ①制圧：制空権(ピラー・ドメイン/超兵器)を抑制
                planet.orbitalDefense = Mathf.Max(0f, planet.orbitalDefense - attackerSAV * prm.suppressRate * deltaTime);
            }
            else
            {
                // ②侵攻：ドメイン・ダウン後に侵略値を蓄積
                planet.invasionProgress += attackerSAV * prm.invadeRate * deltaTime;
            }

            bool domainWentDown = !wasDown && planet.DomainDown;
            bool capturedNow = !wasCaptured && planet.Captured;
            if (capturedNow) planet.owner = attacker; // 占領＝所有を攻撃側へフリップ

            return new SiegeTickResult(domainWentDown, capturedNow);
        }

        /// <summary>既定係数で1tick進める簡易版。</summary>
        public static SiegeTickResult Tick(Planet planet, Faction attacker, float attackerSAV, float deltaTime)
            => Tick(planet, attacker, attackerSAV, deltaTime, SiegeParams.Default);
    }
}
