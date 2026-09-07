// =============================================================================
// DesktopWindowMode — 게임 창을 '바탕화면 위젯'으로 바꿨다 되돌리는 스위치
// =============================================================================
// [역할] 유니티 창 하나를 런타임에 변형한다.
//        평소  : 보통 창(테두리·작업표시줄 있음)
//        바탕화면 모드 : 테두리 없는 투명 창 · 항상 위 · 작업표시줄에서 숨김 ·
//                        빈 곳은 클릭이 통과(바탕화면 아이콘이 그대로 눌림)
//
// [왜 되돌리기가 중요한가] 참고한 상주형 앱들은 켤 때 한 번 바꾸고 끝이라 복구 코드가 없다.
//        우리는 메인 게임 ↔ 바탕화면 모드를 오가므로, 진입 전 창 상태(스타일/위치/크기)를
//        전부 저장했다가 나갈 때 그대로 되돌린다.
//
// [클릭 통과] 매 프레임 커서 밑에 우리 UI가 있는지 보고 통과 여부를 토글한다.
//        UI 위 → 클릭을 받고, 빈 곳 → 바탕화면으로 흘려보낸다.
//
// [제약] 창 조작은 Windows 빌드에서만 동작한다(에디터에선 전부 무시).
//        에디터에서는 상태 전이와 로그만 확인할 수 있다.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>게임 창을 바탕화면 위젯 형태로 바꿨다 되돌리는 스위치(Windows 빌드 전용 동작).</summary>
[DisallowMultipleComponent]
public class DesktopWindowMode : MonoBehaviour
{
    public static DesktopWindowMode Instance { get; private set; }

    [Header("동작")]
    [Tooltip("켜면 시작하자마자 바탕화면 모드로 들어간다(바탕화면 전용 씬에서 사용).")]
    [SerializeField] private bool 시작시진입 = true;
    [Tooltip("빈 곳 클릭을 바탕화면으로 흘려보낸다. 끄면 창 전체가 클릭을 먹는다.")]
    [SerializeField] private bool 클릭통과 = true;
    [Tooltip("창을 화면 어느 쪽에 붙일지(작업표시줄을 뺀 작업 영역 기준).")]
    [SerializeField] private DockSide 위치 = DockSide.Bottom;
    [Tooltip("띠 높이(px). 화면 하단에 이 높이만큼만 창을 만든다.")]
    [SerializeField] private int 띠높이 = 140;

    public enum DockSide { Bottom, Top, FullScreen }

    /// <summary>지금 바탕화면 모드인지(요청 시점에 바로 켜진다).</summary>
    public bool IsDesktopMode { get; private set; }

    /// <summary>창 변형이 실제로 적용됐는지. 전환 가림막이 언제 걷힐지 판단하는 기준.</summary>
    public bool WindowApplied { get; private set; }

    /// <summary>모드가 바뀔 때 알린다(true=바탕화면 모드 진입).</summary>
    public event Action<bool> ModeChanged;

