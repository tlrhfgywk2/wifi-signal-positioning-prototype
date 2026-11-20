# Codex Agent 작업 인수인계 문서

작성일: 2026-08-01 (Asia/Seoul)
대상: 다른 PC에서 이 Unity 프로젝트 작업을 이어갈 Codex Agent와 개발자

## 1. 가장 먼저 알아야 할 현재 상태

이 저장소는 단순한 초기 Unity 프로젝트가 아니라, 다음 기능까지 구현된 플레이 가능한 2D Wi-Fi 위치 추정 프로토타입이다.

- 10m × 10m 정사각형 방
- WASD/방향키 및 누르고 있는 동안 연속 입력되는 화면 방향 버튼
- 방 네 모서리의 `NW`, `NE`, `SW`, `SE` AP
- 플레이어의 절대적으로 신뢰 가능한 **관성 위치**: 속이 찬 흰색 점
- AP 신호로 복원한 **신호 추정 위치**: 속이 빈 빨간색 링
- 기본 1초 주기의 신호 측정
- AP마다 독립적인 기본 `-3~+3 dB` 균등 랜덤 측정 잡음
- AP-플레이어 신호 경로에 영향을 주는 전파 간섭 생물
- 신호 이력과 간섭 상태(`STABLE`, `UNSTABLE`, `SEVERE INTERFERENCE`, `AP CONTAMINATED` 등)
- 신호 바, dBm, 기대 신호 대비 편차, 추정 좌표와 오차를 표시하는 진단 패널

현재 기능 기준 커밋은 `c7c11b5 feat: add noisy signal tracking and radio interference`이다. 이 문서는 그 뒤에 추가되는 별도의 인수인계 커밋에 포함된다.

## 2. Git 및 GitHub 상태

- GitHub 저장소: `https://github.com/tlrhfgywk2/wifi-signal-positioning-prototype`
- 기본 브랜치: `main`
- 작업 브랜치: `codex/wifi-signal-positioning-prototype`
- Draft PR: `https://github.com/tlrhfgywk2/wifi-signal-positioning-prototype/pull/1`
- Draft PR 대상: `main ← codex/wifi-signal-positioning-prototype`
- `main`은 초기 프로젝트 기준 상태이고, 실제 구현은 작업 브랜치에 있다.
- 강제 푸시나 히스토리 재작성은 하지 않았다.

다른 PC에서는 반드시 작업 브랜치를 체크아웃해야 한다.

```powershell
git clone https://github.com/tlrhfgywk2/wifi-signal-positioning-prototype.git
cd wifi-signal-positioning-prototype
git switch codex/wifi-signal-positioning-prototype
git pull --ff-only
git status -sb
```

Codex Agent는 작업을 시작하기 전에 다음을 다시 확인해야 한다.

```powershell
git status -sb
git log --oneline --decorate -8
git remote -v
```

사용자 변경이 남아 있다면 임의로 되돌리거나 덮어쓰지 않는다. 특히 `PrototypeBootstrap.cs`의 UI 좌표 변경은 사용자가 직접 조정한 것이다.

## 3. 개발 환경

- Unity Editor: `6000.3.19f1`
- 정확한 리비전: `7689f4515d75`
- 렌더 파이프라인: Universal Render Pipeline 17.3.0 / 2D 프로젝트
- Input System: `com.unity.inputsystem` 1.19.0
- Project Settings의 `activeInputHandler: 1`: 새 Input System만 활성화
- UI: `com.unity.ugui` 2.0.0
- TextMeshPro를 직접 의존하거나 사용할 필요 없이 기본 uGUI `Text`와 `TextMesh`를 사용
- 테스트 프레임워크 패키지는 있으나 현재 검증은 커스텀 Editor 메서드와 MSBuild를 중심으로 구성

Unity Hub에서 저장소 루트 자체를 프로젝트로 추가한다. 다른 Unity 프로젝트 안에 중첩 프로젝트를 만들면 안 된다.

Unity 생성 디렉터리는 `.gitignore`로 제외되어 있다.

- `Library/`
- `Temp/`
- `Logs/`
- `obj/`
- `Build/`, `Builds/`
- `UserSettings/`

`Assets`, `Packages`, `ProjectSettings`, Unity `.meta` 파일은 추적 대상이다.

