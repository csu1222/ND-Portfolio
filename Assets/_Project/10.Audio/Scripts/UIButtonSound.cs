using ND.UI.Title;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Audio
{
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class UIButtonSound : MonoBehaviour
    {
        [Tooltip("클릭할 때 UI SFX 채널로 재생할 SoundCatalog ID입니다.")]
        [SerializeField] private string soundId;
        [Tooltip("SoundCatalog의 기본 UI 버튼 사운드를 사용합니다.")]
        [SerializeField] private bool useDefaultSound = true;

        private Button button;
        private SoundCatalog catalog;

        private void Awake()
        {
            button = GetComponent<Button>();
            catalog = Resources.Load<SoundCatalog>(SoundCatalog.ResourceName);
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.AddListener(PlayClickSound);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(PlayClickSound);
        }

        private void PlayClickSound()
        {
            SoundManager manager = SoundManager.Instance;
            if (manager == null) return;

            string selectedSoundId = useDefaultSound
                ? catalog?.UiSounds?.DefaultButtonSoundId
                : soundId;
            if (!string.IsNullOrEmpty(selectedSoundId)) manager.PlayUiSfx(selectedSoundId);
        }
    }
}
