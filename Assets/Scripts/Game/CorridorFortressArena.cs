using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 回廊要塞の戦術アリーナ（#40 C-7・Battle シーン）。戦略マップで要塞に封鎖された回廊へ潜行すると
    /// <see cref="BattleSetup"/> が生成する。<see cref="SiegeArena"/>（惑星攻城）の回廊版。
    ///
    /// 地形＝両側が航行不能な岩壁に挟まれた一本の水路。その中ほどに要塞（<see cref="FortressUnit"/>）が居座り、
    /// <b>要塞の守備が健在なあいだ攻撃側は要塞の手前より先へ進めない</b>。壁があるので外周を回り込むこともできない
    /// （幾何の担保＝<see cref="CorridorArenaRules.LeavesNoGap"/>）。
    ///
    /// ★<b>基本は占領</b>（#40）：施設本体と守備は別勘定で、通常の会戦では要塞は<b>破壊されない</b>。
    /// 決着は次の2段階：
    ///  1. <b>守備制圧</b>＝施設の砲台・中枢が沈黙し（<see cref="FortressUnit.IsGarrisonSuppressed"/>）、
    ///     守備艦隊も残っていない。封鎖は解けるが<b>まだ所属は移らない</b>（「陥落」「破壊」ではない）。
    ///  2. <b>占領</b>＝その状態で攻撃側が制圧線（守備側の出口）へ到達する。ここで初めて所属が移り、
    ///     施設・モデル・名前はそのまま新しい持ち主のものになる。
    /// 判定は Core の <see cref="FortressCaptureRules"/>＝test-first。破壊は特殊手段専用の別状態
    /// （<see cref="FortressUnit.DestroyFacility"/>）で、通常の会戦からは到達しない。
    ///
    /// <see cref="FleetMovement"/> は改変せず、<see cref="SiegeArena"/> と同じく LateUpdate で位置を
    /// 地形へ収める（BlackHole 方式）。地形判定の実体は Core の <see cref="CorridorArenaRules"/>。
    /// </summary>
    public class CorridorFortressArena : MonoBehaviour
    {
        [Header("地形（回廊＝岩壁に挟まれた水路）")]
        [Tooltip("水路の半幅。|y| がこれを超えると岩壁＝航行不能")]
        public float channelHalfWidth = 18f;
        [Tooltip("水路の半長。x の可動範囲は ±これ")]
        public float channelHalfLength = 60f;
        [Tooltip("要塞の中心 x（守備側＝+x 側の手前）")]
        public float fortressX = 18f;
        [Tooltip("要塞が塞ぐ半径。channelHalfWidth 以上にすると横に隙間が無くなる＝迂回不能")]
        public float fortressBlockRadius = 20f;
        [Tooltip("ここまで到達したら制圧成立（守備側の出口＝制圧線）")]
        public float breakthroughX = 52f;

        [Header("陣営")]
        [Tooltip("要塞を保持している側（この勢力は自由に通れる）")]
        public Faction fortressOwner = Faction.帝国;
        [Tooltip("突破を目指す側")]
        public Faction attackerFaction = Faction.同盟;
        public string fortressLabel = "イゼルローン要塞";

        [Header("占領の条件（#40 基本は占領）")]
        [Tooltip("守備艦隊まで排除しないと占領を認めない（true＝厳格・既定）")]
        public bool requireGarrisonFleetsCleared = true;

        [Header("見た目")]
        public Color wallColor = new Color(0.24f, 0.22f, 0.28f, 1f);
        public Color breakthroughColor = new Color(0.98f, 0.82f, 0.42f, 0.9f);
        public Color blockadeColor = new Color(0.95f, 0.45f, 0.4f, 0.55f);
        [Tooltip("岩壁の帯の厚み（見た目のみ・判定は channelHalfWidth）")]
        public float wallThickness = 6f;
        [Tooltip("要塞モデルの直径を封鎖直径の何割にするか（1.0＝艦が入れない円と同じ大きさ）")]
        [Range(0.2f, 1.2f)]
        public float fortressModelFillRatio = 0.9f;

        /// <summary>
        /// 要塞が今も回廊を扼しているか（施設の守備の生存で決まる）。
        /// false になっても<b>施設が壊れたわけではない</b>（守備制圧＝通行が開いただけ）。
        /// </summary>
        public bool FortressHolds { get; private set; } = true;

        /// <summary>
        /// 占領（制圧）が成立したか＝守備が尽きた状態で攻撃側が制圧線へ到達した。
        /// 戦略へは「所属が移る」として書き戻される（<see cref="BattleManager"/>→<see cref="GalaxyView"/>）。
        /// 名前は互換のため据え置き（旧「突破」＝現「占領」）。
        /// </summary>
        public bool Breached { get; private set; }

        /// <summary>占領が成立したか（<see cref="Breached"/> と同義の読みやすい別名）。</summary>
        public bool Captured => Breached;

        /// <summary>守備を制圧したか（施設は健在・所属はまだ移っていない）。</summary>
        public bool GarrisonSuppressed => !FortressHolds;

        /// <summary>現在の制御状態（表示・書き戻しの語彙は <see cref="FortressCaptureRules"/> に合わせる）。</summary>
        public FortressControl Control => FortressCaptureRules.Resolve(BuildState(), CaptureParams);

        // 会戦はウィンドウ化で複数シーンが同時に走りうる（WIN-3 #2570）。static を1本にすると別戦場と
        // 混線するので、アリーナは<b>シーン単位</b>で引く。
        private static readonly Dictionary<Scene, CorridorFortressArena> arenas =
            new Dictionary<Scene, CorridorFortressArena>();

        /// <summary>指定シーンの回廊要塞アリーナ（無ければ null）。</summary>
        public static CorridorFortressArena For(Scene scene)
            => arenas.TryGetValue(scene, out var a) && a != null ? a : null;

        /// <summary>いずれか1つのアリーナ（単一会戦＝フルスクリーン時の従来動線）。</summary>
        public static CorridorFortressArena Any()
        {
            foreach (var kv in arenas) if (kv.Value != null) return kv.Value;
            return null;
        }

        private FortressUnit fortress;
        // 要塞を登録したか。Unity の Destroy 済みオブジェクトは == null になるため、
        // 「まだ生成していない」と「破壊された」を取り違えないよう別に持つ（取り違えると封鎖が解けない）。
        private bool fortressAttached;
        // 攻撃側が制圧線へ到達しているか（占領の2条件のうちの片方）。
        private bool attackerAtControlLine;
        private LineRenderer wallTop, wallBottom, breachLine, standoffLine;
        private Material lineMat;
        private readonly List<Material> ownedMaterials = new List<Material>();

        private CorridorArenaRules.CorridorArenaBounds Bounds =>
            new CorridorArenaRules.CorridorArenaBounds(
                channelHalfWidth, channelHalfLength, fortressX, fortressBlockRadius, breakthroughX);

        /// <summary>
        /// アリーナの寸法（ミニマップなど外から地形を描くための読み取り窓口）。
        /// 値はローカル座標（原点＝このアリーナの <see cref="Transform.position"/>）。
        /// </summary>
        public CorridorArenaRules.CorridorArenaBounds ArenaBounds => Bounds;

        /// <summary>アリーナの原点（ワールド）。壁・要塞・封鎖線はすべてこの点からの相対で置いてある。</summary>
        public Vector2 ArenaOrigin => transform.position;

        /// <summary>要塞の<b>実体</b>の半径（艦がめり込めない大きさ）。封鎖半径とは別物。</summary>
        public float FortressBodyRadius => Bounds.fortressBodyRadius;

        /// <summary>封鎖線の x（ローカル）。守備が健在なあいだ、敵対する艦はここより先へ進めない。</summary>
        public float StandoffX => fortressX - fortressBlockRadius;

        private FortressCaptureParams CaptureParams => new FortressCaptureParams(requireGarrisonFleetsCleared);

        private void Awake()
        {
            arenas[gameObject.scene] = this;
            BuildTerrain();
        }

        private void OnDestroy()
        {
            if (arenas.TryGetValue(gameObject.scene, out var a) && a == this) arenas.Remove(gameObject.scene);
            for (int i = 0; i < ownedMaterials.Count; i++)
                if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]); // 実行時生成マテリアルは破棄（規約）
            ownedMaterials.Clear();
        }

        /// <summary>戦略側の要塞を受け取ってアリーナを整える（BattleSetup が呼ぶ）。</summary>
        public void Configure(Faction owner, Faction attacker, string label, float garrisonStrength)
        {
            fortressOwner = owner;
            attackerFaction = attacker;
            if (!string.IsNullOrEmpty(label)) fortressLabel = label;
            FortressHolds = garrisonStrength > 0f;
        }

        /// <summary>アリーナの中央へ要塞の実体を据える（BattleSetup が生成した FortressUnit を登録）。</summary>
        public void AttachFortress(FortressUnit unit)
        {
            fortress = unit;
            fortressAttached = unit != null;
            // 壁と封鎖線はこの GameObject のローカル座標で引いてあるので、要塞も<b>子として</b>同じ原点に置く
            // ＝BattleSetup.ApplyWorldOffset がシーンごと平行移動しても地形と要塞がずれない。
            if (fortress != null)
            {
                fortress.transform.SetParent(transform, false);
                fortress.transform.localPosition = new Vector3(fortressX, 0f, 0f);
                // 「見えている要塞の大きさ＝艦が入れない円」に揃える＝なぜそこで止まるのかが画面で分かる。
                fortress.SetPresentationDiameter(fortressBlockRadius * 2f * Mathf.Max(0.2f, fortressModelFillRatio));
            }
        }

        private void BuildTerrain()
        {
            lineMat = new Material(Shader.Find("Sprites/Default"));
            ownedMaterials.Add(lineMat);

            // 岩壁（航行不能）。判定は channelHalfWidth なので、帯は「越えられない縁」を示す表示。
            wallTop = MakeLine("WallTop", wallColor, wallThickness);
            wallBottom = MakeLine("WallBottom", wallColor, wallThickness);
            SetSegment(wallTop, new Vector2(-channelHalfLength, channelHalfWidth + wallThickness * 0.5f),
                                new Vector2(channelHalfLength, channelHalfWidth + wallThickness * 0.5f));
            SetSegment(wallBottom, new Vector2(-channelHalfLength, -channelHalfWidth - wallThickness * 0.5f),
                                   new Vector2(channelHalfLength, -channelHalfWidth - wallThickness * 0.5f));

            // 制圧線（守備側の出口）。守備を制圧したうえでここへ届けば占領成立。
            breachLine = MakeLine("BreakthroughLine", breakthroughColor, 0.5f);
            SetSegment(breachLine, new Vector2(breakthroughX, -channelHalfWidth),
                                   new Vector2(breakthroughX, channelHalfWidth));

            // 封鎖線（要塞の手前＝攻撃側はここより先へ進めない）。守備を制圧すると消える。
            standoffLine = MakeLine("StandoffLine", blockadeColor, 0.4f);
            float stand = fortressX - fortressBlockRadius;
            SetSegment(standoffLine, new Vector2(stand, -channelHalfWidth), new Vector2(stand, channelHalfWidth));
        }

        private LineRenderer MakeLine(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.startWidth = lr.endWidth = width;
            lr.sharedMaterial = lineMat;
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = -2; // 艦より奥
            return lr;
        }

        private static void SetSegment(LineRenderer lr, Vector2 a, Vector2 b)
        {
            if (lr == null) return;
            lr.SetPosition(0, new Vector3(a.x, a.y, 0f));
            lr.SetPosition(1, new Vector3(b.x, b.y, 0f));
        }

        /// <summary>
        /// 現在の状況を Core の語彙へ写す。施設本体（コア・砲台）と守備艦隊を<b>別々に</b>数えるのが要点
        /// ＝「守備艦隊を全滅させた」と「施設を制圧した」を混同しない。
        /// </summary>
        private FortressSiegeState BuildState()
        {
            bool exists = !fortressAttached || (fortress != null && fortress.FacilityExists);
            int core;
            int activeTurrets;
            if (!fortressAttached)
            {
                // まだ要塞を据えていない（生成前）＝Configure の初期値で「守備健在かどうか」だけを表す。
                core = FortressHolds ? 1 : 0;
                activeTurrets = 0;
            }
            else if (fortress == null)
            {
                core = 0; activeTurrets = 0; // 実体が消えた（特殊破壊）
            }
            else
            {
                core = fortress.IsGarrisonSuppressed ? 0 : Mathf.Max(0, fortress.coreStrength);
                activeTurrets = fortress.ActiveTurretCount;
            }
            return new FortressSiegeState(core, activeTurrets, CountGarrisonFleets(),
                                          attackerAtControlLine, facilityDestroyed: !exists);
        }

        /// <summary>この戦場に生き残っている守備側（要塞の持ち主側）の艦隊数。</summary>
        private int CountGarrisonFleets()
        {
            int n = 0;
            Scene myScene = gameObject.scene;
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || !fs.IsAlive) continue;
                if (fs.gameObject.scene != myScene) continue;
                // 「攻撃側に敵対する側」＝守備側。多勢力でも FactionRelations が唯一の窓口。
                if (FactionRelations.IsHostile(null, fs.Faction, null, attackerFaction)) n++;
            }
            return n;
        }

        private void LateUpdate()
        {
            // 施設の守備の生死を毎フレーム見る＝制圧されれば封鎖が解け、その場から先へ進めるようになる。
            // Destroy 済みの FortressUnit は == null になるので、登録済みかどうかで場合分けする
            // （未登録＝生成前は Configure の初期値、登録済みで null/沈黙＝守備制圧なので封鎖解除）。
            bool holds = fortressAttached ? (fortress != null && fortress.IsAlive) : FortressHolds;
            if (holds != FortressHolds)
            {
                FortressHolds = holds;
                if (!holds)
                {
                    // ★「陥落」「破壊」とは言わない＝施設は健在で、占領はまだ成立していない（#40）。
                    NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                        FortressCaptureRules.DescribeControl(FortressControl.守備制圧, fortressLabel));
                }
            }
            if (standoffLine != null && standoffLine.enabled != FortressHolds) standoffLine.enabled = FortressHolds;

            var bounds = Bounds;
            // 原点は<b>アリーナ自身の位置</b>。壁も要塞も自分の子なので、BattleSetup.ApplyWorldOffset で
            // シーンごと平行移動されても地形・要塞・判定が必ず一致する（BattleField を二重に読まない）。
            Vector2 origin = transform.position;
            Scene myScene = gameObject.scene;

            // 全個艦（旗艦＋配下艦）を地形へ収める。旗艦だけ止めると配下艦が壁を抜けるので全部を対象にする。
            var targets = FleetRegistry.AllTargets;
            for (int i = 0; i < targets.Count; i++)
            {
                IShipTarget t = targets[i];
                if (t == null) continue;
                Transform tr = t.Transform;
                if (tr == null) continue;
                // 会戦は同時に複数シーンが走りうる（WIN-3）。自分の戦場の艦だけを拘束する
                // ＝別の会戦の艦をこの回廊の壁へ押し込まない。
                if (tr.gameObject.scene != myScene) continue;

                bool blocked = FortressHolds && FactionRelations.IsHostile(null, t.Faction, null, fortressOwner);

                Vector3 pos = tr.position;
                Vector2 local = new Vector2(pos.x, pos.y) - origin;
                Vector2 confined = CorridorArenaRules.Confine(local, bounds, blocked);
                if (confined != local)
                    tr.position = new Vector3(confined.x + origin.x, confined.y + origin.y, pos.z);

                // 制圧線への到達＝占領の条件その2（条件その1＝守備が尽きていること）。
                if (!attackerAtControlLine && !blocked
                    && FactionRelations.IsHostile(null, t.Faction, null, fortressOwner)
                    && CorridorArenaRules.IsBreakthrough(confined, bounds))
                {
                    attackerAtControlLine = true;
                }
            }

            EvaluateCapture();
        }

        /// <summary>
        /// 占領（制圧）の成立を判定する。成立条件は Core の <see cref="FortressCaptureRules.CanCapture"/>＝
        /// <b>守備が尽きた（施設の中枢・砲台が沈黙し、守備艦隊も残っていない）状態で攻撃側が制圧線へ到達</b>。
        /// 成立したら要塞の所属をその場で移し（施設は無傷のまま）、通知を出す。
        /// </summary>
        private void EvaluateCapture()
        {
            if (Breached) return;
            FortressSiegeState state = BuildState();
            if (!FortressCaptureRules.CanCapture(state, CaptureParams)) return;

            Breached = true;
            // 施設は壊さず所属だけを移す（モデル・名前はそのまま新しい持ち主のものになる）。
            if (fortress != null) fortress.ApplyCapture(attackerFaction, null);

            NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.警告,
                FortressCaptureRules.DescribeControl(FortressControl.占領, fortressLabel, attackerFaction.ToString()));
        }

        // 戦略への書き戻しは <see cref="BattleManager.ReturnFromCorridorFortress"/> が唯一の窓口。
        // 以前ここにも同じ処理（WriteBackToStrategy）を置いていたが、誰も呼んでおらず、
        // 呼べば二重書き戻しになるだけなので削除した（結果の出所を1つに保つ）。
    }
}
