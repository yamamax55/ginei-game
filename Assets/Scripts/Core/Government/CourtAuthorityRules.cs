using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 朝廷の権威（<see cref="CourtAuthority"/>）の動態の純ロジック（日本の律令制・官僚制基盤）。
    /// <b>戦乱が長引くほど武家が台頭して朝廷の権威は下がり（戦国化）、平時は律令が回復へ向かう（中興）</b>＝
    /// 権威が動くと <see cref="RitsuryoFormalizationRules.PhaseOf"/>（律令制→摂関→院政→武家→戦国）が遷移し、
    /// 名実の乖離・門閥人事・汚職・内政寄与がまとめて変わる（形骸化レイヤーが“生きる”）。基準値非破壊・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CourtAuthorityRules
    {
        /// <summary>権威動態の調整値。</summary>
        public readonly struct AuthorityParams
        {
            public readonly float peaceEquilibrium; // 平時に収束する権威（律令を保てる上限）
            public readonly float warFloor;           // 戦乱が最大のとき引き下げられる下限
            public readonly float driftRate;          // 1年あたり目標へ寄る速さ

            public AuthorityParams(float peaceEquilibrium, float warFloor, float driftRate)
            {
                this.peaceEquilibrium = Mathf.Clamp01(peaceEquilibrium);
                this.warFloor = Mathf.Clamp01(warFloor);
                this.driftRate = Mathf.Max(0f, driftRate);
            }

            /// <summary>既定＝平時0.6へ回復・戦乱最大で0.1へ・年0.05で寄る（緩やか）。</summary>
            public static AuthorityParams Default => new AuthorityParams(0.6f, 0.1f, 0.05f);
        }

        /// <summary>戦乱度（0..1＝前線/交戦の広がり）が定める権威の目標。戦乱が広いほど低い（武家台頭）。</summary>
        public static float Target(float warIntensity, AuthorityParams p)
            => Mathf.Lerp(p.peaceEquilibrium, p.warFloor, Mathf.Clamp01(warIntensity));

        /// <summary>朝廷の権威を1年ぶん目標へ寄せる（<see cref="CourtAuthority.authority"/> を更新・null安全）。</summary>
        public static void TickYear(CourtAuthority court, float warIntensity, AuthorityParams p)
        {
            if (court == null) return;
            float target = Target(warIntensity, p);
            court.authority = Mathf.Clamp01(Mathf.MoveTowards(court.authority, target, p.driftRate));
        }
    }
}