    // ── 커서 위치는 에디터/빌드 모두 필요 → #if 밖 ──
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int i, uint v);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint p, ref RECT pv, uint fw);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("Dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    private struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }

    private const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    private const uint WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000;
    private const uint WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080;
    private const uint SWP_SHOWWINDOW = 0x0040, SWP_FRAMECHANGED = 0x0020, SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001;
    private const uint SPI_GETWORKAREA = 0x0030;
    private const uint LWA_ALPHA = 0x00000002;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    private IntPtr hwnd;

    // 진입 전 원래 창 상태 — 나갈 때 이걸로 되돌린다.
    private bool saved;
    private uint savedStyle, savedExStyle;
    private RECT savedRect;

    private uint desktopExStyleBase;      // 바탕화면 모드의 기본 확장 스타일(통과 비트 제외)
    private bool currentlyClickThrough;
    private int winLeft, winTop;          // 창 좌상단(커서 좌표 보정용)
#endif

    // 전체화면 모드도 저장·복원 대상이다. 프로젝트 기본이 '전체화면 창'이라
    // 그대로 두면 창을 띠 크기로 못 줄인다. 전역 설정을 바꾸는 대신 런타임에만 바꾼다.
    private FullScreenMode savedFullScreenMode;
    private int savedWidth, savedHeight;
    private bool savedScreen;

    private Camera cam;
    private POINT lastCursor;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private readonly System.Text.StringBuilder diag = new System.Text.StringBuilder();

    // 진단용 — 좌표 변환과 히트 결과가 맞는지 눈으로 보려고 들고 있는다.
    private Vector2 probeLocal;
    private int probeHits;
    private string hoverName = "(없음)";
    private float probeTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Application.runInBackground = true;   // 포커스를 잃어도 계속 돌아야 한다
    }

    private void Start()
    {
        cam = Camera.main;
        if (시작시진입) EnterDesktopMode();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // 씬을 떠날 때 창을 원래대로 돌려놓지 않으면 다음 씬이 투명한 채로 남는다.
            if (IsDesktopMode) ExitDesktopMode();
            Instance = null;
        }
    }

    // ── 전환 ──────────────────────────────────────────────────────────

    /// <summary>바탕화면 모드로 들어간다(이미 들어와 있으면 무시).</summary>
    public void EnterDesktopMode()
    {
        if (IsDesktopMode) return;
        IsDesktopMode = true;

        // 전체화면이면 창을 띠 크기로 못 줄인다 → 먼저 창 모드로 내린다.
        savedFullScreenMode = Screen.fullScreenMode;
        savedWidth = Screen.width;
        savedHeight = Screen.height;
        savedScreen = true;
        if (Screen.fullScreenMode != FullScreenMode.Windowed)
            Screen.fullScreenMode = FullScreenMode.Windowed;
        L($"enter: fullScreenMode {savedFullScreenMode} -> Windowed, size {savedWidth}x{savedHeight}");

        // 전체화면 해제는 다음 프레임에 반영되므로, 창 조작은 한 프레임 뒤에 한다.
        StartCoroutine(ApplyDesktopWindowNextFrame());
    }

    private System.Collections.IEnumerator ApplyDesktopWindowNextFrame()
    {
        yield return null;
        ApplyDesktopWindow();
        WindowApplied = true;
        WriteDiag();
        ModeChanged?.Invoke(true);
    }

    private void ApplyDesktopWindow()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero) { L("창 핸들을 못 찾았다"); }
        else
        {
            // ① 원래 상태 저장 — 되돌리기의 전부다
            savedStyle = GetWindowLong(hwnd, GWL_STYLE);
            savedExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            GetWindowRect(hwnd, out savedRect);
            saved = true;

            // ② 작업 영역(작업표시줄 제외) 안에서 띠 위치 계산
            RECT wa = new RECT();
            SystemParametersInfo(SPI_GETWORKAREA, 0, ref wa, 0);
            int waW = wa.right - wa.left, waH = wa.bottom - wa.top;
            int h = 위치 == DockSide.FullScreen ? waH : Mathf.Clamp(띠높이, 40, waH);
            int y = 위치 == DockSide.Bottom ? wa.bottom - h : wa.top;
            winLeft = wa.left; winTop = y;

            // ③ 테두리 없는 투명 창으로 변형
            SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
            desktopExStyleBase = savedExStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW;
            currentlyClickThrough = 클릭통과;
            SetWindowLong(hwnd, GWL_EXSTYLE, desktopExStyleBase | (currentlyClickThrough ? WS_EX_TRANSPARENT : 0));

            // ★ 레이어드 창은 이 호출이 있어야 화면에 합성된다.
            //   빠뜨리면 창은 만들어졌는데 아무것도 안 그려진다(투명이 아니라 '없음').
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);

            // DWM에게 "여백을 클라이언트 영역까지" → 알파 0 픽셀이 진짜로 뚫린다
            MARGINS m = new MARGINS { cxLeftWidth = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);

            SetWindowPos(hwnd, HWND_TOPMOST, wa.left, y, waW, h, SWP_SHOWWINDOW | SWP_FRAMECHANGED);
            L($"hwnd={hwnd} workArea=({wa.left},{wa.top},{wa.right},{wa.bottom}) 배치=({wa.left},{y},{waW},{h})");
            L($"style {savedStyle:X} -> {GetWindowLong(hwnd, GWL_STYLE):X} / exStyle {savedExStyle:X} -> {GetWindowLong(hwnd, GWL_EXSTYLE):X}");
        }