## 4. 실행 방법

1. Unity Hub에서 프로젝트 루트를 Unity `6000.3.19f1`로 연다.
2. `Assets/Scenes/WifiSignalPositioningPrototype.unity`를 연다.
3. Play를 누른다.

이 씬은 Build Settings의 첫 번째 활성 씬이다. 기존 `Assets/Scenes/SampleScene.unity`는 삭제하지 않고 두 번째 활성 씬으로 보존되어 있다.

조작:

- `W`, `A`, `S`, `D`
- 방향키
- 화면 왼쪽 아래 방향 버튼을 마우스로 누르고 있기

키보드와 UI는 모두 `PlayerMovement.ReadCurrentInput()`과 `ApplyMovement()`를 사용한다. 대각선 입력은 정규화되고, 이동 결과는 `RoomBounds.Clamp()`를 거쳐 방 안으로 제한된다.

## 5. 핵심 위치 개념

화면에는 플레이어 위치를 나타내는 표식이 두 개 있다.

### 관성 위치

- 이름: `Inertial Position (Solid White Dot)`
- 표시: 속이 찬 흰색 점
- 플레이어 조작을 매 프레임 반영
- 이 프로토타입에서는 절대적으로 신뢰 가능한 실제 위치로 간주
- 이동과 방 경계 제한의 기준

### 신호 추정 위치

- 이름: `Signal Estimated Position (Red Ring)`
- 표시: 속이 빈 빨간색 링
- AP 신호 측정이 일어날 때만 갱신
- 기본 갱신 주기: 1초
- 플레이어 Transform을 추정 결과로 직접 복사하지 않음
- 측정 잡음과 간섭 때문에 흰색 점과 다른 위치에 나타날 수 있음

플레이어가 움직이면 흰색 점은 즉시 움직이고 빨간 링은 다음 측정까지 이전 위치에 남는다. 플레이어가 정지해 있어도 AP별 랜덤 잡음 때문에 빨간 링은 주변에서 흔들릴 수 있다.

## 6. 신호 및 위치 추정 파이프라인

전체 데이터 흐름은 다음과 같다.

```text
관성 위치(실제 Transform)
  → AP별 실제 Euclidean 거리
  → 이상적 선형 신호
  → 정적 감쇠 + 기본 랜덤 잡음 + 생물 간섭 감쇠
  → AP별 최종 측정 dBm
  → 측정 dBm을 거리로 역산
  → 네 AP의 알려진 좌표와 역산 거리
  → 최소제곱 다변측량
  → 빨간 신호 추정 링
```

### 6.1 이상적 선형 신호

`LinearSignalModel.Calculate()`:

```text
expectedSignal = referenceSignalStrength - distance * signalLossPerMeter
```

기본값:

- `referenceSignalStrength = -20 dBm`
- `signalLossPerMeter = 5 dB/m`

이 모델은 게임플레이 검증용이며 실제 Wi-Fi 전파 모델이 아니다.

### 6.2 최종 측정 신호

`AccessPointSignal.Refresh()`의 개념식:

```text
measuredSignal = expectedSignal
                 - stableAttenuationDb
                 + uniformRandomNoise
                 + creatureAttenuationDb
```

- `uniformRandomNoise`: AP마다 독립적으로 `Random.Range(-3f, 3f)`가 기본값
- `stableAttenuationDb`: 벽 같은 정적 손실용. 현재 기본값은 0
- `creatureAttenuationDb`: 항상 0 이하인 추가 감쇠값
- 최종 측정값은 내부적으로 `float`, UI에서는 정수 dBm으로 반올림

### 6.3 신호에서 거리 역산

`LinearSignalModel.TryRecoverDistance()`:

```text
estimatedDistance =
    (referenceSignalStrength - measuredSignal) / signalLossPerMeter
```

측정 잡음 때문에 계산 결과가 음수가 되면 `0m`로 제한한다. 손실값이 0 이하이거나 값이 유한하지 않으면 실패로 처리한다.

정적 감쇠와 생물 감쇠를 역산 전에 보정하지 않는 것이 현재 의도다. 따라서 감쇠가 있으면 AP가 실제보다 더 멀리 있다고 판단하며 빨간 링이 관성 위치에서 벗어난다.

### 6.4 최소제곱 위치 복원

