# -*- coding: utf-8 -*-
"""
ex2. 바닥에 사각형 그리기 — MuJoCo
===================================
OMX Follower의 TCP(펜 끝)를 바닥 위 일정 높이에서 사각형 경로로 움직이며
실제 지나간 자취를 기록·표시한다. (직선 보간 + IK 연속 풀이)

실행:
    python ex2_draw_rect.py             # 뷰어(창)로 보기 — 파란 자취가 실시간 표시
    python ex2_draw_rect.py --headless  # 창 없이 실행 + PNG 캡처 저장

뷰어 키: R = 처음부터 다시 그리기 / C = 자취 지우고 리셋 (창 닫기 = 종료)

학습 포인트: 경로(Path)를 잘게 나눈 웨이포인트마다 IK를 풀면 궤적(Trajectory)이 된다.
"""
import os
import sys
import numpy as np

if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # 한글 콘솔(cp949) 대응

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "common"))
import omx_kinematics as kin
import omx_mujoco as om
import mujoco

HEADLESS = "--headless" in sys.argv
SPEED = 1.0                       # 재생 속도 (1.0=실시간, 0.5=절반 속도) — --speed 0.5 식으로 지정
for _i, _a in enumerate(sys.argv):
    if _a == "--speed" and _i + 1 < len(sys.argv):
        SPEED = float(sys.argv[_i + 1])
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "output")
os.makedirs(OUT_DIR, exist_ok=True)

# ---------------- 사각형 정의 ----------------
Z_DRAW = 0.03      # 그리기 높이 (펜이 바닥 위 3cm)
Z_UP = 0.10        # 이동 높이 (펜 들기)
PITCH = -90.0      # 펜이 수직 아래를 향하도록
CORNERS = [        # 사각형 꼭짓점 (x, y) — 시계 방향 한 바퀴
    (0.18, -0.05),
    (0.26, -0.05),
    (0.26,  0.05),
    (0.18,  0.05),
]
EDGE_POINTS = 18   # 변 하나를 나누는 웨이포인트 수
HOME = [0.0, -25.0, 35.0, 35.0]

# ---------------- 장면: 꼭짓점 마커 ----------------
scene = []
for i, (cx, cy) in enumerate(CORNERS, start=1):
    scene.append(f'<geom name="corner{i}" type="cylinder" size="0.006 0.0004" pos="{cx} {cy} 0.001" '
                 f'rgba="1 0.9 0.1 1" contype="0" conaffinity="0"/>')

m = om.load_model("\n".join(scene))
sim = om.OmxSim(m)
sim.realtime_speed = 0.0 if HEADLESS else SPEED   # 뷰어 모드는 실시간 재생
cam = om.default_camera()
cam.lookat[:] = [0.21, 0.0, 0.06]
cam.distance = 0.75
renderer = mujoco.Renderer(m, height=720, width=960) if HEADLESS else None

FLAG = {"run": False, "reset": False}


def on_key(keycode):
    if keycode in (ord("R"), ord("r")):
        FLAG["run"] = True
    elif keycode in (ord("C"), ord("c")):
        FLAG["reset"] = True


viewer = None
if not HEADLESS:
    import mujoco.viewer
    viewer = mujoco.viewer.launch_passive(m, sim.d, key_callback=on_key)

trace = []          # 실제 TCP 자취 (m)


def sync():
    if viewer is not None:
        viewer.user_scn.ngeom = 0
        if trace:
            om.add_markers(viewer.user_scn, trace[::2])
        viewer.sync()


def record():
    p, _ = sim.tcp_pose()
    trace.append(p.copy())


def capture(tag):
    if renderer is None:
        return
    renderer.update_scene(sim.d, camera=cam)
    om.add_markers(renderer.scene, trace[::2])
    om.save_png(os.path.join(OUT_DIR, f"ex2_{tag}.png"), renderer.render())
    print(f"[CAPTURE] {tag}.png")


def hook():
    record()
    sync()


