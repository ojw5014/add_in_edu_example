# -*- coding: utf-8 -*-
"""
ex3. 로봇팔 픽앤플레이스 — Isaac Sim (4.5+ / 5.x)
==================================================
ex1(MuJoCo)과 같은 작업을 Isaac Sim에서 수행한다.
로봇: robot_stl_isaac.urdf (MakeUrdf가 Isaac용으로 내보낸 URDF, mimic 제거판)

실행 (Isaac Sim 동봉 파이썬 필수):
    <Isaac 설치 폴더>\python.bat ex3_pick_and_place.py
    <Isaac 설치 폴더>\python.bat ex3_pick_and_place.py --multi   # 상자 3개 팔레타이징

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
MULTI = "--multi" in sys.argv

app = omx_isaac.boot(headless=HEADLESS)   # 다른 omni import보다 반드시 먼저!

import omx_kinematics as kin  # 순수 numpy — Isaac 파이썬에서도 동일하게 동작

# ---------------- 작업 정의 (ex1과 동일) ----------------
if MULTI:
    OBJECTS = [("obj1", 0.22, 0.14, (0.85, 0.20, 0.20)),
               ("obj2", 0.22, 0.08, (0.20, 0.75, 0.25)),
               ("obj3", 0.22, 0.02, (0.25, 0.35, 0.90))]
    TARGETS = [(0.17, -0.11), (0.21, -0.11), (0.25, -0.11)]
else:
    OBJECTS = [("obj1", 0.22, 0.10, (0.85, 0.20, 0.20))]
    TARGETS = [(0.21, -0.11)]

CUBE_HALF = 0.015
Z_APPROACH = 0.10
Z_GRASP = 0.035
Z_PLACE = 0.045
PITCH = -90.0
HOME = [0.0, -25.0, 35.0, 35.0]
TOL_OK = 0.05

sim = omx_isaac.OmxIsaacSim()

# 장면: 팔레트 + 목표 마커 + 상자
sim.add_static_box("pallet", [0.21, -0.11, 0.005], [0.15, 0.11, 0.01], (0.55, 0.36, 0.18))
for i, (tx, ty) in enumerate(TARGETS, start=1):
    sim.add_marker(f"tmark{i}", [tx, ty, 0.0105], 0.024, (1.0, 1.0, 0.2))
cubes = {}
for name, ox, oy, color in OBJECTS:
    cubes[name] = sim.add_cube(name, [ox, oy, CUBE_HALF + 0.001], CUBE_HALF, color)

sim.world.reset()


def solve(x, y, z, q_seed):
    q, ok, err = kin.ik([x, y, z], PITCH, q0_deg=q_seed)
    if not ok and err > 0.010:
        print(f"[WARN] IK 미수렴: ({x:.3f},{y:.3f},{z:.3f}) 오차={err*1000:.1f}mm")
    return q


print("=" * 60)
print("ex3. 픽앤플레이스 (Isaac Sim) —", "3개 순차 적재" if MULTI else "상자 1개")
print("=" * 60)

grip = omx_isaac.GRIP_OPEN_DEG
q_cur = np.array(HOME, dtype=float)
sim._apply(q_cur, grip)
sim.settle(grip, 1.0)

results = []
for (name, ox, oy, _c), (tx, ty) in zip(OBJECTS, TARGETS):
    print(f"\n--- {name}: ({ox:.2f},{oy:.2f}) -> ({tx:.2f},{ty:.2f}) ---")
    q_next = solve(ox, oy, Z_APPROACH, q_cur); q_cur = sim.move_arm(q_cur, q_next, grip, 1.2)
    q_next = solve(ox, oy, Z_GRASP, q_cur);    q_cur = sim.move_arm(q_cur, q_next, grip, 1.0)

    # 파지: 부착 -> 파지폭으로 닫기 (ex1과 동일한 확정적 파지)
    cp, _ = cubes[name].get_world_pose()
    gap = float(np.linalg.norm(sim.tcp_pos() - np.asarray(cp, dtype=float)))
    if gap < 0.04:
        sim.attach(cubes[name])
        grip = omx_isaac.GRIP_HOLD_DEG
        sim._apply(q_cur, grip)
        sim.settle(grip, 0.5)
        print(f"[GRIP] {name} 파지 성공 (TCP-물체 거리 {gap*1000:.0f}mm)")
    else:
        print(f"[GRIP] {name} 파지 실패 (거리 {gap*1000:.0f}mm > 40mm)")

    q_next = solve(ox, oy, Z_APPROACH, q_cur); q_cur = sim.move_arm(q_cur, q_next, grip, 1.0)
    q_next = solve(tx, ty, Z_APPROACH, q_cur); q_cur = sim.move_arm(q_cur, q_next, grip, 1.4)
    q_next = solve(tx, ty, Z_PLACE, q_cur);    q_cur = sim.move_arm(q_cur, q_next, grip, 1.0)

    sim.release()
    grip = omx_isaac.GRIP_OPEN_DEG
    sim._apply(q_cur, grip)
    sim.settle(grip, 0.8)
    q_next = solve(tx, ty, Z_APPROACH, q_cur); q_cur = sim.move_arm(q_cur, q_next, grip, 0.8)

    fp, _ = cubes[name].get_world_pose()
    fp = np.asarray(fp, dtype=float)
    err = float(np.hypot(fp[0] - tx, fp[1] - ty))
    ok = err <= TOL_OK
    results.append((name, err, ok))
    print(f"[RESULT] {name} final=({fp[0]:.3f}, {fp[1]:.3f}, {fp[2]:.3f}) "
          f"target=({tx:.3f}, {ty:.3f}, 0.025) err={err:.3f}m -> {'OK' if ok else 'FAIL'}")

q_cur = sim.move_arm(q_cur, np.array(HOME), grip, 1.2)
n_ok = sum(1 for r in results if r[2])
print(f"\n[SUMMARY] success {n_ok}/{len(results)}, mean err={np.mean([r[1] for r in results]):.3f} m")

if not HEADLESS:
    print("(Isaac Sim 창을 닫거나 Ctrl+C 로 종료)")
    while app.is_running():
        sim.step(1)
app.close()