#endif
        L($"바탕화면 모드 진입 (위치={위치}, 높이={띠높이})");
    }

    /// <summary>보통 창으로 되돌린다.</summary>
    public void ExitDesktopMode()
    {
        if (!IsDesktopMode) return;
        IsDesktopMode = false;
        WindowApplied = false;

        bool backToFullscreen = savedScreen && savedFullScreenMode != FullScreenMode.Windowed;

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (hwnd != IntPtr.Zero && saved)
        {
            SetWindowLong(hwnd, GWL_STYLE, savedStyle);
            SetWindowLong(hwnd, GWL_EXSTYLE, savedExStyle);

            // DWM 여백 원복(0이면 투명 확장 해제)
            MARGINS m = new MARGINS();
            DwmExtendFrameIntoClientArea(hwnd, ref m);

            // 전체화면으로 돌아갈 때는 SetWindowPos 를 생략한다.
            // Screen.SetResolution 이 어차피 창을 다시 잡으므로, 둘 다 하면
            // 리사이즈가 두 번 일어나 화면이 한 번 더 늘어났다 줄어든다.
            if (!backToFullscreen)
            {
                SetWindowPos(hwnd, HWND_NOTOPMOST,
                    savedRect.left, savedRect.top,
                    savedRect.right - savedRect.left, savedRect.bottom - savedRect.top,
                    SWP_SHOWWINDOW | SWP_FRAMECHANGED);
            }
            else
            {
                // 항상위만 먼저 풀어둔다(크기·위치는 SetResolution 에 맡긴다).
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
        }
#endif
        if (backToFullscreen)
            Screen.SetResolution(savedWidth, savedHeight, savedFullScreenMode);

        L($"보통 창으로 복귀 (전체화면복원={backToFullscreen})");
        WriteDiag();
        ModeChanged?.Invoke(false);
    }

    /// <summary>토글.</summary>
    public void Toggle()
    {
        if (IsDesktopMode) ExitDesktopMode(); else EnterDesktopMode();
    }

    // ── 클릭 통과 ─────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsDesktopMode) return;

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        // ── 키보드 탈출구 ──
        // UI 클릭이 안 먹어도 빠져나올 수 있어야 한다. GetAsyncKeyState 는 포커스와 무관하게 읽힌다.
        bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
        if (ctrl && (GetAsyncKeyState(0x44) & 0x8000) != 0)      // Ctrl+D → 메인으로
        {
            L("Ctrl+D — 키보드로 복귀 요청");
            WriteDiag();
            if (DesktopModeRouter.Instance != null) DesktopModeRouter.Instance.Restore();
            return;
        }
        if (ctrl && (GetAsyncKeyState(0x51) & 0x8000) != 0)      // Ctrl+Q → 종료
        {
            L("Ctrl+Q — 종료");
            WriteDiag();
            Application.Quit();
            return;
        }

        if (!클릭통과 || hwnd == IntPtr.Zero) return;

        bool overUI = IsCursorOverOurUI();
        if (overUI == currentlyClickThrough)   // 상태가 뒤집혀야 할 때만 손댄다
        {
            currentlyClickThrough = !overUI;
            SetWindowLong(hwnd, GWL_EXSTYLE, desktopExStyleBase | (currentlyClickThrough ? WS_EX_TRANSPARENT : 0));
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            L($"클릭통과 {(currentlyClickThrough ? "켬" : "끔")} (hover={hoverName})");
            WriteDiag();
        }

        // ── 상시 진단 ──
        // 마우스가 어디를 가리키는지 / 좌표 변환이 맞는지 주기적으로 남긴다.
        probeTimer += Time.unscaledDeltaTime;
        if (probeTimer >= 0.5f)
        {
            probeTimer = 0f;
            // ※ 이 프로젝트는 새 Input System 전용(activeInputHandler=1)이라
            //    구형 Input.mousePosition 은 예외를 던진다. 커서는 GetCursorPos 로만 읽는다.
            L($"probe cursor=({lastCursor.X},{lastCursor.Y}) win=({winLeft},{winTop}) " +
              $"screen={Screen.width}x{Screen.height} local=({probeLocal.x:F0},{probeLocal.y:F0}) " +
              $"hits={probeHits} hover={hoverName} 통과={currentlyClickThrough} " +
              $"module={(EventSystem.current != null && EventSystem.current.currentInputModule != null ? EventSystem.current.currentInputModule.GetType().Name : "없음")} " +
              $"focus={Application.isFocused}");
            WriteDiag();
        }
#endif
    }

    // ── 진단 ──────────────────────────────────────────────────────────
    // 빌드에선 콘솔을 볼 수 없다. exe 옆 desktop_mode_debug.txt 로 남긴다.

    private void L(string s)
    {
        diag.AppendLine($"[{Time.realtimeSinceStartup:F2}] {s}");
        // 0.5초마다 찍히므로 무한정 자라지 않게 앞부분을 버린다.
        if (diag.Length > 24000) diag.Remove(0, diag.Length - 16000);
        Debug.Log("[Desktop] " + s);
    }

    private void WriteDiag()
    {
        try
        {
            string dir = System.IO.Directory.GetParent(Application.dataPath).FullName;
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "desktop_mode_debug.txt"), diag.ToString());
        }
        catch { /* 진단이 본 기능을 막으면 안 된다 */ }
    }

    /// <summary>커서가 우리 UI(Graphic Raycaster가 잡는 것) 위에 있는지.</summary>
    private bool IsCursorOverOurUI()
    {
        if (EventSystem.current == null) { hoverName = "(EventSystem 없음)"; return false; }
        GetCursorPos(out lastCursor);

        float x = lastCursor.X, y = lastCursor.Y;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        x -= winLeft; y -= winTop;             // 화면 절대좌표 → 창 기준
#endif
        // Win32는 위→아래, 유니티는 아래→위
        probeLocal = new Vector2(x, Screen.height - y);
        var data = new PointerEventData(EventSystem.current) { position = probeLocal };
        raycastResults.Clear();
        EventSystem.current.RaycastAll(data, raycastResults);
        probeHits = raycastResults.Count;
        hoverName = probeHits > 0 ? raycastResults[0].gameObject.name : "(없음)";
        return probeHits > 0;
    }
}
