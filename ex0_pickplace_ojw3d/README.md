# ex0. OMX 픽앤플레이스 — OpenJigWare 3D (윈도우 프로그램)

작성일: 2026-08-20. C# WinForms + OpenJigWare 3D — 강의 "기초부터 시작하는 로봇 공학"의 도구·API 스타일 그대로 만든 시연/교육용 윈도우 프로그램.

## 실행

- 빌드된 실행 파일: `bin\Release\net48\OmxPickPlace.exe` (같은 폴더에 stl\, DLL 포함 — 폴더째 복사해도 동작)
- 버튼: **픽앤플레이스(제품 1개)** / **팔레타이징(제품 3개)** / **판 위에 사각형 그리기** / **리셋** + 속도(0.5/1/2배)
- 마우스 우클릭+드래그 = 카메라 회전
- 디버그: `OmxPickPlace.exe --probe` = 좌표 실측 프로브 표시

## 장면 구성

- **제품 = 원통(캔, 지름 24×높이 40)** 3개 — 원통이라 운반 중 자세 회전이 드러나지 않음. 간격 50mm로 그리퍼(개방 49mm)가 이웃과 닿지 않음
- **팔레트** + 적재 목표 원형 마킹(금색 디스크)
- **그리기 판(화이트보드, 80×70 — 그리는 사각형과 동일 크기)** — 빨간 라인이 판의 테두리를 따라 그려짐(테두리 색칠 느낌), 원통 제품과 안 겹침
- **펜 거치대 + 펜(지름 12)** — 사각형 그리기는 펜을 거치대에서 집어 **펜 굵기만큼만 파지**(GRIP_PEN=-10°)한 뒤 그리고 반납. 잉크 자취는 빨간 점으로 판에 남음
- **사각형 2종**: 직선(모서리에서 방향 전환) / **베지어 코너**(모서리를 둥글게 안 멈추고 통과) — ojwSimul에서 라이브러리로 옮겨진 `Ojw.CMath.SampleCornersLinear / SampleCornersBlend` 사용 (강의 Part3 궤적·코너 블렌딩)

## 검증 결과 (2026-08-20)

- 픽앤플레이스 1개: `[SUMMARY] success 1/1, mean err=0.0mm`
- 팔레타이징 3개(원통): 적재 err 0.0mm, 이웃 충돌 없음
- 판 테두리 사각형(직선): `[SUMMARY] 자취 256점, 꼭짓점 최대 오차=0.1mm`, 펜 파지→그리기→반납 완주
- 판 테두리 사각형(베지어 코너): 둥근 모서리 궤적 완주, 자연스러운 펜 그립 확인

## 구성 (교육 포인트)

| 파일 | 내용 |
|---|---|
| `OmxRobot.cs` | **교육용 API — 강의 실물 모터 명령과 동일 형식**: `Play(동작시간, 멈춤시간, ID, 각도, ...)`, `SyncRead()`, `CalcXyz()`(FK), `CalcInv(x,y,z,피치)`(IK). 내부는 변형 DH FK(Rz·Tz·Tx·Rx) + 수치 IK |
| `MainForm.cs` | 3D 초기화(ojwSimul 패턴), 장면(팔레트·상자·자취), 시나리오 — **"학생이 작성하는 부분" 주석 이후가 수업에서 다루는 코드** |
| DH 텍스트 | MakeUrdf 예제 'OMX Follower (Joints Only)' 변형 DH를 그대로 내장 — MakeDHSkeleton_Urdf로 렌더 |

### 학생 코드 예 (MainForm.cs의 시나리오부)

```csharp
float[] a;
m_Robot.CalcInv(220, 140, 100, -90, out a);            // 역기구학: 목표 -> 각도
m_Robot.Play(1200, 0, 11, a[0], 12, a[1], 13, a[2], 14, a[3]);   // 이동
m_Robot.Play(400, 0, 16, GRIP_HOLD);                    // 그리퍼 (모터 16)
float[] now = m_Robot.SyncRead();                       // 현재 각도 읽기
m_Robot.CalcXyz(out x, out y, out z);                   // 정기구학: 현재 TCP
```

이 명령들은 강의의 실물 제어(`m_CMot.Play(...)`, SyncRead)와 같은 형태라, 시뮬레이션에서 실물(다이나믹셀)로 넘어갈 때 코드 구조가 그대로 유지된다.

## 빌드 방법

```
MSBuild OmxPickPlace.csproj -t:Restore,Build -p:Configuration=Release
```
(VS2022 / .NET Framework 4.8, x86. 참조 DLL은 lib\에 포함: OpenJigWare.dll + Tao 4종)

## 구현 메모 (유지보수용)

- 3D 초기화·렌더 루프는 ojwSimul 패턴 (Init→SetAseFile_Path("stl")→MakeDHSkeleton_Urdf→CompileDesign, 33ms 타이머에서 User 오브젝트 재구성+OjwDraw)
- 스켈레톤 숨김: SetSkeletonView(false)+MakeDHSkeleton_Urdf의 bVisibleSkeleton=false — STL 메시만 표시
- **유저 박스(#0) 실측 규약**: Width→로봇X(중심), Height→로봇Y(0에서 −H), Depth→로봇Z(중심) — DrawBox가 보정. 구(#2)는 중심 기준
- 파지 = 부착 방식: TCP-상자 40mm 이내에서 잡으면 TCP 프레임에 고정 (MuJoCo ex1과 동일 개념)
- 모터 16(그리퍼)은 DH의 mimic 라인([16,1,0])이 방향까지 처리하므로 SetData 하나로 양 핑거가 함께 동작
