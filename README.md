# 누워서 돈벌기

방치형 무역·경영 시뮬레이션 프로젝트

## 프로젝트 소개

- 장르: 방치형 / 무역 / 경영 / 시뮬레이션
- 개발 인원: 5인
- Unity: 6000.5.2f1

## 핵심 플레이

경로 및 상품 선택
→ 상품 구매
→ 캐러반 이동
→ 목적지 판매
→ 정산
→ 성장 및 다음 무역

## 핵심 기능

- 오프라인 진행 시스템
- 시장 구매·판매·정산
- 월드맵 및 도시 이동
- 다중 캐러반
- 건설 및 업그레이드
- 퀘스트/진행 시스템

## 담당 영역

Framework & Integration을 중심으로 다음 영역을 구현하고 연동했습니다.

- SaveData 및 Snapshot 기반 상태 저장·복구
- 오프라인 경과 시간과 무역 진행 연동
- 시장 구매, 이동, 판매, 정산으로 이어지는 Trade integration
- 무역 상태와 UI 데이터 연결
- 캐러반별 진행·Cargo·정산 데이터를 분리하는 Multi-caravan integration

## 기술적 특징

- Snapshot 기반 저장
- Offline progression
- Trade state transition
- Multi-caravan data isolation
- Git LFS 기반 대형 asset 관리

## 주요 트러블슈팅

- 단일 캐러반 구조를 다중 캐러반으로 확장하며 Cargo와 정산 데이터를 ID 기준으로 격리
- 시장, Cargo, 정산 사이의 무역 데이터 원천 불일치를 snapshot과 명시적 연결 기준으로 정리
- Additive Scene 및 로딩 과정에서 남는 stale state를 저장 상태 재동기화로 보완
- Git branch/history 관리 과정에서 통합 이력 검증과 복구 절차 수행

## 실행 환경

Unity 6000.5.2f1

## External Dependencies

프로젝트 개발에는 저장소에 재배포하지 않는 다음 외부 package가 사용되었습니다.

- PolyPerfect
- ToonScapes

정확한 버전 및 설치 URL은 프로젝트 기록에 남아 있지 않습니다.
`Assets/_ExternalPackages`는 local-only package 경로이며, 자리표시자 외 package 원본은 Git 저장소에 포함하지 않습니다.

## Asset / License Notice

외부 자산과 생성형 도구로 제작한 프로젝트 자산의 상세 고지는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참조하십시오.

## Repository Purpose

이 저장소는 프로젝트의 포트폴리오 및 개발 기록 공개를 목적으로 합니다.

Repository-wide open-source license는 별도로 부여하지 않습니다. Third-party asset은 각각의 원 라이선스를 따릅니다.