`SignalPositionEstimator`는 첫 AP의 원 방정식을 다른 AP 원 방정식에서 빼 선형화한다. 네 AP가 제공하는 중복 방정식으로 2×2 정규방정식을 만들고 최소제곱 해를 계산한다.

- AP가 3개 미만이면 실패
- 유효한 거리 방정식이 2개 미만이면 실패
- 행렬식이 너무 작으면 실패
- 결과가 유한하지 않으면 실패

## 7. 전파 간섭 생물

씬에는 `Radio Interference Creature` 오브젝트가 명시적으로 직렬화되어 있다. 씬 참조가 없으면 `PrototypeBootstrap.EnsureInterferenceCreature()`가 `(-1.2, 1.1)`에 런타임 대체 생물을 만든다.

현재 생물 표시 자산은 외부 아트가 아니라 청록색 원형 LineRenderer 플레이스홀더다.

### 간섭 판정

`RadioInterferenceMath.PointToSegmentDistance()`가 생물 위치와 **AP→플레이어 선분** 사이의 최단 거리를 계산한다. 투영점은 선분 범위로 clamp되므로 AP 뒤쪽이나 플레이어 너머의 점은 끝점을 사용한다.

씬 기본 간섭 밴드:

| 밴드 | 경로까지 거리 | 감쇠 진폭 |
|---|---:|---:|
| Strong | ≤ 0.25m | 최대 12 dB |
| Medium | ≤ 0.65m | 최대 6 dB |
| Weak | ≤ 1.40m | 최대 3 dB |
| None | > 1.40m | 0 dB |

생물이 AP에서 0.9m 이내면 `AP CONTAMINATED` 상태가 된다. 이 경우 최소 Medium 진폭을 보장한다.

`RadioInterferenceCreature.SampleAttenuation()`은 AP 식별자로 만든 안정적 채널 오프셋과 Perlin noise를 사용한다. 실제 감쇠는 해당 밴드 진폭의 약 `2/3~1배` 범위이며 음수 dB로 더해진다.

기본 생물 설정:

- `noiseUpdateFrequency = 0.7`
- `noiseSeed = 17.31`
- `patrolEnabled = false`
- `patrolDistance = 1.5`
- `patrolSpeed = 0.5`
- `patrolDirection = (1, 0)`

순찰을 켜면 시작점을 중심으로 사인파 왕복 이동한다.

`showInterferenceDebug`는 기본 false다. true로 설정하면 `RadioInterferenceDebugVisualizer`가 AP-플레이어 경로, 생물-최근접점, 간섭 반경과 최근접점 Gizmo/Debug line을 표시한다.

## 8. 신호 이력과 패널 상태

각 `AccessPointSignal`은 기본 12개의 측정값을 `SignalMeasurementHistory` 원형 버퍼에 저장한다. `Range`는 현재 이력의 최대값-최소값이다.

기본 패널 임계값:

- 신호 바: `-90~−20 dBm`
- 강도 분류 기준 필드는 남아 있지만, 현재 행 상태 텍스트는 주로 간섭 상태를 표시
- `unstableRangeThreshold = 5 dB`
- `severeRangeThreshold = 9 dB`

상태 우선순위:

1. `AP CONTAMINATED`
2. `SEVERE INTERFERENCE`
3. `UNSTABLE`
4. `WEAK INTERFERENCE`
5. `STABLE`

각 AP 행에는 다음 정보가 표시된다.

- AP 식별자
- 신호 바
- 반올림된 dBm
- 기대 신호 대비 편차 `Δ`
- 간섭/안정 상태

`recentCreatureSamples`는 간섭이 끝난 직후에도 이력 범위 기반 상태 판정이 잠시 유지되도록 사용된다.

## 9. 씬과 런타임 구성 방식

씬에 모든 런타임 하위 오브젝트를 직렬화하지 않는다. 씬에는 주로 다음 루트가 저장되어 있다.

- `Wi-Fi Positioning Prototype` + `PrototypeBootstrap`
- 그 하위의 `Radio Interference Creature`

Play 시 `PrototypeBootstrap.BuildPrototype()`이 한 번만 다음을 생성한다.

