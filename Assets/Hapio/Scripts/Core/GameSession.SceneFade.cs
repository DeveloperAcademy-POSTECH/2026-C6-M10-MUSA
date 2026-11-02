using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// 방에 들어갈 때의 화면 전환 (#418). 방을 만들거나 들어가 Home에서 자리 고르기(Lobby) 화면으로 넘어갈 때, 방이 열린
    /// 순간 화면을 바로 검게 가리고 곧장 Lobby 씬을 불러온 뒤, 자리 고르기 화면이 그려지면 밝아진다. Inspector에서 어두워지는
    /// 시간을 주면 Home 화면을 그대로 둔 채(GameLobbyView가 HoldsHomeForFade를 본다) 그만큼 어두워진 뒤에 씬을 바꾼다.
    /// 가리는 동안에는 눌리지 않는다. 그사이 입장이 실패해 Home에 남으면 다시 밝아진다.
    /// </summary>
    public sealed partial class GameSession
    {
        [Header("방에 들어갈 때 화면 전환 (#418)")]
        [Tooltip("Home에서 자리 고르기 화면으로 넘어가기 전에 어두워지는 시간 (초). 0(기본)이면 방이 열린 순간 바로 가리고 씬을 바꾼다.")]
        [SerializeField, Min(0f)] private float lobbyFadeOutSeconds;
        [Tooltip("자리 고르기 화면이 뜬 뒤 밝아지는 시간 (초). 0이면 바로 걷힌다.")]
        [SerializeField, Min(0f)] private float lobbyFadeInSeconds = .35f;
        [SerializeField] private Color lobbyFadeColor = Color.black;
        // 씬을 불러온 직후의 긴 프레임에 막이 한 번에 걷히지 않게, 밝아질 때 한 프레임에 쓰는 시간을 줄인다.
        private const float MaxFadeInStep = 1f / 30f;
        private GameSceneFade sceneFade;
        private Coroutine fadeInRoutine;
        private bool holdHomeForFade;

        public GameSceneFade SceneFade => sceneFade;
        /// <summary>Home 화면이 어두워지는 중이라 지금 모습 그대로 두어야 하는지.</summary>
        public bool HoldsHomeForFade => holdHomeForFade;

        private bool FadesIntoLobby(string path) => sceneFade != null && path == GameScenePaths.Lobby
            && SceneManager.GetActiveScene().path == GameScenePaths.Home;

        /// <summary>Home을 그대로 둔 채 어두워진다. 그사이 다시 Home에 머물게 되면(입장 실패·취소) 멈춘다.</summary>
        private IEnumerator FadeOutOfHome()
        {
            if (fadeInRoutine != null) { StopCoroutine(fadeInRoutine); fadeInRoutine = null; }
            float alpha = sceneFade.Alpha;
            while (alpha < 1f && !quitting && wantedScene != SceneManager.GetActiveScene().path)
            {
                alpha = lobbyFadeOutSeconds <= 0f ? 1f : alpha + Time.unscaledDeltaTime / lobbyFadeOutSeconds;
                sceneFade.Set(alpha, true);
                if (alpha < 1f) yield return null;
            }
        }

        /// <summary>새 화면(또는 입장이 실패해 남은 Home)을 보여 주며 밝아진다. 밝아지는 동안에는 눌림을 막지 않는다.</summary>
        private void FadeIntoScene()
        {
            holdHomeForFade = false;
            if (sceneFade == null || !sceneFade.Visible || quitting) return;
            if (fadeInRoutine != null) StopCoroutine(fadeInRoutine);
            fadeInRoutine = StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            float alpha = sceneFade.Alpha;
            while (alpha > 0f)
            {
                alpha = lobbyFadeInSeconds <= 0f ? 0f
                    : alpha - Mathf.Min(Time.unscaledDeltaTime, MaxFadeInStep) / lobbyFadeInSeconds;
                sceneFade.Set(alpha, false);
                if (alpha > 0f) yield return null;
            }
            fadeInRoutine = null;
        }
    }
}
