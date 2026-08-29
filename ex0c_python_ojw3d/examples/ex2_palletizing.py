# -*- coding: utf-8 -*-
# ============================================================
# 예제 2: 팔레타이징 — 제품(원통) 3개를 for 반복문으로 순차 적재
#
# 실행기(C#)가 이 코드를 같은 프로세스 안에서 실행하며 두 객체를 준다:
#   c3d   — 화면에 떠 있는 3D (Ojw.C3d 그대로)
# 잡기/놓기는 실물처럼 그리퍼 여닫기로 (닫으면 잡히고 열면 놓임), scene 은 실물 통신 전용
# 모든 알고리즘(수직 툴 IK·시간 보간 모션)은 OpenJigWare.dll 안에 있다 (Play/PlayXyz)
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

# ── 팔레타이징 시퀀스 ──
# [실물 로봇] 아래 세 줄 맨 앞의 # 한 글자만 지우면 같은 코드로 실물 OMX 도 함께 움직인다
#scene.open(4, 1000000)       # 통신 열기 — U2D2 포트(COM4), 통신 속도 1Mbps
#scene.torqon()               # 토크 ON — 켜기 전 실물 자세를 읽어 3D 를 먼저 정렬(점프 없음)
#scene.syncread()             # 실물 관절각을 읽어 로그로 확인

# ── PlayXyz 인자 읽는 법 ─────────────────────────────────────────────
# c3d.PlayXyz(시간ms, 딜레이ms, 수식번호, x, y, z, Rx, Ry, Rz, 손목모터ID, 위치관절ID 3개)
#   · 시간ms/딜레이ms   : 이동에 걸리는 시간 / 도착 후 잠깐 멈추는 시간
#   · 수식번호(0)       : 로봇 기구학 수식 번호 (이 로봇은 0 하나)
#   · x, y, z          : 로봇팔 끝(TCP = 그리퍼 중심)이 갈 좌표 (mm)
#   · Rx, Ry, Rz       : 툴(그리퍼) 자세 각도(도) — Ry=90 이면 수직 아래를 향한다
#   · 손목모터ID (14)   : 툴 자세(피치)를 만드는 손목 모터의 ID
#   · 위치관절 (11,12,13): TCP 위치를 만드는 관절 3개 — 각도는 역기구학이 계산
# c3d.Play(시간ms, 딜레이ms, ID,각도, ID,각도, ...) : 모터 ID 에 각도(도)를 직접 지정

# (제품 이름, 제품 x, 제품 y, 목표 x) — 목표 y는 모두 -110
products = [
    ("obj1", 200, 160, 170),
    ("obj2", 200, 110, 210),
    ("obj3", 200, 60, 250),
]

print("팔레타이징 시작 — 제품 %d개" % len(products))
home(1200)

for name, x, y, tx in products:
    print("- " + name + " 옮기는 중...")
    grip(GRIP_OPEN)
    move(x, y, 100, 1200)      # 제품 위
    move(x, y, 45, 1000)       # 내려가기
    grip(GRIP_HOLD)            # 제품 굵기만큼만 쥐기
    move(x, y, 100, 1000)      # 들어올리기
    move(tx, -110, 100, 1400)  # 목표 위로
    move(tx, -110, 55, 1000)   # 내려놓을 높이
    grip(GRIP_OPEN)
    move(tx, -110, 100, 800)

home(1200)

# [실물 로봇] 끝나면 아래 두 줄 맨 앞의 # 한 글자만 지워 토크를 끄고 통신을 닫는다
#scene.torqoff()              # 토크 OFF (로봇을 손으로 움직일 수 있게)
#scene.close()                # 통신 닫기
print("완료!")