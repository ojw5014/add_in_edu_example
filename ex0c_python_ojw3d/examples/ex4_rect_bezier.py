# -*- coding: utf-8 -*-
# ============================================================
# 예제 4: 판 테두리 사각형 그리기 (베지어 코너) — 모서리를 둥글게 통과
#
# 실행기(C#)가 이 코드를 같은 프로세스 안에서 실행하며 두 객체를 준다:
#   c3d   — 화면에 떠 있는 3D (Ojw.C3d 그대로)
# 잡기/놓기는 실물처럼 그리퍼 여닫기로 (닫으면 잡히고 열면 놓임), scene 은 실물 통신 전용
# 모든 알고리즘(수직 툴 IK·모션·경로 추종)은 OpenJigWare.dll 안에 있다 (Play/PlayXyz/PlayXyzPath)
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

# ── 베지어 사각형 시퀀스 ──
# [실물 로봇] 아래 세 줄 맨 앞의 # 한 글자만 지우면 같은 코드로 실물 OMX 도 함께 움직인다
#scene.open(4, 1000000)       # 통신 열기 — U2D2 포트(COM4), 통신 속도 1Mbps
#scene.torqon()               # 토크 ON — 켜기 전 실물 자세를 읽어 3D 를 먼저 정렬(점프 없음)
#scene.syncread()             # 실물 관절각을 읽어 로그로 확인

Z_DRAW = 63.5   # 펜 끝이 판에 닿는 TCP 높이
BLEND = 18      # 모서리에서 잘라낼 길이(mm) — 클수록 둥글다
corners = [(170, -35), (250, -35), (250, 35), (170, 35)]


def bez2(p0, c, p1, u):
    """2차 베지어 — 시작 p0, 제어점 c(=모서리), 끝 p1 (강의 Part3 궤적)"""
    a = (1 - u) * (1 - u)
    b = 2 * u * (1 - u)
    d = u * u
    return (a * p0[0] + b * c[0] + d * p1[0],
            a * p0[1] + b * c[1] + d * p1[1])


def cut(p_from, p_to, dist):
    """p_from에서 p_to 방향으로 dist만큼 간 점"""
    dx, dy = p_to[0] - p_from[0], p_to[1] - p_from[1]
    length = (dx * dx + dy * dy) ** 0.5
    return (p_from[0] + dx / length * dist, p_from[1] + dy / length * dist)


print("사각형 그리기 (베지어 코너)")
home(1200)

# 1) 펜 잡기 — 펜 굵기만큼만 쥔다
grip(GRIP_OPEN)
move(140, 170, 120, 1200)
move(140, 170, 61, 900)
grip(GRIP_PEN)
move(140, 170, 120, 800)

# 2) 경로 만들기: 변의 직선 구간(끝점만) + 모서리의 베지어 라운드(촘촘히)
#    — 점 사이 연속 보간은 DLL 이 하므로 직선은 끝점이면 충분하다
path = []
for i in range(4):
    a = corners[i]
    b = corners[(i + 1) % 4]
    p_in = cut(a, b, BLEND)          # 모서리 a를 빠져나오는 점
    p_out = cut(b, a, BLEND)         # 모서리 b로 들어가는 점
    path.append(p_in)
    path.append(p_out)
    nxt = cut(b, corners[(i + 2) % 4], BLEND)
    for s in range(1, 21):           # 모서리 b의 베지어 라운드 (곡선이라 촘촘히)
        path.append(bez2(p_out, b, nxt, s / 20.0))
# 마지막 라운드의 끝 = path[0] — 닫힌 도형

# 3) 경로 그리기 — 시작점으로 내려가 전체를 한 획으로 (멈춤 없이, 경로 추종은 DLL)
x0, y0 = path[0]
move(x0, y0, 100, 1200)
move(x0, y0, Z_DRAW, 800)
pts = []
for x, y in path[1:]:
    pts += [x, y, Z_DRAW]
c3d.PlayXyzPath(7000, 0, 0, pts, 0, 90, 0, 14, [11, 12, 13])
print("- 베지어 사각형 완료")

# 4) 펜 반납
move(x0, y0, 100, 800)
move(140, 170, 120, 1200)
move(140, 170, 61, 900)
grip(GRIP_OPEN)
move(140, 170, 120, 800)
home(1200)

# [실물 로봇] 끝나면 아래 두 줄 맨 앞의 # 한 글자만 지워 토크를 끄고 통신을 닫는다
#scene.torqoff()              # 토크 OFF (로봇을 손으로 움직일 수 있게)
#scene.close()                # 통신 닫기
print("완료!")