using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 要塞<b>主砲</b>の演出（#F）。通常砲撃（細い1本のビーム）と一目で区別できるよう、層を重ねる。
    ///
    /// 段取り：<b>集光</b>（チャージ中に光の粒が砲口へ収束）→<b>砲口閃光</b>→
    /// <b>大口径ビーム</b>（明るい芯＋外側の光の2本）→<b>着弾衝撃波</b>と<b>残光</b>。
    ///
    /// <b>見た目だけ</b>＝当たり判定・射線・射程・射界・クールダウン・ダメージの発生時刻は
    /// <see cref="FortressMainCannon"/> のまま。この装置は「撃った」と言われて絵を出すだけなので、
    /// 演出が伸びても当たっていない敵に損害は出ない。
    ///
    /// <b>Bloom に頼らない</b>：発光は加算ブレンドのスプライトを自前で重ねて作る
    /// （ポストプロセスが無い環境でも層が見える）。
    /// <b>ポーズ/倍速に追従</b>：演出は game-time（<see cref="Time.deltaTime"/>）で進む＝ポーズで止まる。
    /// <b>後片付け</b>：主砲が止まった／要塞が落ちたら <see cref="StopAll"/> で残留ビームを消す。
    /// </summary>
    public class MainCannonFx : MonoBehaviour
    {
        [Header("集光（チャージ）")]
        [Tooltip("砲口へ収束する光の粒の数")]
        public int convergeCount = 8;
        [Tooltip("収束が始まる距離（ワールド単位）")]
        public float convergeRadius = 3.2f;

        [Header("ビーム")]
        [Tooltip("明るい芯の太さ")]
        public float coreWidth = 0.55f;
        [Tooltip("外側の光の太さ（芯より太く・淡い）")]
        public float glowWidth = 2.4f;
        [Tooltip("ビームが見えている時間（秒・game-time）")]
        public float beamDuration = 0.45f;
        [Tooltip("残光が消えるまでの時間（秒・game-time）")]
        public float afterglowDuration = 0.7f;

        [Header("着弾")]
        [Tooltip("衝撃波の最大半径（ワールド単位）")]
        public float shockwaveRadius = 4.5f;
        [Tooltip("衝撃波が広がりきる時間（秒・game-time）")]
        public float shockwaveDuration = 0.5f;

        [Header("色")]
        public Color coreColor = new Color(1f, 0.98f, 0.90f, 1f);
        public Color glowColor = new Color(1f, 0.72f, 0.32f, 0.55f);
        public Color chargeColor = new Color(1f, 0.85f, 0.35f, 0.9f);

        private LineRenderer core;
        private LineRenderer glow;
        private SpriteRenderer muzzleFlash;
        private SpriteRenderer shockwave;
        private readonly List<SpriteRenderer> convergeDots = new List<SpriteRenderer>();
        private readonly List<Material> owned = new List<Material>();
        private static Sprite sharedGlowSprite;

        private void Awake()
        {
            core = MakeLine("MainCannonCore", coreWidth, coreColor, 12);
            glow = MakeLine("MainCannonGlow", glowWidth, glowColor, 11);
            muzzleFlash = MakeSprite("MuzzleFlash", 13);
            shockwave = MakeSprite("Shockwave", 13);
            SetVisible(false);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);   // 実行時生成マテリアルは破棄（規約）
            owned.Clear();
        }

        // ===== 外から呼ぶ窓口 =====

        /// <summary>
        /// チャージ中の集光を更新する（毎フレーム呼ぶ）。<paramref name="progress"/> は 0→1。
        /// 光の粒が砲口へ寄っていき、砲口の光が強くなる＝「これから撃つ」と分かる。
        /// </summary>
        public void UpdateCharge(Vector3 muzzle, Vector2 aimDir, float progress)
        {
            progress = Mathf.Clamp01(progress);
            EnsureConvergeDots();

            float t = 1f - progress;                       // 1（遠い）→0（砲口）
            Vector2 perp = new Vector2(-aimDir.y, aimDir.x);
            for (int i = 0; i < convergeDots.Count; i++)
            {
                SpriteRenderer d = convergeDots[i];
                if (d == null) continue;
                // 砲口の周りに扇状に配し、進むほど砲口へ寄せる。
                float a = (i / (float)Mathf.Max(1, convergeDots.Count)) * Mathf.PI * 2f;
                Vector2 offset = (perp * Mathf.Sin(a) + (Vector2)aimDir * Mathf.Cos(a) * 0.35f) * convergeRadius * t;
                d.transform.position = muzzle + (Vector3)offset;
                float scale = Mathf.Lerp(0.20f, 0.55f, progress);
                d.transform.localScale = Vector3.one * scale;
                Color c = chargeColor;
                c.a = Mathf.Lerp(0.15f, 0.95f, progress);
                d.color = c;
                d.enabled = true;
            }

            // 砲口そのものも溜まるほど明るくする。
            if (muzzleFlash != null)
            {
                muzzleFlash.transform.position = muzzle;
                muzzleFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.1f, progress);
                Color c = chargeColor;
                c.a = Mathf.Lerp(0.1f, 0.8f, progress);
                muzzleFlash.color = c;
                muzzleFlash.enabled = true;
            }
        }

        /// <summary>チャージをやめた（標的消失・沈黙・会戦終了）＝集光を消す。</summary>
        public void CancelCharge()
        {
            HideConvergeDots();
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        /// <summary>
        /// 発射の演出（砲口閃光→大口径ビーム→着弾衝撃波→残光）。
        /// <b>ダメージはこの前に済んでいる</b>＝ここでは何も当てない。
        /// </summary>
        public void PlayShot(Vector3 muzzle, Vector3 impact)
        {
            HideConvergeDots();
            StopAllCoroutines();
            StartCoroutine(ShotRoutine(muzzle, impact));
        }

        /// <summary>すべての演出を止めて消す（主砲停止・要塞制圧・会戦終了時に呼ぶ）。</summary>
        public void StopAll()
        {
            StopAllCoroutines();
            HideConvergeDots();
            SetVisible(false);
        }

        // ===== 実装 =====

        private IEnumerator ShotRoutine(Vector3 muzzle, Vector3 impact)
        {
            // ① 砲口閃光（一瞬・大きく）
            if (muzzleFlash != null)
            {
                muzzleFlash.transform.position = muzzle;
                muzzleFlash.enabled = true;
            }

            // ② 大口径ビーム（芯＋外光）。長さは実際の射線と同じ＝見た目と当たりが食い違わない。
            if (core != null) { core.SetPosition(0, muzzle); core.SetPosition(1, impact); core.enabled = true; }
            if (glow != null) { glow.SetPosition(0, muzzle); glow.SetPosition(1, impact); glow.enabled = true; }

            // ③ 着弾の衝撃波
            if (shockwave != null)
            {
                shockwave.transform.position = impact;
                shockwave.enabled = true;
            }

            float dur = Mathf.Max(0.05f, beamDuration);
            float elapsed = 0f;
            while (elapsed < dur)
            {
                // game-time で進める＝ポーズで止まり、倍速で速く終わる（規約）。
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / dur);

                // ビームは太く始まって細く閉じる（撃ち抜いた感じ）。
                float shrink = 1f - k * 0.65f;
                if (core != null) core.widthMultiplier = coreWidth * shrink;
                if (glow != null) glow.widthMultiplier = glowWidth * shrink;
                SetAlpha(core, coreColor, 1f - k * 0.5f);
                SetAlpha(glow, glowColor, (1f - k) * 0.9f);

                // 砲口閃光は最初の2割で消える。
                if (muzzleFlash != null)
                {
                    float f = Mathf.Clamp01(1f - k / 0.2f);
                    muzzleFlash.transform.localScale = Vector3.one * (1.4f + (1f - f) * 0.6f);
                    Color c = coreColor; c.a = f;
                    muzzleFlash.color = c;
                    if (f <= 0.001f) muzzleFlash.enabled = false;
                }

                // 衝撃波は広がりながら薄くなる。
                if (shockwave != null)
                {
                    float s = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, shockwaveDuration));
                    shockwave.transform.localScale = Vector3.one * (shockwaveRadius * s);
                    Color c = glowColor; c.a = (1f - s) * 0.8f;
                    shockwave.color = c;
                    if (s >= 1f) shockwave.enabled = false;
                }
                yield return null;
            }

            // ④ 残光（芯だけを細く残してから消す）
            if (core != null) core.enabled = false;
            float ag = Mathf.Max(0.05f, afterglowDuration);
            elapsed = 0f;
            while (elapsed < ag)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / ag);
                if (glow != null) glow.widthMultiplier = glowWidth * 0.35f * (1f - k);
                SetAlpha(glow, glowColor, (1f - k) * 0.35f);
                yield return null;
            }

            SetVisible(false);
        }

        private static void SetAlpha(LineRenderer line, Color baseColor, float alpha)
        {
            if (line == null) return;
            Color c = baseColor;
            c.a = Mathf.Clamp01(alpha);
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, c.a * 0.75f);
        }

        private void SetVisible(bool on)
        {
            if (core != null) core.enabled = on;
            if (glow != null) glow.enabled = on;
            if (muzzleFlash != null) muzzleFlash.enabled = on;
            if (shockwave != null) shockwave.enabled = on;
        }

        private void EnsureConvergeDots()
        {
            while (convergeDots.Count < Mathf.Max(1, convergeCount))
                convergeDots.Add(MakeSprite("Converge" + convergeDots.Count, 12));
        }

        private void HideConvergeDots()
        {
            for (int i = 0; i < convergeDots.Count; i++)
                if (convergeDots[i] != null) convergeDots[i].enabled = false;
        }

        private LineRenderer MakeLine(string name, float width, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.widthMultiplier = width;
            lr.sortingOrder = order;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;

            Material m = BeamFx.CreateMaterial();   // 加算ブレンド＝Bloom が無くても光って見える
            owned.Add(m);
            lr.material = m;
            lr.startColor = color;
            lr.endColor = color;
            return lr;
        }

        private SpriteRenderer MakeSprite(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetGlowSprite();
            sr.sortingOrder = order;
            Material m = BeamFx.CreateMaterial();
            owned.Add(m);
            sr.material = m;
            sr.enabled = false;
            return sr;
        }

        /// <summary>中心が白く縁へ向けて消える光の玉（アプリ寿命で1枚だけ作る）。</summary>
        private static Sprite GetGlowSprite()
        {
            if (sharedGlowSprite != null) return sharedGlowSprite;
            const int size = 64;
            const float c = (size - 1) / 2f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;                               // 中心を強く
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            sharedGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedGlowSprite;
        }
    }
}
