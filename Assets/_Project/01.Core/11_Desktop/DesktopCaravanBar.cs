// =============================================================================
// DesktopCaravanBar — 바탕화면 띠에 캐러밴 진행도를 그린다
// =============================================================================
// [화면]
//     [1] [2] [3] [4]
//     바람마을 ────🐴──────── 소금마을        도착까지 12분
//
// [데이터] 전부 이미 있는 것을 읽기만 한다. 새로 계산하는 값이 없다.
//     진행도   TreadmillRouteSampler.TryGetProgress(caravanId)   ← 시간 기반, 껐다 켜도 맞음
//     마을     SharedRouteDefinition.FromTownId / ToTownId → TryGetTown → DisplayName
//     도착     TradeProgressSaveData.expectedTradeEndUtcTick − 지금
//
// [주의] 이 씬은 FrameworkRoot 가 DontDestroyOnLoad 로 살아 있는 상태에서 열린다.
//        그래서 씬이 바뀌어도 무역은 계속 굴러가고, 여기서는 읽기만 하면 된다.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>바탕화면 띠에 선택한 캐러밴의 진행도를 표시한다(읽기 전용).</summary>
public class DesktopCaravanBar : MonoBehaviour
{
    [Header("선 위 표식")]
    [Tooltip("진행도에 따라 좌우로 움직일 캐러밴 표식.")]
    [SerializeField] private RectTransform 캐러밴표식;
    [Tooltip("표식이 움직일 범위가 되는 선.")]
    [SerializeField] private RectTransform 진행선;

    [Header("글자")]
    [SerializeField] private TextMeshProUGUI 출발마을;
    [SerializeField] private TextMeshProUGUI 도착마을;
    [SerializeField] private TextMeshProUGUI 상태글;

    [Header("슬롯 버튼")]
    [Tooltip("캐러밴 1~4를 고르는 버튼들. 순서대로 슬롯 1,2,3,4.")]
    [SerializeField] private Button[] 슬롯버튼 = new Button[0];

    [Header("갱신")]
    [Tooltip("초당 몇 번 갱신할지. 상주 위젯이라 낮게 잡는다.")]
    [SerializeField] private float 갱신주기 = 0.5f;

    private int 선택슬롯 = 1;
    private float timer;