- Main Camera
- World
- Room 및 테두리
- 관성 위치 플레이어
- 네 AP와 레이블
- 신호 위치 추정기
- 빨간 추정 링
- 진단 Canvas와 신호 패널
- 누르고 있기 방향 패드
- Input System EventSystem
- 간섭 디버그 시각화 컴포넌트

매 프레임 새 UI 또는 월드 오브젝트를 생성하지 않는다. 신호 계산은 `LateUpdate()`에서 기본 1초 간격으로 수행한다.

## 10. 주요 파일과 책임

| 파일 | 책임 |
|---|---|
| `Assets/Scenes/WifiSignalPositioningPrototype.unity` | 전용 플레이 씬과 직렬화 설정 |
| `Assets/Scripts/PrototypeBootstrap.cs` | 런타임 계층 생성, 1초 샘플링 오케스트레이션, UI 배치 |
| `Assets/Scripts/RoomBounds.cs` | 방 크기와 좌표 clamp |
| `Assets/Scripts/PlayerMovement.cs` | 키보드/UI 입력 통합, 정규화 이동 |
| `Assets/Scripts/DirectionalInputButton.cs` | 포인터 down/up/exit 기반 유지 입력 |
| `Assets/Scripts/AccessPointSignal.cs` | 이상 신호, 잡음, 정적/생물 감쇠, 이력, 거리 역산 입력 |
| `Assets/Scripts/SignalPositionEstimator.cs` | 최소제곱 다변측량 |
| `Assets/Scripts/SignalMeasurementHistory.cs` | 고정 용량 신호 이력과 범위 |
| `Assets/Scripts/RadioInterferenceCreature.cs` | 생물 설정, 플레이스홀더, 선택적 순찰, Perlin 감쇠 |
| `Assets/Scripts/RadioInterferenceMath.cs` | AP-플레이어 선분 거리와 간섭 밴드 판정 |
| `Assets/Scripts/RadioInterferenceDebugVisualizer.cs` | 간섭 경로와 반경 디버그 표시 |
| `Assets/Scripts/SignalPanelController.cs` | 행 갱신, 추정 좌표/오차, 상태 임계값 |
| `Assets/Scripts/SignalRowView.cs` | AP별 신호 행과 상태 분류 |
| `Assets/Editor/WifiPrototypeSceneGenerator.cs` | 씬 재생성, 핵심 수학 검증, Play Mode 스모크 테스트 |
| `README.md` | 사용자 중심 실행 및 구조 설명 |
| `CODEX_HANDOFF.md` | Codex Agent 간 상세 인수인계 문서 |

## 11. 현재 직렬화 기본값

`WifiSignalPositioningPrototype.unity`의 핵심 값:

| 항목 | 값 |
|---|---:|
| 방 크기 | 10 × 10m |
| 플레이어 속도 | 3 units/s |
| 기준 신호 | -20 dBm |
| 신호 손실 | 5 dB/m |
| 측정 주기 | 1s |
| 기본 랜덤 잡음 | ±3 dB |
| AP 테두리 여백 | 0.45m |
| 생물 위치 | (-1.2, 1.1) |
| 간섭 디버그 | false |
| 생물 순찰 | false |

AP는 방 중심 기준 대략 `(±4.55, ±4.55)`에 생성된다.

## 12. 사용자가 직접 수정한 UI 사항

다음 변경은 사용자가 직접 Game View를 보고 겹침을 해결하기 위해 조정한 것이므로, 다른 Codex Agent가 임의로 원래 값으로 되돌리면 안 된다.

- Estimated Coordinates 영역의 상단 anchor를 `0.47f - 0.12f` 형태로 낮춤
- 하단 `Move with...` 안내문 글자 크기를 18로 키움
- 안내문 X 위치를 `260f + 320f`만큼 오른쪽으로 이동
- 신호 행의 열 너비도 현재 AP/바/값·편차/상태가 맞도록 재배치됨

표현식이 단순화되지 않은 형태로 남아 있어도 사용자의 의도적인 위치 조정이다. UI를 리팩터링하려면 먼저 현재 Game View를 캡처하거나 확인하고, 변경 전후 겹침 여부를 검증한다.

## 13. 알고 있어야 할 설계 결정과 대화 맥락

