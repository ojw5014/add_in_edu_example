# -*- coding: utf-8 -*-
# ============================================================
# 예제 6: 팔레타이징 2 — 아치(ARCH)·경유(PASS) 설정으로 짧게 쓰기
#
# 예제 2와 같은 작업이지만, 옮기는 한 번이 "이동 한 줄"이다.
#   · 설정은 미리 한 번:  PlayXyz_Param_Tcp / _TcpId / _Arch / _Pass
#   · 동작은 좌표만:      c3d.PlayXyz(시간ms, 딜레이ms, 수식번호, x, y, z)
#   · ARCH  = 지정한 축(x/y/z)으로 높이만큼 들어올려 → 이동 → 내려놓는 자동 경로
#   · PASS  = 경로 코너를 둥글게 통과 ("20mm" = 코너 앞뒤 20mm 를 곡선으로,
#             "80%" = 변의 80%까지 직선 후 곡선). 설정 안 하면 코너가 각진다.
# 예제 2의 반복 블록(9줄)이 5줄로 줄어드는 것을 비교해 보자.
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

GRIP_OPEN, GRIP_HOLD = 30, 4                 # 그리퍼: 활짝 / 제품 굵기

def home(ms=1200):
    c3d.Play(ms, 0, 11, 0, 12, -25, 13, 35, 14, 35, 15, 0)   # 홈 자세

def move(x, y, z, ms=2600):
    c3d.PlayXyz(ms, 0, 0, x, y, z)           # 축약형 — 자세·아치·경유는 미리 한 설정을 따른다

def grip(deg, ms=400):
    c3d.Play(ms, 0, 16, deg)                 # 그리퍼 여닫기

# ── 팔레타이징 시퀀스 ──
# [실물 로봇] 아래 세 줄 맨 앞의 # 한 글자만 지우면 같은 코드로 실물 OMX 도 함께 움직인다
#scene.open(4, 1000000)       # 통신 열기 — U2D2 포트(COM4), 통신 속도 1Mbps
#scene.torqon()               # 토크 ON — 켜기 전 실물 자세를 읽어 3D 를 먼저 정렬(점프 없음)
#scene.syncread()             # 실물 관절각을 읽어 로그로 확인

# (제품 이름, 제품 x, 제품 y, 목표 x) — 목표 y는 모두 -110
products = [
    ("obj1", 200, 160, 170),
    ("obj2", 200, 110, 210),
    ("obj3", 200, 60, 250),
]

print("팔레타이징 2 — ARCH/PASS 설정 사용, 제품 %d개" % len(products))
home(1200)
c3d.PlayXyz(1500, 0, 0, 200, 160, 100)       # 시작 위치(obj1 상공)로 — 보통 이동

# ── 설정은 여기서 한 번만 ────────────────────────────────────────────
c3d.PlayXyz_Param_Tcp(0, 0, 90, 0)           # 툴 자세: Ry=90 = 수직 아래
c3d.PlayXyz_Param_TcpId(0, 14, 11, 12, 13)   # 손목 모터 14, 위치 관절 11·12·13
c3d.PlayXyz_Param_Arch(0, 45, "z")           # 아치: z 축으로 45mm 들어올려 이동
                                             # (높이는 로봇이 닿는 범위 안으로 — 너무 높이면 팔이 못 닿는다)
c3d.PlayXyz_Param_Pass(0, "20mm")            # 경유: 코너 앞뒤 20mm 를 둥글게

# ── 이동은 좌표 한 줄 — 들어올리기·수평이동·내려놓기는 아치가 알아서 ──
for name, x, y, tx in products:
    print("- " + name + " 옮기는 중...")
    grip(GRIP_OPEN)
    move(x, y, 45)               # 제품 위치로 (첫 회는 제자리 수직 = 직선)
    grip(GRIP_HOLD)              # 쥐기
    move(tx, -110, 55)           # 목표로 — 아치 한 줄
    grip(GRIP_OPEN)              # 놓기

c3d.PlayXyz_Param_Arch(0, 0)                 # 아치 해제 (0 = 끄기)
c3d.PlayXyz_Param_Pass(0, None)              # 경유 해제
c3d.PlayXyz(800, 0, 0, 250, -110, 100)       # 위로 빠지기 (직선)
home(1200)

# [실물 로봇] 끝나면 아래 두 줄 맨 앞의 # 한 글자만 지워 토크를 끄고 통신을 닫는다
#scene.torqoff()              # 토크 OFF (로봇을 손으로 움직일 수 있게)
#scene.close()                # 통신 닫기
print("완료!")