    private void Start()
    {
        for (int i = 0; i < 슬롯버튼.Length; i++)
        {
            int slot = i + 1;                       // 클로저에 담을 지역 복사
            if (슬롯버튼[i] != null) 슬롯버튼[i].onClick.AddListener(() => 슬롯선택(slot));
        }
        Refresh();
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < 갱신주기) return;
        timer = 0f;
        Refresh();
    }

    private void 슬롯선택(int slot)
    {
        선택슬롯 = slot;
        Refresh();
    }

    // ── 표시 갱신 ────────────────────────────────────────────────────

    private void Refresh()
    {
        UpdateSlotButtons();

        string caravanId = ResolveCaravanId(선택슬롯);
        if (string.IsNullOrEmpty(caravanId)) { ShowIdle("캐러밴 없음"); return; }

        var entry = FindTravelingProgress(caravanId);
        if (entry == null) { ShowIdle("정박 중"); return; }

        // 진행도 — 이동 중이 아니면 위에서 걸러졌으므로 여기선 항상 성공한다.
        float p = 0f;
        TreadmillRouteSampler.TryGetProgress(caravanId, out p);
        PlaceMarker(p);

        // 마을 이름
        var shared = ND.Framework.FrameworkRoot.Instance != null
            ? ND.Framework.FrameworkRoot.Instance.SharedGameData : null;
        string from = "?", to = "?";
        if (shared != null && !string.IsNullOrEmpty(entry.activeRouteId)
            && shared.TryGetRoute(entry.activeRouteId, out var route) && route != null)
        {
            from = TownName(shared, route.FromTownId);
            to = TownName(shared, route.ToTownId);
        }
        SetText(출발마을, from);
        SetText(도착마을, to);

        SetText(상태글, $"{Mathf.RoundToInt(p * 100f)}%  ·  도착까지 {RemainingText(entry)}");
    }

    private void ShowIdle(string message)
    {
        PlaceMarker(0f);
        SetText(출발마을, "—");
        SetText(도착마을, "—");
        SetText(상태글, message);
    }

    /// <summary>진행도(0~1)를 선 위 가로 위치로 옮긴다.</summary>
    private void PlaceMarker(float p)
    {
        if (캐러밴표식 == null || 진행선 == null) return;
        float w = 진행선.rect.width;
        var pos = 캐러밴표식.anchoredPosition;
        // 선의 중앙을 기준으로 좌(−w/2) → 우(+w/2)
        pos.x = Mathf.Lerp(-w * 0.5f, w * 0.5f, Mathf.Clamp01(p));
        캐러밴표식.anchoredPosition = pos;
    }

    private void UpdateSlotButtons()
    {
        for (int i = 0; i < 슬롯버튼.Length; i++)
        {
            if (슬롯버튼[i] == null) continue;
            int slot = i + 1;
            bool traveling = !string.IsNullOrEmpty(ResolveCaravanId(slot))
                             && FindTravelingProgress(ResolveCaravanId(slot)) != null;

            // 이동 중이 아닌 슬롯은 흐리게 — 누를 수는 있게 둔다(정박 중 표시를 보려고).
            var img = 슬롯버튼[i].GetComponent<Image>();
            if (img != null)
            {
                img.color = slot == 선택슬롯
                    ? new Color(0.35f, 0.30f, 0.14f, 0.95f)          // 선택됨
                    : (traveling ? new Color(0.17f, 0.15f, 0.12f, 0.95f)
                                 : new Color(0.13f, 0.12f, 0.11f, 0.6f));
            }
        }
    }

    // ── 데이터 읽기 ──────────────────────────────────────────────────

    /// <summary>슬롯 번호(1~4)에 해당하는 caravanId. 없으면 빈 문자열.</summary>
    private static string ResolveCaravanId(int slot)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) return string.Empty;

        foreach (var c in save.caravans)
        {
            if (c == null) continue;
            // 슬롯 표기가 0-based / 1-based 양쪽으로 쓰여 온 이력이 있어 둘 다 받는다.
            if (c.slotIndex + 1 == slot || c.slotIndex == slot) return c.caravanId;
        }
        return string.Empty;
    }

    /// <summary>이동 중인 무역 진행 항목. 이동 중이 아니면 null.</summary>
    private static ND.Framework.TradeProgressSaveData FindTravelingProgress(string caravanId)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.tradeProgressEntries == null || string.IsNullOrEmpty(caravanId)) return null;

        foreach (var e in save.tradeProgressEntries)
            if (e != null && e.caravanId == caravanId
                && e.state == ND.Framework.TradeProgressState.Traveling) return e;
        return null;
    }

    private static string TownName(ND.Framework.ISharedGameDataProvider shared, string townId)
    {
        if (shared == null || string.IsNullOrEmpty(townId)) return "?";
        return shared.TryGetTown(townId, out var town) && town != null && !string.IsNullOrEmpty(town.DisplayName)
            ? town.DisplayName : townId;
    }

    /// <summary>도착까지 남은 시간을 사람이 읽는 문구로.</summary>
    private static string RemainingText(ND.Framework.TradeProgressSaveData entry)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        long now = (fr != null && fr.GameTime != null) ? fr.GameTime.CurrentUtc.Ticks : System.DateTime.UtcNow.Ticks;
        long left = entry.expectedTradeEndUtcTick - now;
        if (left <= 0) return "곧 도착";

        var span = System.TimeSpan.FromTicks(left);
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}시간 {span.Minutes}분";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}분";
        return $"{span.Seconds}초";
    }

    private static void SetText(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
}
