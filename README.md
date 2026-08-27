# OMX Follower 시뮬레이션 예제 (MuJoCo / Isaac Sim)

작성일: 2026-08-17. K-디지털 국비지원 강의용 예제 프로그램 모음.
로봇 모델은 **ojwSimul/MakeUrdf의 OMX Follower 예제(변형 DH)**를 MakeUrdf로 내보낸 것을 사용한다.

## 폴더 구성

| 폴더 | 내용 | 검증 상태 |
|---|---|---|
| `ex0_pickplace_ojw3d\` | **윈도우 프로그램 (C# + OpenJigWare 3D)** — 픽앤플레이스·팔레타이징·사각형 그리기. 제어 API = 강의 형식 그대로 `Play / SyncRead / CalcInv / CalcXyz` (상세: 폴더 내 README) | ✅ 실행 검증 (1개·3개 모두 err 0.0mm, 사각형 완주) |
| `ex0b_motiontable_ojw3d\` | **모션 테이블 실행기 (C# + OpenJigWare 3D)** — 예제 버튼 → 모션 그리드 생성 → Run으로 라이브러리 재생(PlayFrame+WaitAction). GUI 제외 전부 dll 함수 (상세: 폴더 내 README) | ✅ 실행 검증 (픽앤플레이스 안착·베지어 사각형 완주) |
| `ex0c_python_ojw3d\` | **파이썬 실행기 (C# + OpenJigWare 3D)** — 단일 파일 `robot_example.py`(팔레타이징+사각형 직선+베지어)를 열어 Run = `Ojw.CPython` 실행, omx.py 명령(move/grip/grab...)으로 로봇 구동. 환경·동작은 ex0과 동일 (상세: 폴더 내 README) | ✅ 실행 검증 (한 번의 Run으로 3개 동작 완주) |
| `model\` | OMX Follower 모델 — robot_stl.urdf(PyBullet/MuJoCo), robot_stl.xml(MJCF), robot_stl_isaac.urdf(Isaac용, mimic 제거), stl\ 메시 40종 | 생성·로딩 검증 완료 |
| `common\` | 공용 모듈 — omx_kinematics.py(변형 DH FK/IK), omx_mujoco.py(MuJoCo 헬퍼), omx_isaac.py(Isaac 헬퍼) | FK는 MuJoCo와 교차 검증(오차 0.000mm) |
| `ex1_pick_and_place_mujoco\` | **픽앤플레이스** (MuJoCo) — `--multi`로 3개 팔레타이징 | ✅ 실행 검증 (3개: 3/3, **평균 오차 0.000m**) |
| `ex2_draw_rect_mujoco\` | **바닥 사각형 그리기** (MuJoCo) — 자취 시각화 | ✅ 실행 검증 (꼭짓점 최대 0.8mm, 경로 이탈 평균 0.1mm) |
| `ex3_pick_and_place_isaacsim\` | 픽앤플레이스 (Isaac Sim) | ⚠ 미검증 (Isaac 미설치 환경에서 작성) |
| `ex4_draw_rect_isaacsim\` | 바닥 사각형 그리기 (Isaac Sim) | ⚠ 미검증 |

## 실행 방법

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
   - IK: 수치 DLS — 목표 = TCP 위치(x,y,z) + 접근 피치(위에서 집기 = -90°)
   - 정지자세 TCP (306.7, 0, 210.7)mm — ojwSimul 검증값과 일치
```

- 관절: T11(베이스 요) T12(어깨) T13(팔꿈치) T14(손목 피치) + T15(툴 롤) + T16(그리퍼, mimic 3개)
- 그리퍼: T16 +30°=열림(간격 49mm) / +8°=3cm 상자 파지폭 / -25°=완전 닫힘
- 파지: 확정적 부착(attach) 방식 — TCP와 물체 거리 40mm 이내에서 그리퍼를 닫으면
  물체를 손목 프레임에 고정 (초급 예제용 단순화, 흡착 그리퍼에 대응). 놓으면 물리 낙하.

## 모델 재생성 방법

1. `MakeUrdf.exe` 실행 (d:\bin\cs\OpenJigWare\trunk\OpenJigWare\MakeUrdf\bin\Release\)
2. DH Parameters에 OMX Follower(Joints Only) 예제 텍스트 입력(Example 콤보 또는 직접 입력) → Apply DH
3. 하단 View 콤보를 **STL**로 (현재 model\은 STL 전용 내보내기 — Skeleton이면 메시가 안 들어가고, All이면 스켈레톤 프리미티브가 같이 들어감)
4. **View URDF** 클릭 → `bin\Release\urdf\`의 산출물을 이 폴더 `model\`로 복사
