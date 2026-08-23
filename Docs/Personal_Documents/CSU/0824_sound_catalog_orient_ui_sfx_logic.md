# SoundCatalog 기반 UI 및 Trade SFX 로직

## 1. 개요

프로젝트의 BGM, SFX, UI SFX는 `SoundCatalog`에서 통합 관리한다.

기존 Scene별 BGM 설정을 유지하면서 다음 기능을 추가하였다.

- 일반 UI 버튼의 기본 클릭음 설정
- 특수 UI 버튼의 개별 SFX Override
- 선택한 UI에 `UIButtonSound` 일괄 적용
- 무역 상태에 따른 Gameplay SFX 재생

전체 구조는 다음과 같다.

```text
SoundCatalog
├─ Definitions
├─ Scene BGM Mappings
├─ UI Sounds
│  └─ Default Button Sound
└─ Trade Sounds
   ├─ Depart
   ├─ Settlement Ready
   ├─ Claim
   └─ Failed
        │
        ▼
   SoundManager
   ├─ BGM
   ├─ SFX
   └─ UI SFX
```

---

## 2. UI SFX

### 기본 버튼

일반 버튼은 개별 Sound ID를 입력하지 않고 `SoundCatalog`의 기본 UI Sound를 사용한다.

```text
Button Click
→ UIButtonSound
→ SoundCatalog.DefaultButtonSoundId
→ SoundManager.PlayUiSfx
```

`UIButtonSound`:

```text
Use Default UI Sound = true
```

로 설정한다.

### 특수 버튼

Confirm, Cancel 등 별도 효과음이 필요한 버튼은:

```text
Use Default UI Sound = false
→ Override Sound 선택
```

으로 설정한다.

Override Sound는 Inspector Dropdown에서 `UiSfx` Category의 Sound ID 중 선택할 수 있다.

### 일괄 적용

여러 Button에 기본 설정을 적용할 때:

```text
Tools
→ Audio
→ Apply Default UI Sound To Selection
```

을 사용한다.

선택한 UI Root 하위의 Button에 `UIButtonSound`를 추가하며, 이미 컴포넌트가 있는 Button은 기존 설정을 유지한다.

---

## 3. Trade SFX

Trade SFX는 버튼 클릭이 아니라 **실제 Gameplay 결과가 확정된 시점**에 재생한다.

```text
Framework Trade Event
→ TradeSoundController
→ SoundCatalog Trade Sound
→ SoundManager.PlaySfx
```

### 출발

```text
TradeStartService
→ 출발 성공
→ Save 성공
→ TradeStarted
→ Depart SFX
```

출발 검증 또는 Save가 실패하면 Depart SFX를 재생하지 않는다.

### 정산

```text
TradeSettlementCreated
├─ Success / PartialSuccess
│  → Settlement Ready SFX
│
└─ Failed
   → Failed SFX
```

Settlement Ready와 Failed SFX는 동시에 재생하지 않는다.

### Claim

```text
Success / PartialSuccess
→ Claim 성공
→ Claim SFX

Failed
→ Claim 성공
→ Gameplay Claim SFX 없음
```

실패 무역도 Framework상 Claim은 정상적으로 수행하지만, 실패 후 긍정적인 Claim 효과음이 재생되지 않도록 Audio 계층에서 구분한다.

버튼 자체의 UI Click SFX는 별도로 재생할 수 있다.

---

## 4. Trade 결과 캐시

`TradeSoundController`는 실패 Claim 여부를 판단하기 위해 Settlement 결과를 임시 저장한다.

멀티 캐러반 간 결과가 섞이지 않도록:

```text
(caravanId, full tradeId)
```

를 Key로 사용한다.

```text
TradeSettlementCreated
→ Grade Cache 저장

TradeClaimed
→ 동일 Trade Grade 확인
→ SFX 결정
→ Cache 제거
```

---

## 5. Restore 정책

저장된 `SettlementPending`을 불러오는 것은 새로운 Gameplay 결과가 아니므로 SFX를 다시 재생하지 않는다.

```text
SettlementPending
→ 게임 종료
→ Continue
→ Pending 복구
→ Settlement / Failed SFX 재재생 없음
```

복구된 Trade는 현재 세션의 Grade Cache가 없으므로 Claim 시에도 Gameplay Claim SFX를 생략한다.

---

## 6. 주요 클래스

| 클래스 | 역할 |
|---|---|
| `SoundDefinition` | Sound ID, Clip, Category, Volume, Pitch 정의 |
| `SoundCatalog` | BGM/UI/Trade Sound 설정 관리 |
| `SoundManager` | 실제 Audio 재생 |
| `UIButtonSound` | Button Click → UI SFX 연결 |
| `UIButtonSoundEditor` | Override Sound Dropdown 제공 |
| `UiSoundSetupTool` | UIButtonSound 일괄 적용 |
| `TradeSoundController` | Trade Event → Gameplay SFX 연결 |
| `TradeStartService` | 출발 성공 후 `TradeStarted` 발행 |
| `TradeProgressCoordinator` | Settlement / Claim 이벤트 발행 |

---

## 7. 핵심 설계 원칙

### UI 입력과 Gameplay 결과 분리

```text
Button Click
→ UI SFX

실제 Trade 성공
→ Gameplay SFX
```

따라서 버튼을 눌렀지만 거래가 실패한 경우 성공 Gameplay SFX가 재생되지 않는다.

### Sound 설정 중앙화

사운드 파일과 상황별 Sound ID는 `SoundCatalog`에서 관리하여 개별 호출부가 AudioClip에 직접 의존하지 않는다.

### Framework와 Audio 분리

Framework는 Trade 상태 변화 이벤트만 전달하고, 어떤 Sound를 재생할지는 `TradeSoundController`가 결정한다.

### Multi-Caravan 격리

Trade별 Audio 상태는:

```text
caravanId + full tradeId
```

기준으로 관리하여 서로 다른 캐러반의 정산 결과가 섞이지 않도록 한다.

---

## 8. 최종 흐름

```text
                     SoundCatalog
                          │
          ┌───────────────┼───────────────┐
          │               │               │
      Scene BGM        UI Sound        Trade Sound
          │               │               │
          ▼               ▼               ▼
     SoundManager    UIButtonSound   TradeSoundController
                          ▲               ▲
                          │               │
                     Button Click    FrameworkEvents
```

**핵심은 `SoundCatalog`에서 사운드 정책을 중앙 관리하고, `UIButtonSound`는 사용자 입력을, `TradeSoundController`는 실제 게임 상태 변화를 각각 SFX 재생으로 연결하는 구조이다.**