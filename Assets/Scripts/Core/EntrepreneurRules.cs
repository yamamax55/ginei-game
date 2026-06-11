using UnityEngine;

namespace Ginei
{
    /// <summary>企業家類型＝シュンペーターの均衡破壊者(企業家)とルーティン運営者(管理者)の弁別（#1584）。</summary>
    public enum EntrepreneurType
    {
        /// <summary>企業家＝新結合（イノベーション）を断行して均衡を壊す創造的破壊者。リスクを取る。</summary>
        企業家,
        /// <summary>管理者＝既存事業を効率的に回すルーティンの担い手。安定をもたらすが破壊しない。</summary>
        管理者
    }

    /// <summary>起業活動・企業家類型の調整値（top-level＝クラス外・既定 <see cref="Default"/>）。</summary>
    public readonly struct EntrepreneurParams
    {
        /// <summary>企業家精神の構成比＝リスク選好の重み（残りは先見＋現状打破欲で配分）。</summary>
        public readonly float riskWeight;
        /// <summary>企業家精神の構成比＝先見（ビジョン）の重み。</summary>
        public readonly float visionWeight;
        /// <summary>企業家精神の構成比＝現状打破欲（落ち着かなさ）の重み。</summary>
        public readonly float restlessnessWeight;
        /// <summary>企業家/管理者を弁別する企業家精神の閾値（これ以上＝企業家）。</summary>
        public readonly float typeThreshold;
        /// <summary>起業成功に必要な総合適合（drive×資本×市場受容）の最小値。</summary>
        public readonly float startupBase;
        /// <summary>破滅確率の上限（過大リスク×低資本でも飛びすぎない安全弁）。</summary>
        public readonly float maxRuin;

        public EntrepreneurParams(float riskWeight, float visionWeight, float restlessnessWeight,
            float typeThreshold, float startupBase, float maxRuin)
        {
            // 重みは非負化してから合計1へ正規化（縮退時は均等割り）
            float rw = Mathf.Max(0f, riskWeight);
            float vw = Mathf.Max(0f, visionWeight);
            float sw = Mathf.Max(0f, restlessnessWeight);
            float sum = rw + vw + sw;
            if (sum <= 0f) { rw = vw = sw = 1f; sum = 3f; }
            this.riskWeight = rw / sum;
            this.visionWeight = vw / sum;
            this.restlessnessWeight = sw / sum;
            this.typeThreshold = Mathf.Clamp01(typeThreshold);
            this.startupBase = Mathf.Clamp01(startupBase);
            this.maxRuin = Mathf.Clamp01(maxRuin);
        }

        /// <summary>
        /// 既定＝リスク0.4/先見0.35/現状打破0.25・類型閾値0.5・起業必要適合0.5・破滅上限0.8。
        /// </summary>
        public static EntrepreneurParams Default => new EntrepreneurParams(0.4f, 0.35f, 0.25f, 0.5f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 企業家類型と起業活動の純ロジック（#1584・SCHU-2・シュンペーター・test-first・唯一の窓口）。
    /// 「企業家＝新結合（イノベーション）で均衡を壊す創造的破壊者で、リスクを取り創造的破壊を起こすが運営には向かない。
    /// ルーティンを効率的に回す管理者とは別の資質」を式に出す＝人物を企業家/管理者で弁別し（<see cref="TypeOf"/>）、
    /// 企業家精神（<see cref="EntrepreneurialDrive"/>）からイノベーション産出と運営安定性（逆相関）を導く。
    /// <para>分担：<see cref="PersonRules"/>(軍人/文民の適材適所＝役割と役職の一致。本クラスは起業家精神という別軸の資質弁別)
    /// ／<see cref="FirmRules"/>(企業の operation＝既存企業が需要を見て稼働を決め損益を出す。本クラスは均衡を壊して企業を生む側)
    /// ／<see cref="CreativeDestructionRules"/>(同EPIC・破壊の出所＝<see cref="CreativeDisruption"/> がその入力。本クラスは破壊を起こす人物を扱う)。
    /// 本クラスは「均衡破壊者vs管理者」の人物弁別と起業の成否・破滅に専念する。</para>
    /// 調整値は <see cref="EntrepreneurParams"/> に集約。全入力クランプ・乱数は roll 引数で決定論。
    /// </summary>
    public static class EntrepreneurRules
    {
        /// <summary>
        /// 企業家精神(0..1)＝リスク選好×重み＋先見×重み＋現状打破欲×重みの加重和（#1584）。
        /// シュンペーターの企業家＝新結合を断行する資質。3要素が高いほど均衡を壊しに行く。全入力0..1クランプ。
        /// </summary>
        public static float EntrepreneurialDrive(float riskAppetite, float vision, float restlessness, EntrepreneurParams p)
        {
            float risk = Mathf.Clamp01(riskAppetite);
            float vis = Mathf.Clamp01(vision);
            float rest = Mathf.Clamp01(restlessness);
            return risk * p.riskWeight + vis * p.visionWeight + rest * p.restlessnessWeight;
        }

        /// <summary>企業家精神（既定パラメータ）。</summary>
        public static float EntrepreneurialDrive(float riskAppetite, float vision, float restlessness)
            => EntrepreneurialDrive(riskAppetite, vision, restlessness, EntrepreneurParams.Default);

        /// <summary>
        /// 企業家/管理者の弁別（#1584）＝企業家精神が閾値以上なら企業家、未満なら管理者。
        /// 「均衡を壊すイノベーターか、ルーティンを回す運営者か」の別の資質を二分する。
        /// </summary>
        public static EntrepreneurType TypeOf(float entrepreneurialDrive, float threshold)
        {
            float drive = Mathf.Clamp01(entrepreneurialDrive);
            float th = Mathf.Clamp01(threshold);
            return drive >= th ? EntrepreneurType.企業家 : EntrepreneurType.管理者;
        }

        /// <summary>企業家/管理者の弁別（既定閾値）。</summary>
        public static EntrepreneurType TypeOf(float entrepreneurialDrive)
            => TypeOf(entrepreneurialDrive, EntrepreneurParams.Default.typeThreshold);

        /// <summary>
        /// 新結合（イノベーション）の産出(0..1)＝企業家精神×機会（#1584）。
        /// 企業家精神が高くても機会がなければ実らず、機会があっても担い手がいなければ生まれない＝両者の積。
        /// 「企業家は新結合を断行して均衡を壊す」のイノベーション量。全入力0..1クランプ。
        /// </summary>
        public static float InnovationOutput(float entrepreneurialDrive, float opportunity)
        {
            float drive = Mathf.Clamp01(entrepreneurialDrive);
            float opp = Mathf.Clamp01(opportunity);
            return drive * opp;
        }

        /// <summary>
        /// ルーティン運営の安定性(0..1)＝企業家精神の逆相関（#1584）＝1−企業家精神。
        /// 「破壊者は運営に向かない」＝企業家精神が高いほど現状を壊しに行き定常運営は不安定になる。
        /// 管理者（低drive）ほど既存事業を効率的に回せる＝<see cref="FirmRules"/> の operation 側に向く資質。
        /// </summary>
        public static float ManagerialEfficiency(float entrepreneurialDrive)
        {
            float drive = Mathf.Clamp01(entrepreneurialDrive);
            return 1f - drive; // 逆相関：壊す者は守れない
        }

        /// <summary>
        /// 起業の成否（#1584・決定論 roll）＝企業家精神×資本×市場受容の総合適合が
        /// <see cref="EntrepreneurParams.startupBase"/> を超える確率を上回れば成功。
        /// drive・資本・市場のどれかが欠ければ積で落ちる＝条件が揃って初めて起業は実る。
        /// roll は 0..1（小さいほど運に恵まれる）。全入力0..1クランプ。
        /// </summary>
        public static bool StartupSuccess(float drive, float capital, float marketReceptivity, float roll, EntrepreneurParams p)
        {
            float d = Mathf.Clamp01(drive);
            float cap = Mathf.Clamp01(capital);
            float market = Mathf.Clamp01(marketReceptivity);
            float fit = d * cap * market; // 三要素の積＝どれか欠ければ起業は実らない
            float chance = Mathf.Clamp01(fit / Mathf.Max(0.0001f, p.startupBase)); // 必要適合に届くほど成功率↑
            return Mathf.Clamp01(roll) < chance;
        }

        /// <summary>起業の成否（既定パラメータ）。</summary>
        public static bool StartupSuccess(float drive, float capital, float marketReceptivity, float roll)
            => StartupSuccess(drive, capital, marketReceptivity, roll, EntrepreneurParams.Default);

        /// <summary>
        /// 創造的破壊(0..1)＝企業家のイノベーション産出が既存均衡に与える破壊（#1584）。
        /// イノベーションが大きいほど旧い均衡（既存企業・旧技術）を壊す＝<see cref="CreativeDestructionRules"/> への入力。
        /// 「企業家は均衡を壊すイノベーター」の破壊量。ここでは産出をそのまま破壊圧として渡す（0..1クランプ）。
        /// </summary>
        public static float CreativeDisruption(float innovationOutput)
        {
            return Mathf.Clamp01(innovationOutput); // 新結合が大きいほど旧均衡を壊す
        }

        /// <summary>
        /// 破滅確率(0..1)＝過大なリスク選好が薄い資本を飛ばす（#1584・決定論用の確率値）。
        /// リスク選好が高く資本が薄いほど高い＝リスク×(1−資本)。リスクを取る企業家の影の代償。
        /// <see cref="EntrepreneurParams.maxRuin"/> を上限に抑える（必ずしも破滅しない）。全入力0..1クランプ。
        /// </summary>
        public static float RiskOfRuin(float riskAppetite, float capital, EntrepreneurParams p)
        {
            float risk = Mathf.Clamp01(riskAppetite);
            float cap = Mathf.Clamp01(capital);
            float ruin = risk * (1f - cap); // 過大リスク×薄い資本＝破滅
            return Mathf.Min(p.maxRuin, Mathf.Clamp01(ruin));
        }

        /// <summary>破滅確率（既定パラメータ）。</summary>
        public static float RiskOfRuin(float riskAppetite, float capital)
            => RiskOfRuin(riskAppetite, capital, EntrepreneurParams.Default);

        /// <summary>
        /// 起業環境の倍率(0..2)＝制度的支援−規制の煩雑さ（#1584）が起業活動を増減する。
        /// 支援が手厚く規制が薄いほど起業が増え（1超）、規制が重いほど萎える（1未満）。
        /// イノベーション産出・起業成功に掛け合わせる係数（基準1.0＝中立）。全入力0..1クランプ。
        /// </summary>
        public static float ClimateMultiplier(float institutionalSupport, float redTape)
        {
            float support = Mathf.Clamp01(institutionalSupport);
            float tape = Mathf.Clamp01(redTape);
            return Mathf.Clamp(1f + support - tape, 0f, 2f); // 支援で押し上げ・規制で押し下げ
        }
    }
}
