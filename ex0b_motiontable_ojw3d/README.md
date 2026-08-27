# ex0b. OMX 모션 테이블 실행기 — OpenJigWare 3D

작성일: 2026-08-20. **GUI를 제외한 거의 모든 동작이 OpenJigWare.dll 함수로 처리되는** 교육용 윈도우 프로그램.
ex0(픽앤플레이스)와 별개 — ex0는 코드로 시퀀스를 짜는 방식, **ex0b는 모션 테이블(그리드)로 명령을 만들어 라이브러리가 재생하는 방식**(알모션/Tools.exe 계보).

## 사용 흐름

1. **예제 버튼** (픽앤플레이스 / 사각형 직선 / 사각형 베지어) → 하단 **모션 그리드에 명령(행)이 생성**됨
   - 행 = En, Motor0~5(관절각), Time(동작시간), Delay
   - 모터 0~3=팔, 4=툴 롤, 5=그리퍼 (열림 30 / 파지 4)
2. **▶ Run** → 행을 순서대로 재생. **그리드 셀 값을 직접 고쳐서 다시 Run** 해볼 수 있음 (교육 포인트)
3. **■ Stop** → 현재 행까지 실행하고 정지

## 검증 결과 (2026-08-20)

- 예제 1 픽앤플레이스: 파지 거리 26mm 성공, `final=(210.0, -109.6)` 목표 안착
- 예제 3 베지어 사각형: 39행 재생 완료, 자취 130점 (둥근 모서리 확인)

## 라이브러리 함수 사용 내역 (COjw_12_3D.cs 등)

| 역할 | 라이브러리 함수 |
|---|---|
| 모션 그리드 | `GridMotionEditor_Init / SetMotor / SetTime / SetDelay / SetEnable / GetTime / GetDelay` |
| **재생(애니메이션)** | `PlayFrame(행)` + `WaitAction_SetTimer / WaitAction_ByTimer` — **스무스 시뮬레이션을 라이브러리가 자체 펌핑** (`SetSimulation_With_PlayFrame(true)` + `SetSimulation_Smooth(true)`) |
| IK (행 생성 시) | `CalcInv_SetIKAlgorithm(0)`(CCDIK) + `CalcInv(수식0, x, y, z, iter, tol)` + `CalcInv_MotorIDs` — 직전 행 각도를 `SetData`로 시드해 연속 해 |
| FK (파지 판정·자취) | `CalcF(수식0, ...)` |
| 궤적 샘플링 | `Ojw.CMath.SampleCornersLinear / SampleCornersBlend` (직선/베지어 코너, 닫힌 도형) |
| 3D·장면 | `MakeDHSkeleton_Urdf / CompileDesign / SetData / User_* / OjwDraw / printf` |

앱(GUI) 코드가 하는 일: 버튼·그리드 배치, 예제의 좌표 목록 정의, 잡기/놓기/자취 같은 장면 이벤트 표시뿐.

## 주의 (재현용)

- **모션 그리드는 모터 번호가 0부터 연속이어야 한다** — 그리드 열과 PlayFrame이 모터 0..nMotorCnt-1을 순회하므로,
  이 앱의 DH는 OMX 모터를 11~16 → **0~5로 재번호**해 내장했다 (ex0/ojwSimul의 11~16 체계와 다름).
- Run 재생은 `WaitAction_ByTimer` 안에서 DoEvents가 돌므로 화면 갱신·부착 갱신(33ms 타이머)이 함께 동작한다.
- 빌드: `MSBuild OmxMotionTable.csproj -t:Restore,Build -p:Configuration=Release` (net48 x86, lib\ DLL 동봉)