- 네 AP 신호가 모두 `-80 dBm`인 지점은 현재 기하와 선형 모델에서 존재하지 않는다. `-80 dBm`은 AP별 12m 거리에 해당하지만 네 AP에서 동시에 같은 거리를 만족하는 유일한 대칭점인 방 중심에서는 약 `-52.17 dBm`이다.
- 빨간 링은 실제 Transform을 복사하지 않는다. 실제 거리로 모의 신호를 만든 뒤 그 **측정 신호만** 거리로 역산해 위치를 복원한다.
- 관성 위치는 게임 내부에서 절대적으로 신뢰 가능한 기준이고, 신호 위치는 의도적으로 느리고 부정확할 수 있다.
- 기본 ±3 dB 랜덤 잡음은 실제 Wi-Fi가 정지 상태에서도 흔들리는 현상을 단순화한 요구사항이다.
- 생물 간섭은 후속으로 추가된 확장이다. 실제 전파 물리 모델이 아니라 AP-플레이어 선분과 생물 거리 기반 게임 규칙이다.

## 14. 검증 현황

기능 개발 초기에 Unity `6000.3.19f1` 배치 모드에서 다음이 실제로 통과했다.

- C# 컴파일
- 씬 생성 및 로드
- 선형 신호 계산과 거리 역산
- 잘못된 손실값 방어
- 네 AP 최소제곱 위치 복원
- WASD 입력 경로
- 네 화면 방향 버튼의 유지 입력
- 방 경계 제한
- AP 코너 배치와 신호 행 수
- 두 위치 마커 생성

간섭 생물과 신호 이력까지 포함한 최신 커밋 `c7c11b5`에서는 다음 MSBuild 검증이 실제 통과했다.

```text
Assembly-CSharp.csproj restore/build: 성공
Assembly-CSharp-Editor.csproj restore/build: 성공
git diff --check: 성공
```

최신 `WifiPrototypeSceneGenerator.ValidateCoreModel()`에는 다음 자동 검증 코드가 들어 있다.

- 신호 정방향/역방향 모델
- AP-플레이어 선분 최근접점 clamp
- Strong/Medium/Weak 간섭 밴드
- AP 근접 오염
- 고정 용량 신호 이력
- 상태 분류
- 잡음 없는 최소제곱 위치 복원

최신 `RunPlayModeSmokeTest()`에는 런타임 계층, 두 마커, 생물, AP, 신호 행, 입력, 경계, 잡음 허용 오차 검사가 들어 있다.

다만 최신 간섭 기능을 포함한 상태에서 Unity Editor의 메뉴/배치 메서드를 다시 실제 실행하는 최종 확인은 아직 권장된다. 이전 PC에서는 동일 프로젝트를 잡고 있는 Unity 프로세스가 여러 개라 별도 배치 인스턴스가 프로젝트 잠금으로 종료된 적이 있다. 프로세스를 강제로 종료하지 않고 MSBuild 검증으로 대체했다.

## 15. 다른 PC에서 가장 먼저 수행할 검증

1. 작업 브랜치가 최신인지 확인한다.
2. Unity `6000.3.19f1`로 프로젝트를 연다.
3. 콘솔의 C# 오류와 Missing Script를 확인한다.
4. `WifiSignalPositioningPrototype` 씬을 열고 Play한다.
5. 흰색 관성 점과 빨간 추정 링이 모두 보이는지 확인한다.
6. 정지 중에도 AP 값과 빨간 링이 약 1초마다 흔들리는지 확인한다.
7. WASD/방향키/네 화면 버튼을 각각 누르고 있는 동안 이동하는지 확인한다.
8. 플레이어가 방을 벗어나지 않는지 확인한다.
9. 생물과 AP-플레이어 경로의 거리가 가까워질 때 해당 AP 상태와 감쇠가 변하는지 확인한다.
10. 패널 글자, 하단 안내문, 방향 패드가 겹치지 않는지 확인한다.
11. Console에 반복되는 `NullReferenceException`이 없는지 확인한다.

필요하면 Unity 메뉴에서 씬을 재생성할 수 있다.

```text
Tools > Wi-Fi Prototype > Generate Playable Scene
```

주의: 이 메뉴는 전용 씬을 새로 만들고 저장하므로, 사용자가 씬에서 수동 조정한 내용이 있다면 먼저 diff와 백업 여부를 확인한다.

## 16. 자동 검증 실행 예시

