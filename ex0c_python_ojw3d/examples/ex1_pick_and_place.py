# -*- coding: utf-8 -*-
# ============================================================
# 예제 1: 픽앤플레이스 — 파이썬으로 OpenJigWare 3D 로봇을 움직인다
#
# 모든 동작은 오픈지그웨어(OpenJigWare.dll) 함수다:
#   Play(시간ms, 딜레이ms, ID,각도, ID,각도, ...)   — 관절 이동 (강의 Play 형식)
#   PlayXyz(시간ms, 딜레이ms, 수식, x, y, z, Rx, Ry, Rz, 손목ID, 위치관절들)
#     — 툴 자세를 3축 각도로 지정한 TCP 이동 (IK 내장). Ry=90 이면 수직 아래
# 잡기/놓기는 실물처럼 그리퍼 여닫기만으로 된다 — 닫으면 잡히고(수평 12mm 이내), 열면 놓인다.
# scene 은 실물 로봇 통신 전용: open/close/torqon/torqoff/syncread
# ============================================================
import clr, os, sys
if "__file__" in dir():                              # 단독 실행(VS Code 등) — 상위 폴더의 DLL 경로 추가
    sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
clr.AddReference("OpenJigWare")                      # 오픈지그웨어 라이브러리
from OpenJigWare import Ojw

try:
    scene                                            # 실행기(3D 창) 안 — scene 이 준비되어 있다
except NameError:
    scene = Ojw.CScene_t.CreateOmx()                 # 단독 실행 — 3D 창 없이 동일 동작(실물 가능)

c3d = scene.c3d

GRIP_OPEN, GRIP_HOLD, GRIP_PEN = 30, 4, -4   # 그리퍼: 활짝 / 제품 굵기 / 펜 굵기

def home(ms=1200):
    c3d.Play(ms, 0, 11, 0, 12, -25, 13, 35, 14, 35, 15, 0)   # 홈 자세

def move(x, y, z, ms=1000):
    # 툴 자세 (Rx=0, Ry=90, Rz=0) = 수직 아래 — 손목(14)이 피치를 유지한다
    c3d.PlayXyz(ms, 0, 0, x, y, z, 0, 90, 0, 14, 11, 12, 13)

def grip(deg, ms=400):
    c3d.Play(ms, 0, 16, deg)                                  # 그리퍼 여닫기

# ── 픽앤플레이스 시퀀스 ──
# [실물 로봇] 아래 세 줄 맨 앞의 # 한 글자만 지우면 같은 코드로 실물 OMX 도 함께 움직인다
#scene.open(4, 1000000)       # 통신 열기 — U2D2 포트(COM4), 통신 속도 1Mbps
#scene.torqon()               # 토크 ON — 켜기 전 실물 자세를 읽어 3D 를 먼저 정렬(점프 없음)
#scene.syncread()             # 실물 관절각을 읽어 로그로 확인

print("픽앤플레이스 시작")
home(1200)
grip(GRIP_OPEN)

move(200, 110, 100, 1200)    # 1) 제품 위 경유점
move(200, 110, 45, 1000)     # 2) 내려가기
grip(GRIP_HOLD)              # 3) 닫아서 잡기 — 자동 파지, [GRIP] 로그 확인
move(200, 110, 100, 1000)    # 4) 들어올리기
move(210, -110, 100, 1400)   # 5) 목표 위로 이동
move(210, -110, 55, 1000)    # 6) 내려놓을 높이
grip(GRIP_OPEN)              # 7) 그리퍼 열어 놓기 — 자동 안착
move(210, -110, 100, 800)    # 8) 복귀 경유점
home(1200)

# [실물 로봇] 끝나면 아래 두 줄 맨 앞의 # 한 글자만 지워 토크를 끄고 통신을 닫는다
#scene.torqoff()              # 토크 OFF (로봇을 손으로 움직일 수 있게)
#scene.close()                # 통신 닫기
print("완료!")
