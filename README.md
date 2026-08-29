# 로보틱스 기초 — 교육용 로봇팔 예제 모음 (OpenJigWare 3D · MuJoCo · Isaac Sim)

K-디지털 기초역량훈련 「로보틱스 기초」 강의용 실습 예제 프로그램 모음입니다.
로봇 모델은 OMX Follower 6축 로봇팔(변형 DH)이며, **파이썬 코드로 3D 로봇을 직접 움직이고,
같은 코드로 실물 로봇(다이나믹셀)까지 구동**할 수 있습니다.

## 폴더 구성

| 폴더 | 내용 | 검증 상태 |
|---|---|---|
| `OMX_Examples_Simple\` | **★ 심플 파이썬 실행기 (C# + OpenJigWare 3D)** — 예제 6종(픽앤플레이스·팔레타이징·사각형 직선/베지어 좌표/베지어 실시간 계산·팔레타이징2 아치/경유 설정)을 "한 줄에 한 명령" 스타일로. 그리퍼를 닫으면 잡히고 열면 놓이는 자동 파지. **VS Code 등에서 예제 파일 단독 실행도 지원** | ✅ 전 예제 실행 검증 |
| `ex0c_python_ojw3d\` | **파이썬 실행기 (C# + OpenJigWare 3D)** — 임베디드 파이썬(pythonnet)이 화면의 3D를 직접 구동. 예제는 함수·반복문을 사용한 구조화 버전 | ✅ 전 예제 실행 검증 |
| `ex0_pickplace_ojw3d\` | 윈도우 프로그램 (C#) — 버튼식 픽앤플레이스·팔레타이징·사각형. 제어 API = 강의 형식 `Play / SyncRead / CalcInv` | ✅ 실행 검증 |
| `ex0b_motiontable_ojw3d\` | 모션 테이블 실행기 (C#) — 예제 버튼 → 모션 그리드 생성 → 라이브러리 재생(PlayFrame) | ✅ 실행 검증 |
| `model\` | OMX Follower 모델 — robot_stl.urdf(PyBullet/MuJoCo), robot_stl.xml(MJCF), robot_stl_isaac.urdf(Isaac용), stl\ 메시 40종 | 생성·로딩 검증 |
| `common\` | 공용 파이썬 모듈 — omx_kinematics.py(변형 DH FK/IK), omx_mujoco.py, omx_isaac.py | FK는 MuJoCo와 교차 검증(오차 0.000mm) |
| `ex1_pick_and_place_mujoco\` | 픽앤플레이스 (MuJoCo) — `--multi`로 3개 팔레타이징 | ✅ 실행 검증 (3/3) |
| `ex2_draw_rect_mujoco\` | 바닥 사각형 그리기 (MuJoCo) — 자취 시각화 | ✅ 실행 검증 |
| `ex3_pick_and_place_isaacsim\` | 픽앤플레이스 (Isaac Sim) | ⚠ 미검증 |
| `ex4_draw_rect_isaacsim\` | 바닥 사각형 그리기 (Isaac Sim) | ⚠ 미검증 |

처음이라면 **OMX_Examples_Simple** 부터 보세요. 예제 코드는 로봇 제어 명령만 사용합니다:

```python
c3d.Play(1200, 0, 11, 0, 12, -25, 13, 35, 14, 35, 15, 0)             # 관절 이동 (홈 자세)
c3d.PlayXyz(1000, 0, 0, 200, 110, 45, 0, 90, 0, 14, 11, 12, 13)      # TCP 좌표 이동 (IK 내장)
c3d.Play(400, 0, 16, 4)                                              # 그리퍼 닫기 = 잡기 (자동 파지)
c3d.PlayXyzPath(1800, 0, 0, [250, -35, 63.5], 0, 90, 0, 14, [11, 12, 13])  # 한 획 긋기
path = Ojw.CMath.SampleCornersSmooth(corners, 90, "30mm", True)  # 예제 5: 코너 라운드 경로 실시간 생성 ("30mm"/"90%")
c3d.PlayXyz_Param_Arch(0, 45, "z")                 # 예제 6: 아치 설정 — 이후의 이동은 들어올려→이동→내려놓기 자동
c3d.PlayXyz(2600, 0, 0, 170, -110, 55)             # 예제 6: 축약 이동 — 자세·아치·경유는 미리 한 설정을 따른다
# scene.open(4, 1000000) / scene.torqon()  ← 주석 해제 시 실물 OMX 동시 구동
```

물체·펜·적재 목표의 실제 좌표는 각 실행기 폴더의 **`workspace_map.png`** (3D 화면 위에 좌표를
표시한 안내도)를 참고하세요.

## 필요 환경

- Windows 64비트, Visual Studio 2022 (C# 실행기 빌드 시)
- **Python 3.10 또는 3.11 (64비트)** + `pip install pythonnet` — C# 실행기의 임베디드 파이썬 및 단독 실행용
- MuJoCo 예제: `pip install mujoco numpy`
- 실물 구동(선택): 다이나믹셀 XL-430 기반 OMX Follower + U2D2

## 실행 방법

### OpenJigWare 3D 실행기 (OMX_Examples_Simple, ex0c)

- 빌드: 각 폴더의 `.csproj`를 Release(**x64**)로 빌드 후 `lib\*.dll`을 `bin\{구성}\net48\`에 복사
- 실행기에서: 예제 버튼 → 코드 확인 → ▶ Run (편집창에서 수정 후 바로 재실행, 리셋 버튼 = 중지)
- VS Code 단독 실행: `bin\...\examples\`의 예제 .py 파일을 그대로 실행 — 3D 창 없이 동일하게 동작
- 상세 사용법·아키텍처·함정 기록은 각 폴더의 README 참조

### MuJoCo (ex1, ex2)

```
pip install mujoco numpy
python ex1_pick_and_place.py              # 뷰어 창으로 보기
python ex1_pick_and_place.py --multi      # 상자 3개 순차 적재(팔레타이징)
python ex1_pick_and_place.py --headless   # 창 없이 실행 + output\ PNG 캡처
python ex2_draw_rect.py                   # 사각형 그리기 (파란 자취 실시간 표시)
```

- **뷰어 키**: `R` = 처음부터 다시 실행 / `C` = 초기 상태로 리셋 (프로그램 재시작 불필요, 창 닫기 = 종료)
- **재생 속도**: 뷰어 모드는 실시간(1.0배) 재생. `--speed 0.5` = 절반 속도, `--speed 2` = 2배속. (`--headless`는 항상 최고 속도로 계산)
- 한글 경로 대응: 모델을 `from_xml_string` + 메모리 assets로 로딩하므로 폴더가 한글이어도 동작한다.
- 콘솔이 cp949라 한글이 깨지면 `python -X utf8 ...` 로 실행.

### Isaac Sim (ex3, ex4) — Isaac Sim 4.5+ / 5.x

```
<Isaac 설치 폴더>\python.bat ex3_pick_and_place.py
<Isaac 설치 폴더>\python.bat ex4_draw_rect.py
```

- **미검증 코드**: API 버전 차이로 오류가 나면 `common\omx_isaac.py`의 import 폴백과
  link_5 프림 경로(cand)를 먼저 확인할 것.
- URDF 임포터의 비ASCII 경로 문제 대비: 한글 경로면 모델을 `%TEMP%\omx_model_ascii`로 자동 복사 후 임포트한다.

## 구조 (강의 파이프라인 그대로)

```
변형 DH (ojwSimul/MakeUrdf 예제 'OMX Follower')
   │  MakeUrdf 'View URDF' 내보내기
   ├── robot_stl.urdf ──────── PyBullet / (MuJoCo URDF 로딩)
   ├── robot_stl.xml (MJCF) ── MuJoCo  ← ex1, ex2
   └── robot_stl_isaac.urdf ── Isaac Sim ← ex3, ex4
공용 기구학: common\omx_kinematics.py
   - FK: DH 한 줄 = Rz(θ)·Tz(d)·Tx(a)·Rx(α) 누적 (강의 Part3 변환행렬 그대로)
OpenJigWare 3D 실행기: 임베디드 파이썬 → Play/PlayXyz/PlayXyzPath (IK·모션·경로 전부 라이브러리)
```

## 관련 프로젝트

- [OpenJigWare](https://github.com/ojw5014/OpenJigWare) — 본 예제가 사용하는 로봇 3D·제어 라이브러리
