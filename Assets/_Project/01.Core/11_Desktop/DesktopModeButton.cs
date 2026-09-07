// =============================================================================
// DesktopModeButton — 최소화 / 돌아가기 버튼에 붙이는 얇은 어댑터
// =============================================================================
// [역할] UI Button 의 OnClick 에 인스펙터로 연결할 것 없이, 이 컴포넌트만 붙이면 동작한다.
//        같은 오브젝트의 Button 을 찾아 자동으로 연결한다.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>버튼에 붙여 바탕화면 모드로 내려가거나 돌아오게 하는 어댑터.</summary>
[RequireComponent(typeof(Button))]
public class DesktopModeButton : MonoBehaviour
{
    public enum Action { 최소화, 돌아가기 }

    [Tooltip("이 버튼이 할 일.")]
    [SerializeField] private Action 동작 = Action.최소화;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (동작 == Action.최소화) DesktopModeRouter.Instance.Minimize();
        else DesktopModeRouter.Instance.Restore();
    }
}
