// =============================================================================
// DesktopModeRouter — 메인 게임 ↔ 바탕화면 모드 왕복 담당
// =============================================================================
// [역할] 최소화/돌아가기 요청을 받아 씬을 갈아타고, 그 사이의 지저분한 구간을 가려준다.
//        창 모양 변경은 DesktopWindowMode 가 하고, 이쪽은 '순서'와 '가림막'을 맡는다.
//
// [왜 씬을 갈아치우나] 무역 진행도는 FrameworkRoot(DontDestroyOnLoad)가 들고 있어
//        씬이 바뀌어도 계속 굴러간다. 메인 씬을 메모리에 안고 있을 이유가 없다.
//
// [왜 가림막이 필요한가] 창 크기가 140px 띠 ↔ 전체화면으로 바뀌는 동안,
//        아직 살아 있는 이전 씬이 새 크기에 늘어나 그려진다(찌그러짐).
//        백버퍼 재생성은 몇 프레임 걸려서 없앨 수 없으므로, 그 구간을 덮는다.
//        가림막은 씬 전환을 넘어 살아 있어야 하므로 이 오브젝트가 직접 들고 있다.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>메인 게임과 바탕화면 위젯 사이를 오가는 라우터(씬 전환 + 전환 가림).</summary>
[DisallowMultipleComponent]
public class DesktopModeRouter : MonoBehaviour
{
    private const string DesktopSceneName = "Desktop";
    private const string DefaultReturnScene = "InGame";
    private const string ReturnSceneKey = "desktop.returnScene";

    // 가림막 타이밍. 짧게 덮고 빠지되, 창 리사이즈가 안정될 시간은 준다.
    private const float FadeInSeconds = 0.12f;
    private const float FadeOutSeconds = 0.22f;
    private const int SettleFrames = 3;      // 리사이즈가 반영될 때까지 기다릴 프레임

    private static DesktopModeRouter instance;

    private Canvas veilCanvas;
    private CanvasGroup veil;
    private bool busy;

    /// <summary>없으면 만들어서 돌려준다(씬 어디서든 버튼 하나로 부를 수 있게).</summary>
    public static DesktopModeRouter Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<DesktopModeRouter>();
            if (instance == null)
            {
                var go = new GameObject(nameof(DesktopModeRouter));
                instance = go.AddComponent<DesktopModeRouter>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildVeil();
    }

    // ── 전환 ──────────────────────────────────────────────────────────

    /// <summary>지금 씬을 기억하고 바탕화면 위젯으로 내려간다.</summary>
    public void Minimize()
    {
        if (busy) return;
        string current = SceneManager.GetActiveScene().name;
        if (current == DesktopSceneName) return;      // 이미 위젯

        PlayerPrefs.SetString(ReturnSceneKey, current);
        PlayerPrefs.Save();
        Debug.Log($"[DesktopRouter] 최소화 — 돌아올 씬 '{current}' 기억");
        StartCoroutine(SwitchTo(DesktopSceneName, exitDesktopFirst: false));
    }

    /// <summary>기억해둔 씬으로 되돌아간다.</summary>
    public void Restore()
    {
        if (busy) return;
        string target = PlayerPrefs.GetString(ReturnSceneKey, DefaultReturnScene);
        if (string.IsNullOrEmpty(target) || target == DesktopSceneName)
            target = DefaultReturnScene;

        Debug.Log($"[DesktopRouter] 복귀 — '{target}' 로 이동");
        StartCoroutine(SwitchTo(target, exitDesktopFirst: true));
    }

    /// <summary>
    /// 가림막을 덮은 상태에서 창 모양을 바꾸고 씬을 갈아탄다.
    /// 순서가 핵심이다 — 덮기 → 창 변경 → 안정 대기 → 씬 로드 → 걷기.
    /// </summary>
    private IEnumerator SwitchTo(string sceneName, bool exitDesktopFirst)
    {
        busy = true;
        yield return Fade(0f, 1f, FadeInSeconds);

        // 창 모양 변경은 가림막 아래에서 한다(찌그러지는 구간을 안 보이게).
        if (exitDesktopFirst && DesktopWindowMode.Instance != null)
            DesktopWindowMode.Instance.ExitDesktopMode();

        // 백버퍼 재생성이 반영될 때까지 몇 프레임 넘긴다.
        for (int i = 0; i < SettleFrames; i++) yield return null;

        // 무거운 씬은 동기 로드 시 한 번 멈춘다 → 비동기로 넘기고 가림막을 유지한다.
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null) { while (!op.isDone) yield return null; }

        // 바탕화면 씬으로 들어가는 경우, 그 씬의 스위치가 창을 띠로 바꾸는 것까지
        // 기다렸다가 걷어야 한다(안 그러면 전체화면 상태가 한 번 비친다).
        if (!exitDesktopFirst)
        {
            float wait = 0f;
            while (wait < 1.5f)
            {
                var m = DesktopWindowMode.Instance;
                if (m != null && m.WindowApplied) break;   // 요청이 아니라 '적용 완료'를 기다린다
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < SettleFrames; i++) yield return null;
        }

        // 새 씬이 첫 프레임을 그릴 때까지 기다렸다가 걷는다.
        for (int i = 0; i < SettleFrames; i++) yield return null;

        yield return Fade(1f, 0f, FadeOutSeconds);
        busy = false;
    }

    // ── 가림막 ────────────────────────────────────────────────────────

    private void BuildVeil()
    {
        var go = new GameObject("TransitionVeil");
        go.transform.SetParent(transform, false);

        veilCanvas = go.AddComponent<Canvas>();
        veilCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        veilCanvas.sortingOrder = 32760;          // 무엇보다 위에
        go.AddComponent<CanvasScaler>();

        veil = go.AddComponent<CanvasGroup>();
        veil.alpha = 0f;
        veil.blocksRaycasts = false;
        veil.interactable = false;

        var imgGo = new GameObject("Black");
        imgGo.transform.SetParent(go.transform, false);
        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;              // 창 크기가 바뀌어도 항상 꽉 채운다
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = imgGo.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (veil == null) yield break;
        veil.blocksRaycasts = true;               // 전환 중 클릭이 새 나가지 않게
        float t = 0f;
        veil.alpha = from;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;          // 게임 일시정지 중에도 돌아야 한다
            veil.alpha = Mathf.Lerp(from, to, seconds <= 0f ? 1f : t / seconds);
            yield return null;
        }
        veil.alpha = to;
        veil.blocksRaycasts = to > 0.01f;
    }
}