Windows에서 Unity 경로가 동일한 경우:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath (Get-Location).Path `
  -executeMethod WifiPositioningPrototypeEditor.WifiPrototypeSceneGenerator.ValidateCoreModel `
  -logFile '.\Logs\handoff-core-validation.log'
```

Play Mode 스모크 테스트는 `RunPlayModeSmokeTest`를 사용한다. 이 메서드는 테스트 완료 시 `EditorApplication.Exit()`를 호출하므로 별도의 `-quit` 없이 실행하는 방식이 맞다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath (Get-Location).Path `
  -executeMethod WifiPositioningPrototypeEditor.WifiPrototypeSceneGenerator.RunPlayModeSmokeTest `
  -logFile '.\Logs\handoff-playmode-validation.log'
```

같은 프로젝트를 Unity Editor에서 이미 열어 둔 상태에서는 배치 인스턴스를 동시에 실행하지 않는다.

## 17. 알려진 제한과 주의사항

- 신호 모델은 현실적인 Wi-Fi 모델이 아니라 선형 모델이다.
- 기본 잡음은 시간 상관이 없는 균등 랜덤 잡음이다.
- 생물 간섭은 선분 거리 밴드와 Perlin 감쇠에 기반한 게임 규칙이다.
- 벽, 멀티패스, 실제 환경 간섭, RSS 핑거프린트 DB는 없다.
- 최소제곱 결과를 방 경계로 clamp하지 않는다. 큰 간섭이 있으면 빨간 링이 방 밖에 나타날 수 있으며, 이것이 버그인지 의도인지 다음 디자인 단계에서 결정해야 한다.
- `TryRecoverDistance()`는 음수 추정 거리를 0으로 clamp한다.
- 외부 아트, 오디오, 적, 전투, 목표, 저장, 네트워킹은 없다.
- Game View 자동 스크린샷은 이전 배치 환경에서 End-of-Frame 제약으로 실패했다. UI 가독성은 Editor에서 육안 확인하는 것이 안전하다.
- `PrototypeBootstrap`이 런타임 UI를 코드로 만들기 때문에 UI 변경은 주로 해당 파일에서 이루어진다.
- `SignalRowView`의 `Δ` 문자는 UTF-8 그리스 대문자 델타(U+0394)다. 인코딩 문제로 `?`로 보이면 파일을 UTF-8로 다시 확인한다.

## 18. 추천 후속 작업

우선순위가 높은 다음 작업 후보:

1. 최신 간섭 기능 상태에서 Unity Core/Play Mode 자동 검증을 실제 재실행
2. 1280×720 및 다른 종횡비에서 UI 겹침 육안 확인
3. 생물을 움직이거나 위치를 조정해 AP별 상태 변화와 빨간 링 이탈을 플레이테스트
4. 추정 위치가 방 밖으로 나갈 때의 디자인 결정
5. 랜덤 시드를 주입할 수 있는 테스트용 잡음 공급자 분리
6. 신호 이력 그래프나 최근 측정값 시각화
7. 현재 코드 생성 UI를 Prefab/씬 직렬화 UI로 옮길지 여부 결정

새로운 기능을 추가하기 전에 이 프로토타입의 목적이 **핵심 Wi-Fi 신호 기반 위치 추정 게임 메커니즘 검증**이라는 점을 유지한다. 현실적인 무선 모델이나 대규모 프레임워크를 성급하게 추가하지 않는다.

## 19. 작업 종료 시 Git 규칙

- 사용자 변경을 임의로 되돌리지 않는다.
- Unity `.meta` 파일을 함께 커밋한다.
- `Library`, `Temp`, `Logs`를 커밋하지 않는다.
- 현재 작업 브랜치에서 설명적인 커밋을 만든다.
- 푸시 전 `git diff --check`, 관련 C# 빌드 또는 Unity 검증을 수행한다.
- `git push --force`를 사용하지 않는다.
- Draft PR #1이 이미 있으므로 같은 브랜치 푸시는 자동으로 PR에 반영된다. 중복 PR을 만들지 않는다.

다음 Agent가 작업을 시작할 때는 이 문서보다 코드와 `git status`가 최종 진실의 원천이다. 문서와 코드가 다르면 먼저 최근 커밋과 diff를 확인하고 문서도 함께 갱신한다.
