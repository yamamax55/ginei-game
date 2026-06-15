using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 官吏の清廉度（<see cref="OfficialMerit.integrity"/>）の動態＝汚職の生成・抑制の純ロジック
    /// （日本の律令制・官僚制基盤）。<b>監督（朝廷の権威）が弱いほど汚職が育つ</b>＝官職が名誉職化し
    /// 中央の監察が届かない世（封建）では清廉が崩れ、律令が機能して監督が効く世では清廉が保たれる。
    /// 清廉度は考課（徳）と内政（<see cref="AdministrationRules"/>）に跳ね返るため、これが動くと
    /// 「腐った官僚→悪政→不安定」の負ループが回る。基準値非破壊（integrity のみ更新）・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OfficialIntegrityRules
    {
        /// <summary>清廉度の動態の調整値。</summary>
        public readonly struct IntegrityParams
        {
            public readonly float cleanBaseline; // 朝廷の権威が満点（監督が効く）のときの清廉の目標
            public readonly float corruptFloor;   // 権威0（名誉職化・無監督）のときの清廉の目標
            public readonly float driftRate;       // 1年あたり目標へ寄る速さ

            public IntegrityParams(float cleanBaseline, float corruptFloor, float driftRate)
            {
                this.cleanBaseline = Mathf.Clamp01(cleanBaseline);
                this.corruptFloor = Mathf.Clamp01(corruptFloor);
                this.driftRate = Mathf.Max(0f, driftRate);
            }

            /// <summary>既定＝権威満点で0.85・権威0で0.30・年0.10で寄る。</summary>
            public static IntegrityParams Default => new IntegrityParams(0.85f, 0.30f, 0.10f);
        }

        /// <summary>監督（朝廷の権威）が定める清廉の目標。権威が高いほど清廉、低いほど汚職横行へ。</summary>
        public static float TargetIntegrity(float courtAuthority, IntegrityParams p)
            => Mathf.Lerp(p.corruptFloor, p.cleanBaseline, Mathf.Clamp01(courtAuthority));

        /// <summary>清廉度を1年ぶん目標へ寄せた値（0..1）を返す。基準は呼び出し側が代入（実効値パターン）。</summary>
        public static float Tick(float current, float courtAuthority, IntegrityParams p)
            => Mathf.Clamp01(Mathf.MoveTowards(Mathf.Clamp01(current), TargetIntegrity(courtAuthority, p), p.driftRate));
    }
}
