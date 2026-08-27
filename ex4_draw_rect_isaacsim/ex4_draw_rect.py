# -*- coding: utf-8 -*-
"""
ex4. 바닥에 사각형 그리기 — Isaac Sim (4.5+ / 5.x)
===================================================
ex2(MuJoCo)와 같은 작업을 Isaac Sim에서 수행한다. TCP 자취는 debug_draw로 표시.

실행 (Isaac Sim 동봉 파이썬 필수):
    <Isaac 설치 폴더>\python.bat ex4_draw_rect.py

※ 작성 환경에 Isaac Sim이 없어 실기 검증 전 상태입니다. API 오류가 나면
   common/omx_isaac.py 의 import 폴백/프림 경로(cand)를 우선 확인하세요.
"""
import os
import sys
import numpy as np

if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "common"))
import omx_isaac

HEADLESS = "--headless" in sys.argv
app = omx_isaac.boot(headless=HEADLESS)

import omx_kinematics as kin

# 자취 표시 (debug_draw — 버전별 폴백)
try:
    from isaacsim.util.debug_draw import _debug_draw
except ImportError:
    try:
        from omni.isaac.debug_draw import _debug_draw
    except ImportError:
        _debug_draw = None
draw = _debug_draw.acquire_debug_draw_interface() if _debug_draw else None

# ---------------- 사각형 정의 (ex2와 동일) ----------------
Z_DRAW = 0.03
Z_UP = 0.10
PITCH = -90.0
CORNERS = [(0.18, -0.05), (0.26, -0.05), (0.26, 0.05), (0.18, 0.05)]
EDGE_POINTS = 18
HOME = [0.0, -25.0, 35.0, 35.0]

sim = omx_isaac.OmxIsaacSim()
for i, (cx, cy) in enumerate(CORNERS, start=1):
    sim.add_marker(f"corner{i}", [cx, cy, 0.001], 0.012, (1.0, 0.9, 0.1))
sim.world.reset()

trace = []


def hook():
    trace.append(sim.tcp_pos().copy())
    if draw is not None and trace:
        pts = [tuple(p) for p in trace[::2]]
        draw.clear_points()
        draw.draw_points(pts, [(0.1, 0.4, 1.0, 1.0)] * len(pts), [4] * len(pts))


def solve(x, y, z, q_seed):
    q, ok, err = kin.ik([x, y, z], PITCH, q0_deg=q_seed, tol=5e-4)
    if not ok and err > 0.010:
        print(f"[WARN] IK 미수렴: ({x:.3f},{y:.3f},{z:.3f}) 오차={err*1000:.1f}mm")
    return q


print("=" * 60)
print("ex4. 바닥 사각형 그리기 (Isaac Sim)")
print("=" * 60)

grip = omx_isaac.GRIP_CLOSE_DEG   # 펜을 물었다고 가정
q_cur = np.array(HOME, dtype=float)
sim._apply(q_cur, grip)
sim.settle(grip, 1.0)

x0, y0 = CORNERS[0]
q_next = solve(x0, y0, Z_UP, q_cur);   q_cur = sim.move_arm(q_cur, q_next, grip, 1.2, hook=hook)
q_next = solve(x0, y0, Z_DRAW, q_cur); q_cur = sim.move_arm(q_cur, q_next, grip, 0.8, hook=hook)
print(f"[DRAW] 펜 내림 at ({x0:.2f}, {y0:.2f}, {Z_DRAW:.2f})")

for i in range(4):
    xa, ya = CORNERS[i]
    xb, yb = CORNERS[(i + 1) % 4]
    for s in np.linspace(0, 1, EDGE_POINTS + 1)[1:]:
        q_next = solve(xa + (xb - xa) * s, ya + (yb - ya) * s, Z_DRAW, q_cur)
        q_cur = sim.move_arm(q_cur, q_next, grip, 0.12, hook=hook)
    print(f"[DRAW] 변 {i+1}/4 완료 -> 꼭짓점 ({xb:.2f}, {yb:.2f})")

q_next = solve(*CORNERS[0], Z_UP, q_cur); q_cur = sim.move_arm(q_cur, q_next, grip, 0.8, hook=hook)
q_cur = sim.move_arm(q_cur, np.array(HOME), grip, 1.2, hook=hook)

# ---------------- 정확도 평가 (ex2와 동일 방식) ----------------
tr = np.array(trace)
draw_pts = tr[np.abs(tr[:, 2] - Z_DRAW) < 0.01]
print()
for i, (cx, cy) in enumerate(CORNERS, start=1):
    err = float(np.hypot(draw_pts[:, 0] - cx, draw_pts[:, 1] - cy).min())
    print(f"[RESULT] corner{i} target=({cx:.3f}, {cy:.3f}) 최근접 자취 오차={err*1000:.1f}mm")


def dist_to_rect(p):
    best = 1e9
    for i in range(4):
        a = np.array(CORNERS[i]); b = np.array(CORNERS[(i + 1) % 4])
        ab = b - a
        t = np.clip(np.dot(p - a, ab) / np.dot(ab, ab), 0, 1)
        best = min(best, float(np.linalg.norm(a + ab * t - p)))
    return best

path_err = [dist_to_rect(p[:2]) for p in draw_pts]
print(f"[SUMMARY] 자취 {len(draw_pts)}점, 경로 이탈 평균={np.mean(path_err)*1000:.1f}mm, "
      f"최대={np.max(path_err)*1000:.1f}mm")

if not HEADLESS:
    print("(Isaac Sim 창을 닫거나 Ctrl+C 로 종료)")
    while app.is_running():
        sim.step(1)
app.close()
