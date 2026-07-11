# Wi-Fi Signal Positioning Prototype

Unity 6 기반의 작고 완전하게 플레이 가능한 2D 검증용 프로토타입입니다. 10m × 10m 방 안에서 플레이어가 움직이면 네 모서리 AP의 단순화된 신호 세기를 계산하고, 그 신호만으로 플레이어 위치를 다시 복원해 실시간으로 표시합니다.

이 프로젝트의 신호 모델은 게임플레이 아이디어 검증을 위해 의도적으로 비현실적인 선형 모델을 사용합니다. 실제 Wi-Fi 전파를 모사하지 않습니다.

## 열기와 실행

- Unity Editor: `6000.3.19f1`
- 프로젝트 루트를 Unity Hub에서 엽니다.
- `Assets/Scenes/WifiSignalPositioningPrototype.unity` 씬을 엽니다.
- Play를 누릅니다. 이 씬은 Build Settings의 첫 번째 활성 씬으로도 등록되어 있습니다.

## 조작

- 이동: `W`, `A`, `S`, `D`
- 추가 키보드 입력: 방향키
- 마우스: 화면 왼쪽 아래의 네 방향 버튼을 누르고 있는 동안 계속 이동

키보드와 UI 버튼은 모두 `PlayerMovement.ReadCurrentInput()` 및 `ApplyMovement()`라는 동일한 이동 경로를 사용합니다. 대각선 입력은 정규화되며 플레이어는 방 경계 밖으로 나갈 수 없습니다.

## 신호 모델

각 AP의 거리 `d`에 대해 다음 식을 매 프레임 적용합니다.

```text
signalStrength = referenceSignalStrength - distance * signalLossPerMeter
```

기본값은 기준 신호 `-20 dBm`, 미터당 손실 `5 dB`입니다. 내부 값은 `float`로 유지하고 UI에서만 정수 dBm으로 반올림합니다. 신호 모델과 역산은 `LinearSignalModel`에 격리되어 있습니다.

추정 거리는 다음과 같이 신호에서 다시 계산합니다.

```text
estimatedDistance =
    (referenceSignalStrength - signalStrength) / signalLossPerMeter
```

`SignalPositionEstimator`는 첫 AP의 원 방정식을 나머지 AP의 원 방정식에서 빼 선형화한 뒤, 네 AP가 제공하는 중복 방정식을 2차원 최소제곱법으로 풉니다. 신호 손실값이 0 이하이거나 값이 유한하지 않거나 행렬이 특이한 경우 유효한 추정으로 처리하지 않습니다. 잡음이 없으므로 추정 링은 실제 플레이어 점과 거의 일치합니다.

## 주요 위치

- 플레이 씬: `Assets/Scenes/WifiSignalPositioningPrototype.unity`
- 런타임 구성: `Assets/Scripts/PrototypeBootstrap.cs`
- 방과 이동: `Assets/Scripts/RoomBounds.cs`, `Assets/Scripts/PlayerMovement.cs`
- 누르고 있기 UI 입력: `Assets/Scripts/DirectionalInputButton.cs`
- AP와 선형 신호 모델: `Assets/Scripts/AccessPointSignal.cs`
- 위치 복원: `Assets/Scripts/SignalPositionEstimator.cs`
- 신호 UI: `Assets/Scripts/SignalPanelController.cs`, `Assets/Scripts/SignalRowView.cs`
- 씬 생성 및 검증 도구: `Assets/Editor/WifiPrototypeSceneGenerator.cs`

씬에는 직렬화된 `PrototypeBootstrap` 하나만 두고, Play 시작 시 읽기 쉬운 `World`, 진단 UI, Input System EventSystem 계층을 한 번 생성합니다. 매 프레임 새 오브젝트를 만들지 않습니다. 기존 `SampleScene`과 기존 설정 자산은 삭제하지 않았습니다.

## 조정 가능한 값

- `PrototypeBootstrap`: 방 너비/높이, 플레이어 속도, AP 기준 신호, 미터당 신호 손실, AP 테두리 여백
- `PlayerMovement`: 이동 속도와 경계 여백
- `AccessPointSignal`: 안정적 식별자, 기준 신호, 미터당 손실, 현재 신호
- `SignalPanelController`: Strong/Good/Fair/Weak 임계값과 신호 바 표시 범위

기본 UI는 TextMeshPro 직접 의존성을 추가하지 않고 프로젝트에 이미 있는 uGUI와 Input System을 사용합니다. 프로젝트 설정은 새 Input System 활성 상태입니다.

## 검증

Unity 배치 실행으로 다음을 자동 확인할 수 있도록 편집기 검증 메서드를 포함합니다.

- C# 컴파일 및 씬 직렬화
- 신호 정방향 계산과 거리 역산
- 0 신호 손실값 방어
- 네 AP 최소제곱 위치 복원
- Play Mode 런타임 계층, AP 코너 배치, 신호 행 수
- WASD 입력 경로와 네 방향 누르고 있기 버튼
- 방 경계 제한
- AP에 가까워질 때 신호 강화, 멀어질 때 신호 약화
- 잡음 없는 모델에서 근접 0 추정 오차

`Tools > Wi-Fi Prototype > Generate Playable Scene` 메뉴로 씬을 재생성할 수 있습니다. 정상 플레이에는 편집기 전용 코드가 필요하지 않습니다.

## 알려진 제한과 다음 단계

현재 단계에는 신호 잡음, 움직이는 장애물, 환경 간섭, 멀티패스/벽 감쇠, RSS 핑거프린트 데이터베이스, 적, 전투, 게임 목표가 없습니다. 저장, 오디오, 네트워킹도 포함하지 않습니다.

다음 단계에서는 교체 가능한 신호 모델에 결정론적 잡음을 선택적으로 추가하고, 추정 오차 시각화 및 알고리즘 비교 테스트를 확장할 수 있습니다. 실제 실내 측위 정확도를 주장하려면 별도의 측정 데이터와 현실적인 전파 모델이 필요합니다.
