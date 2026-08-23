using System.Collections.Generic;
using ND.Framework;
using ND.UI.Title;
using UnityEngine;

namespace ND.Audio
{
    public sealed class TradeSoundController : MonoBehaviour
    {
        private const string RootObjectName = "TradeSoundController";

        private static TradeSoundController instance;
        private readonly Dictionary<(string caravanId, string tradeId), JourneyResultGrade> settlementGrades = new();
        private SoundCatalog catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (instance != null) return;
            new GameObject(RootObjectName).AddComponent<TradeSoundController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            catalog = Resources.Load<SoundCatalog>(SoundCatalog.ResourceName);
        }

        private void OnEnable()
        {
            FrameworkEvents.TradeStarted += HandleTradeStarted;
            FrameworkEvents.TradeSettlementCreated += HandleTradeSettlementCreated;
            FrameworkEvents.TradeClaimed += HandleTradeClaimed;
        }

        private void OnDisable()
        {
            FrameworkEvents.TradeStarted -= HandleTradeStarted;
            FrameworkEvents.TradeSettlementCreated -= HandleTradeSettlementCreated;
            FrameworkEvents.TradeClaimed -= HandleTradeClaimed;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void HandleTradeStarted(string caravanId, string tradeId)
        {
            Play(catalog?.TradeSounds?.DepartSoundId);
        }

        private void HandleTradeSettlementCreated(string caravanId, string tradeId, JourneyResultData result)
        {
            if (result != null)
            {
                settlementGrades[(caravanId, tradeId)] = result.grade;
            }

            string soundId = result != null && result.grade == JourneyResultGrade.Failed
                ? catalog?.TradeSounds?.FailedSoundId
                : catalog?.TradeSounds?.SettlementReadySoundId;
            Play(soundId);
        }

        private void HandleTradeClaimed(string caravanId, string tradeId)
        {
            var key = (caravanId, tradeId);
            if (!settlementGrades.TryGetValue(key, out JourneyResultGrade grade)) return;

            settlementGrades.Remove(key);
            if (grade == JourneyResultGrade.Failed) return;

            Play(catalog?.TradeSounds?.ClaimSoundId);
        }

        private static void Play(string soundId)
        {
            if (!string.IsNullOrEmpty(soundId)) SoundManager.Instance?.PlaySfx(soundId);
        }
    }
}