def solve(x, y, z, q_seed, tol=1e-4):
    q, ok, err = kin.ik([x, y, z], PITCH, q0_deg=q_seed, tol=tol)
    if not ok and err > 0.010:
        print(f"[WARN] IK 미수렴: ({x:.3f},{y:.3f},{z:.3f}) 오차={err*1000:.1f}mm")
    return q


def reset_scene():
    """자취 지우고 팔 홈 복귀."""
    trace.clear()
    sim.set_grip(om.GRIP_CLOSE_DEG)
    sim.move_arm(HOME, 1.0, hook=sync)
    sim.settle(0.4, hook=sync)
    print("[RESET] 자취 삭제·초기 자세 복귀 완료")


def run_task(first=False):
    """사각형 그리기 전체 시퀀스 1회 수행."""
    print("=" * 60)
    print("ex2. 바닥 사각형 그리기 —",
          f"{(CORNERS[1][0]-CORNERS[0][0])*100:.0f}cm x {(CORNERS[2][1]-CORNERS[1][1])*100:.0f}cm")
    print("=" * 60)
    sim.set_grip(om.GRIP_CLOSE_DEG)   # 펜을 물었다고 가정 — 그리퍼 닫음
    sim.set_arm_ctrl(HOME)
    sim.settle(0.8, hook=sync)
    if first:
        capture("00_start")

    # 1) 시작 꼭짓점 위 -> 펜 내리기
    q_cur = np.array(HOME, dtype=float)
    x0, y0 = CORNERS[0]
    q_cur = solve(x0, y0, Z_UP, q_cur); sim.move_arm(q_cur, 1.2, hook=sync)
    q_cur = solve(x0, y0, Z_DRAW, q_cur); sim.move_arm(q_cur, 0.8, hook=sync)
    print(f"[DRAW] 펜 내림 at ({x0:.2f}, {y0:.2f}, {Z_DRAW:.2f})")

    # 2) 사각형 4변 그리기 — 변마다 직선 보간 + 연속 IK
    for i in range(4):
        xa, ya = CORNERS[i]
        xb, yb = CORNERS[(i + 1) % 4]
        for s in np.linspace(0, 1, EDGE_POINTS + 1)[1:]:
            x = xa + (xb - xa) * s
            y = ya + (yb - ya) * s
            q_cur = solve(x, y, Z_DRAW, q_cur, tol=5e-4)
            sim.move_arm(q_cur, 0.12, hook=hook)
        print(f"[DRAW] 변 {i+1}/4 완료 -> 꼭짓점 ({xb:.2f}, {yb:.2f})")
        if first and i == 1:
            capture("01_half")

    # 3) 펜 들고 홈 복귀
    q_cur = solve(*CORNERS[0], Z_UP, q_cur); sim.move_arm(q_cur, 0.8, hook=sync)
    sim.move_arm(HOME, 1.2, hook=sync)
    sim.settle(0.4, hook=sync)
    if first:
        capture("02_done")

    # ---------------- 정확도 평가 ----------------
    tr = np.array(trace)
    draw_pts = tr[np.abs(tr[:, 2] - Z_DRAW) < 0.01]   # 그리기 높이 부근 자취만
    print()
    max_corner_err = 0.0
    for i, (cx, cy) in enumerate(CORNERS, start=1):
        dists = np.hypot(draw_pts[:, 0] - cx, draw_pts[:, 1] - cy)
        err = float(dists.min())
        max_corner_err = max(max_corner_err, err)
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
          f"최대={np.max(path_err)*1000:.1f}mm, 꼭짓점 최대 오차={max_corner_err*1000:.1f}mm")


# ---------------- 실행 ----------------
run_task(first=True)

if viewer is not None:
    print("\n키: R = 처음부터 다시 그리기 / C = 자취 지우고 리셋 (창 닫기 = 종료)")
    import time
    while viewer.is_running():
        if FLAG["run"]:
            FLAG["run"] = False
            reset_scene()
            run_task()
            print("\n키: R = 다시 그리기 / C = 리셋 (창 닫기 = 종료)")
        elif FLAG["reset"]:
            FLAG["reset"] = False
            reset_scene()
        time.sleep(0.1)
