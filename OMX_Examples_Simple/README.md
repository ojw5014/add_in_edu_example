# OMX Examples Simple — 한 줄에 한 명령 (OpenJigWare 3D)

ex0c(`ex0c_python_ojw3d`)와 **같은 실행기·같은 장면·같은 동작**이지만, 예제 파이썬 코드를
**가장 단순한 형태**로 다시 쓴 버전이다.

## ex0c 예제와의 차이

| | ex0c (본 예제) | OMX_Examples_Simple (이 폴더) |
|---|---|---|
| 스타일 | `def home()/move()/grip()` 로 감싸고 for 반복문 사용 | **감싸는 함수 없음 — 한 줄에 한 명령** |
| 명령 | 함수 안에서 라이브러리 호출 | **라이브러리 함수를 그대로 호출** (`c3d.Play`, `c3d.PlayXyz`, `c3d.PlayXyzPath`) |
| 팔레타이징 | for 문으로 3개 반복 | 3개 블록을 전부 풀어 씀 (반복이 보이면 for 로 줄일 수 있다는 것을 배우는 단계) |
| 베지어 | 수식(bez2)으로 점 생성 | 점 좌표를 한 줄에 하나씩 나열 |

새 함수·새 모듈 파일 없음. 딜레이는 `Play(시간, 딜레이ms, ...)`의 둘째 인자,
단순 대기는 `Ojw.CTimer.Wait(ms)`, 실물 로봇은 `scene.open/torqon/syncread/close`
(주석만 해제하면 실물 동시 구동) — 전부 기존 함수다.

## 사용하는 명령 요약

```python
c3d.Play(1200, 0, 11, 0, 12, -25, ...)                        # 관절 이동 (시간ms, 딜레이ms, ID,각도,...)
c3d.PlayXyz(1000, 0, 0, x, y, z, 0, 90, 0, 14, 11, 12, 13)    # TCP 이동 (Ry=90 = 수직 아래)
c3d.PlayXyzPath(1800, 0, 0, [x, y, z], 0, 90, 0, 14, [11, 12, 13])  # 한 획 긋기 (그리기용)
Ojw.CMath.SampleCornersBlend(corners, 90)                      # 꼭짓점 → 베지어 라운드 경로 런타임 생성 (예제 5)
Ojw.CTimer.Wait(300)                                           # 대기
# 잡기/놓기 명령 없음 — 그리퍼 닫기 = 잡기(수평 12mm 이내), 열기 = 놓기 (자동)
# 펜도 동일 — 거치대에서 닫으면 파지, 잉크는 펜 끝이 판에 닿으면 자동
scene.open(4, 1000000) / scene.torqon() / scene.syncread() / scene.close()   # 실물 로봇 (scene = Ojw.CScene_t)
```

## 빌드·실행

- 빌드: `MSBuild OmxExamplesSimple.csproj /p:Configuration=Release` (net48 **x64**)
- 빌드 후 `lib\*.dll` 을 `bin\{구성}\net48\` 에 복사 (pythonnet 이 의존 어셈블리를 전부 요구)
- 필요 환경: python 3.10 또는 3.11 (x64, PATH — 3.12 이상은 pythonnet 호환 미검증), `pip install pythonnet`
- 실행기 사용법·아키텍처·함정 기록은 `..\ex0c_python_ojw3d\README.md` 참조 (실행기 소스 동일)
