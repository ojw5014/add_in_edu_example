# -*- coding: utf-8 -*-
# ============================================================
# 심플 예제 5: 사각형 그리기 (베지어 실시간 계산) — 예제 4와의 차이
#
# 예제 4는 미리 계산해 둔 좌표를 나열했지만, 여기서는 꼭짓점 4개만 주고
# 오픈지그웨어 라이브러리의 곡선 함수가 경로를 "실행 순간에" 계산한다:
#   Ojw.CMath.SampleCornersSmooth(꼭짓점, 점 개수, "30mm" 또는 "90%", 폐루프 여부)
#     — 산업용 코너 지정 방식 2종:
#       "30mm" = 코너 도착 30mm 전에서 직선을 멈추고 코너 지나 30mm 지점까지 베지어
#       "90%"  = 변의 90% 지점까지 직선, 남은 10%와 다음 변의 10%를 베지어로
#     폐루프(True)면 마지막 변에서 시작 코너까지도 둥글게 잇는다.
# 점 하나짜리 곡선 계산은 Ojw.CMath.Bez2(p0, 제어점, p1, u) 로도 가능하다.
# ============================================================
import clr, os, sys
if "__file__" in dir():                              # 단독 실행(VS Code 등) — 상위 폴더의 DLL 경로 추가
    sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
clr.AddReference("OpenJigWare")                      # 오픈지그웨어 라이브러리
from OpenJigWare import Ojw
from System.Collections.Generic import List
from System import Array, Single

try:
    scene                                            # 실행기(3D 창) 안 — scene 이 준비되어 있다
except NameError:
    scene = Ojw.CScene_t.CreateOmx()                 # 단독 실행 — 3D 창 없이 동일 동작(실물 가능)

c3d = scene.c3d

# [실물 로봇] 아래 세 줄 맨 앞의 # 한 글자만 지우면 같은 코드로 실물 OMX 도 함께 움직인다
#scene.open(4, 1000000)       # 통신 열기 — U2D2 포트(COM4), 통신 속도 1Mbps
#scene.torqon()               # 토크 ON — 켜기 전 실물 자세를 읽어 3D 를 먼저 정렬(점프 없음)
#scene.syncread()             # 실물 관절각을 읽어 로그로 확인

print("사각형 그리기 (베지어 실시간 계산)")
c3d.Play(1200, 0, 11, 0, 12, -25, 13, 35, 14, 35, 15, 0)             # 홈 자세

c3d.Play(400, 0, 16, 30)                                             # 그리퍼 활짝
c3d.PlayXyz(1200, 0, 0, 140, 170, 120, 0, 90, 0, 14, 11, 12, 13)     # 펜 거치대 위
c3d.PlayXyz(900, 0, 0, 140, 170, 61, 0, 90, 0, 14, 11, 12, 13)       # 내려가기
c3d.Play(400, 0, 16, -4)                                             # 펜 굵기만큼만 쥐기
c3d.PlayXyz(800, 0, 0, 140, 170, 120, 0, 90, 0, 14, 11, 12, 13)      # 들어올리기

# 꼭짓점 4개 + 시작점 반복(= 닫힌 도형) — 경로는 라이브러리가 실행 순간에 계산한다
Z = 63.5
corners = List[Array[Single]]()
corners.Add(Array[Single]([170.0, -35.0, Z]))
corners.Add(Array[Single]([250.0, -35.0, Z]))
corners.Add(Array[Single]([250.0, 35.0, Z]))
corners.Add(Array[Single]([170.0, 35.0, Z]))
corners.Add(Array[Single]([170.0, -35.0, Z]))    # 첫 점 반복 = 닫힌 도형

path = Ojw.CMath.SampleCornersSmooth(corners, 90, "30mm", True)   # ★ 코너 30mm 전부터 라운드 (폐루프)
# path = Ojw.CMath.SampleCornersSmooth(corners, 90, "90%", True)  #   퍼센트형 — 변의 90%까지 직선 후 라운드
print("- 라이브러리가 만든 경로 점: %d개" % path.Count)

pts = []
for p in path:                                       # PlayXyzPath 입력(x,y,z 반복)으로 펼치기
    pts += [p[0], p[1], p[2]]

c3d.PlayXyz(1200, 0, 0, pts[0], pts[1], 100, 0, 90, 0, 14, 11, 12, 13)   # 시작점 위
c3d.PlayXyz(800, 0, 0, pts[0], pts[1], Z, 0, 90, 0, 14, 11, 12, 13)      # 펜 내리기
c3d.PlayXyzPath(7000, 0, 0, pts, 0, 90, 0, 14, [11, 12, 13])             # 전체를 한 획으로
print("- 베지어 사각형(실시간 계산) 완료")

c3d.PlayXyz(800, 0, 0, pts[0], pts[1], 100, 0, 90, 0, 14, 11, 12, 13)    # 펜 들기
c3d.PlayXyz(1200, 0, 0, 140, 170, 120, 0, 90, 0, 14, 11, 12, 13)         # 거치대 위로
c3d.PlayXyz(900, 0, 0, 140, 170, 61, 0, 90, 0, 14, 11, 12, 13)           # 내려가기
c3d.Play(400, 0, 16, 30)                                                 # 그리퍼 열기 = 펜 반납
c3d.PlayXyz(800, 0, 0, 140, 170, 120, 0, 90, 0, 14, 11, 12, 13)          # 들어올리기
c3d.Play(1200, 0, 11, 0, 12, -25, 13, 35, 14, 35, 15, 0)                 # 홈 복귀

# [실물 로봇] 끝나면 아래 두 줄 맨 앞의 # 한 글자만 지워 토크를 끄고 통신을 닫는다
#scene.torqoff()              # 토크 OFF (로봇을 손으로 움직일 수 있게)
#scene.close()                # 통신 닫기
print("완료!")
