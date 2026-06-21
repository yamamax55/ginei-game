using System.Collections;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// BGM と効果音を一元管理するシングルトン。DontDestroyOnLoad で永続化。
    /// Inspector で AudioClip を割り当てる。未割り当て clip は無音でエラーにならない。
    /// 未割当時は Resources から規約名（bgm_title 等）で自動ロード＝ファイルを置くだけで鳴る
    /// （コード生成の AudioManager でも有効。導入手順は docs/audio-sourcing.md）。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Object.FindFirstObjectByType<AudioManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AudioManager");
                        instance = go.AddComponent<AudioManager>();
                        if (Application.isPlaying)
                            DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("BGM")]
        public AudioClip bgmTitle;
        public AudioClip bgmBattle;
        public AudioClip bgmStrategy;
        public AudioClip bgmResult;

        [Header("BGM Resources フォールバック名（Inspector未割当時に Resources からロード）")]
        [Tooltip("Assets/Resources/ 直下にこの名前で音源を置けば Inspector 割当なしで鳴る")]
        public string bgmTitleResource = "bgm_title";
        public string bgmBattleResource = "bgm_battle";
        public string bgmStrategyResource = "bgm_strategy";
        public string bgmResultResource = "bgm_result";

        [Header("BGM クロスフェード")]
        [Tooltip("BGM切替のフェード時間（秒・実時間）。0で即時切替。クラシックの曲間ギャップを和らげる")]
        public float bgmCrossfade = 1.2f;

        [Header("効果音")]
        public AudioClip seBeam;
        public AudioClip seHit;
        public AudioClip seExplosion;
        public AudioClip seUiClick;

        [Header("効果音 Resources フォールバック名")]
        public string seHitResource = "se_hit";
        public string seExplosionResource = "se_explosion";
        public string seUiClickResource = "se_uiclick";

        [Header("音量")]
        [Range(0f, 1f)] public float bgmVolume = 0.6f;
        [Range(0f, 1f)] public float seVolume = 1.0f;

        [Header("効果音の発音制御")]
        [Tooltip("SEの同時発音ボイス数（重ねて鳴らせる数）。多数の配下艦が同時発砲しても途切れにくくする")]
        public int seVoiceCount = 6;
        [Tooltip("ビームSEを鳴らす最小間隔（秒・実時間）。全艦が同時発砲しても音が氾濫しないよう間引く")]
        public float beamMinInterval = 0.035f;
        [Tooltip("ビームSEのピッチ揺らぎ（±）。同じ音の反復で機械的に聞こえるのを避ける")]
        [Range(0f, 0.5f)] public float beamPitchJitter = 0.12f;

        [Tooltip("Inspectorでビーム音が未割当のとき Resources からロードするクリップ名")]
        public string beamClipResource = "shot_1";

        private AudioSource bgmSource;
        private AudioSource seSource;
        private AudioSource[] seVoices; // 重ね発音用ボイスプール（ピッチをばらけさせる）
        private int seVoiceIndex;
        private float nextBeamTime;     // 次にビームSEを鳴らせる実時間
        private Coroutine bgmFadeRoutine; // クロスフェード進行中のコルーチン

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            seSource = gameObject.AddComponent<AudioSource>();
            seSource.loop = false;
            seSource.playOnAwake = false;

            // SE のボイスプール（ピッチを1発ごとに変えるため複数ソースを用意）
            int n = Mathf.Max(1, seVoiceCount);
            seVoices = new AudioSource[n];
            for (int i = 0; i < n; i++)
            {
                AudioSource v = gameObject.AddComponent<AudioSource>();
                v.loop = false;
                v.playOnAwake = false;
                seVoices[i] = v;
            }

            // Inspector未割当の音源は Resources からフォールバック読み込み（規約名で置くだけで鳴る）
            if (seBeam == null && !string.IsNullOrEmpty(beamClipResource))
                seBeam = Resources.Load<AudioClip>(beamClipResource);
            LoadIfNull(ref bgmTitle, bgmTitleResource);
            LoadIfNull(ref bgmBattle, bgmBattleResource);
            LoadIfNull(ref bgmStrategy, bgmStrategyResource);
            LoadIfNull(ref bgmResult, bgmResultResource);
            LoadIfNull(ref seHit, seHitResource);
            LoadIfNull(ref seExplosion, seExplosionResource);
            LoadIfNull(ref seUiClick, seUiClickResource);

            ApplyMasterVolume();
        }

        private void Start()
        {
            ApplyMasterVolume();
        }

        public void ApplyMasterVolume()
        {
            float master = GameSettings.Instance.masterVolume;
            if (bgmSource != null) bgmSource.volume = bgmVolume * master;
            if (seSource != null) seSource.volume = seVolume * master;
        }

        /// <summary>未割当(null)なら Resources から名前でロードする補助。</summary>
        private static void LoadIfNull(ref AudioClip clip, string resourceName)
        {
            if (clip == null && !string.IsNullOrEmpty(resourceName))
                clip = Resources.Load<AudioClip>(resourceName);
        }

        /// <summary>現在のシーン名に対応する BGM を再生する（Title/Battle/Strategy/Result）。</summary>
        public void PlayBGMForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "Title": PlayBGM(bgmTitle); break;
                case "Battle": PlayBGM(bgmBattle); break;
                case "Strategy": PlayBGM(bgmStrategy); break;
                case "Result": PlayBGM(bgmResult); break;
            }
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            // 即時切替（フェード無効・非再生中・非アクティブ時）。
            if (bgmCrossfade <= 0f || !Application.isPlaying || !isActiveAndEnabled || !bgmSource.isPlaying)
            {
                if (bgmFadeRoutine != null) { StopCoroutine(bgmFadeRoutine); bgmFadeRoutine = null; }
                bgmSource.clip = clip;
                bgmSource.volume = bgmVolume * GameSettings.Instance.masterVolume;
                bgmSource.Play();
                return;
            }

            if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = StartCoroutine(CrossfadeTo(clip));
        }

        /// <summary>現BGMをフェードアウト→新BGMをフェードイン（曲間の唐突な切替を和らげる簡易版・実時間）。</summary>
        private IEnumerator CrossfadeTo(AudioClip clip)
        {
            float target = bgmVolume * GameSettings.Instance.masterVolume;
            float dur = Mathf.Max(0.01f, bgmCrossfade);

            float from = bgmSource.volume;
            for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(from, 0f, Easing.SmoothStep(e / dur));
                yield return null;
            }

            bgmSource.clip = clip;
            bgmSource.volume = 0f;
            bgmSource.Play();

            for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(0f, target, Easing.SmoothStep(e / dur));
                yield return null;
            }
            bgmSource.volume = target;
            bgmFadeRoutine = null;
        }

        public void StopBGM()
        {
            if (bgmFadeRoutine != null) { StopCoroutine(bgmFadeRoutine); bgmFadeRoutine = null; }
            bgmSource.Stop();
        }

        public void PlaySE(AudioClip clip)
        {
            if (clip == null || seSource == null) return;
            seSource.PlayOneShot(clip, seVolume * GameSettings.Instance.masterVolume);
        }

        /// <summary>ボイスプールでピッチを変えて鳴らす（重ね発音・音色のばらつき用）。</summary>
        private void PlaySEPitched(AudioClip clip, float pitch)
        {
            if (clip == null) return;
            if (seVoices == null || seVoices.Length == 0) { PlaySE(clip); return; }
            AudioSource v = seVoices[seVoiceIndex];
            seVoiceIndex = (seVoiceIndex + 1) % seVoices.Length;
            v.pitch = pitch;
            v.PlayOneShot(clip, seVolume * GameSettings.Instance.masterVolume);
        }

        /// <summary>
        /// ビーム発射音。多数の艦が同時発砲しても音が氾濫しないよう実時間で間引き、
        /// 1発ごとにピッチを微妙に変えて機械的な反復感を避ける。
        /// </summary>
        public void PlayBeam()
        {
            if (seBeam == null) return;
            float now = Time.unscaledTime; // 倍速/ポーズに依らず一定密度に保つ
            if (now < nextBeamTime) return;
            nextBeamTime = now + Mathf.Max(0f, beamMinInterval);

            float pitch = 1f + Random.Range(-beamPitchJitter, beamPitchJitter);
            PlaySEPitched(seBeam, pitch);
        }

        public void PlayHit()       => PlaySE(seHit);
        public void PlayExplosion() => PlaySE(seExplosion);
        public void PlayUIClick()   => PlaySE(seUiClick);
    }
}
