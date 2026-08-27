# -*- coding: utf-8 -*-
"""
ex1. 로봇팔 픽앤플레이스 — MuJoCo
==================================
OMX Follower(변형 DH -> MakeUrdf 변환 모델)로 상자를 집어 팔레트 목표 위치에 놓는다.

실행:
    python ex1_pick_and_place.py             # 뷰어(창)로 보기
    python ex1_pick_and_place.py --headless  # 창 없이 실행 + PNG 캡처 저장
    python ex1_pick_and_place.py --multi     # 상자 3개 순차 적재(팔레타이징)

뷰어 키: R = 처음부터 다시 실행 / C = 초기 상태로 리셋 (창 닫기 = 종료)

체인: 변형 DH(강의 Part3) -> IK(수치, DLS) -> 위치 액추에이터 -> MuJoCo 물리
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
MULTI = "--multi" in sys.argv
SPEED = 1.0                       # 재생 속도 (1.0=실시간, 0.5=절반 속도) — --speed 0.5 식으로 지정
for _i, _a in enumerate(sys.argv):
    if _a == "--speed" and _i + 1 < len(sys.argv):
        SPEED = float(sys.argv[_i + 1])
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "output")
os.makedirs(OUT_DIR, exist_ok=True)

# ---------------- 작업 정의 ----------------
if MULTI:
    OBJECTS = [  # (이름, 시작 x, y, 색), 순차 적재
        ("obj1", 0.22, 0.14, "0.85 0.20 0.20"),
        ("obj2", 0.22, 0.08, "0.20 0.75 0.25"),
        ("obj3", 0.22, 0.02, "0.25 0.35 0.90"),
    ]
    TARGETS = [(0.17, -0.11), (0.21, -0.11), (0.25, -0.11)]
else:
    OBJECTS = [("obj1", 0.22, 0.10, "0.85 0.20 0.20")]
    TARGETS = [(0.21, -0.11)]

CUBE_HALF = 0.015          # 상자 반변(3cm 정육면체)
Z_APPROACH = 0.10          # 접근 높이 (물체/목표 위 경유점)
Z_GRASP = 0.035            # 집는 높이 (TCP 기준)
Z_PLACE = 0.045            # 놓는 높이 (상자 바닥이 팔레트에 살짝 닿는 높이)
PITCH = -90.0              # 위에서 아래로 집는 자세
HOME = [0.0, -25.0, 35.0, 35.0]
TOL_OK = 0.05              # 판정 허용 오차 (m) — 채점표 기준과 동일

# ---------------- 장면 구성 ----------------
scene = ['<body name="pallet" pos="0.21 -0.11 0.005">'
         '<geom name="pallet_geom" type="box" size="0.075 0.055 0.005" rgba="0.55 0.36 0.18 1"/></body>']
for i, (tx, ty) in enumerate(TARGETS, start=1):
    scene.append(f'<geom name="tmark{i}" type="cylinder" size="0.012 0.0006" pos="{tx} {ty} 0.0105" '
                 f'rgba="1 1 0.2 1" contype="0" conaffinity="0"/>')
for name, ox, oy, rgba in OBJECTS:
    scene.append(f'<body name="{name}" pos="{ox} {oy} {CUBE_HALF + 0.001}">'
                 f'<freejoint name="{name}_free"/>'
                 f'<geom name="{name}_geom" type="box" size="{CUBE_HALF} {CUBE_HALF} {CUBE_HALF}" '
                 f'rgba="{rgba} 1" mass="0.03"/></body>')

m = om.load_model("\n".join(scene))
sim = om.OmxSim(m)
sim.realtime_speed = 0.0 if HEADLESS else SPEED   # 뷰어 모드는 실시간 재생
cam = om.default_camera()
renderer = mujoco.Renderer(m, height=720, width=960) if HEADLESS else None

# 뷰어 키 입력: R=다시 실행, C=리셋
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

# 물체 초기 상태 (리셋용)
OBJ_JOINTS = {}
for name, ox, oy, rgba in OBJECTS:
    jid = mujoco.mj_name2id(m, mujoco.mjtObj.mjOBJ_JOINT, name + "_free")
    OBJ_JOINTS[name] = (jid, m.jnt_qposadr[jid], np.array([ox, oy, CUBE_HALF + 0.001]))


def sync():
    if viewer is not None:
        viewer.sync()


def capture(tag):
    if renderer is None:
        return
    renderer.update_scene(sim.d, camera=cam)
    om.save_png(os.path.join(OUT_DIR, f"ex1_{tag}.png"), renderer.render())
    print(f"[CAPTURE] {tag}.png")


def solve(x, y, z, q_seed):
    q, ok, err = kin.ik([x, y, z], PITCH, q0_deg=q_seed)
    if not ok and err > 0.010:  # 10mm 이상 벗어날 때만 경고
        print(f"[WARN] IK 미수렴: 목표=({x:.3f},{y:.3f},{z:.3f}) 오차={err*1000:.1f}mm")
    return q


def log_pose(tag):
    p, pitch = kin.fk_pos_pitch(np.degrees([sim.d.ctrl[sim.act[n]] for n in om.ARM_ACTS]))
    print(f"[POSE] {tag}: TCP 목표=({p[0]:.3f},{p[1]:.3f},{p[2]:.3f}) pitch={pitch:.0f}")


def reset_scene():
    """팔 홈 복귀 + 상자들을 시작 위치로 되돌림 (재실행/리셋용)."""
    sim.release()
    sim.set_grip(om.GRIP_OPEN_DEG)
    sim.move_arm(HOME, 1.0, hook=sync)
    for name, (jid, adr, p0) in OBJ_JOINTS.items():
        sim.d.qpos[adr:adr + 3] = p0
        sim.d.qpos[adr + 3:adr + 7] = [1, 0, 0, 0]
        dof = m.jnt_dofadr[jid]
        sim.d.qvel[dof:dof + 6] = 0
    mujoco.mj_forward(m, sim.d)
    sim.settle(0.5, hook=sync)
    print("[RESET] 초기 상태 복귀 완료")


def run_task(first=False):
    """픽앤플레이스 전체 시퀀스 1회 수행."""
    print("=" * 60)
    print("ex1. 픽앤플레이스 —", "3개 순차 적재(팔레타이징)" if MULTI else "상자 1개")
    print("=" * 60)
    sim.set_grip(om.GRIP_OPEN_DEG)
    sim.set_arm_ctrl(HOME)
    sim.settle(0.8, hook=sync)
    if first:
        capture("00_start")

    results = []
    q_cur = np.array(HOME, dtype=float)
    for (name, ox, oy, rgba), (tx, ty) in zip(OBJECTS, TARGETS):
        print(f"\n--- {name}: ({ox:.2f},{oy:.2f}) -> ({tx:.2f},{ty:.2f}) ---")

        # 1) 물체 위 경유점 -> 내려가기
        q_cur = solve(ox, oy, Z_APPROACH, q_cur); sim.move_arm(q_cur, 1.2, hook=sync); log_pose("접근")
        sim.set_grip(om.GRIP_OPEN_DEG)
        q_cur = solve(ox, oy, Z_GRASP, q_cur); sim.move_arm(q_cur, 1.0, hook=sync); log_pose("하강")

        # 2) 집기: 파지 판정(부착) 후 그리퍼를 파지폭까지 닫기
        #    ※ 완전 닫힘으로 조이면 강체 상자가 밀려나므로, 부착 -> 파지폭(28mm) 순서로 진행
        tcp, _ = sim.tcp_pose()
        obj_p = sim.body_pos(name)
        gap = np.linalg.norm(tcp - obj_p)
        if gap < 0.04:
            sim.attach(name, name + "_free")
            sim.set_grip(om.GRIP_HOLD_DEG)
            sim.settle(0.5, hook=sync)
            print(f"[GRIP] {name} 파지 성공 (TCP-물체 거리 {gap*1000:.0f}mm)")
        else:
            print(f"[GRIP] {name} 파지 실패 (거리 {gap*1000:.0f}mm > 40mm)")
        if first and name == OBJECTS[0][0]:
            capture("01_grasp")

        # 3) 들어올려 목표 위 경유점으로
        q_cur = solve(ox, oy, Z_APPROACH, q_cur); sim.move_arm(q_cur, 1.0, hook=sync)
        q_cur = solve(tx, ty, Z_APPROACH, q_cur); sim.move_arm(q_cur, 1.4, hook=sync); log_pose("이동")

        # 4) 내려놓기 — 손가락을 먼저 벌린 뒤(부착 유지) 부착 해제해야 상자를 건드리지 않음
        q_cur = solve(tx, ty, Z_PLACE, q_cur); sim.move_arm(q_cur, 1.0, hook=sync)
        sim.set_grip(om.GRIP_OPEN_DEG)
        sim.settle(0.3, hook=sync)   # 손가락 벌리기
        sim.release()
        sim.settle(0.6, hook=sync)   # 낙하·안착
        q_cur = solve(tx, ty, Z_APPROACH, q_cur); sim.move_arm(q_cur, 0.8, hook=sync)

        # 5) 판정
        fp = sim.body_pos(name)
        err = float(np.hypot(fp[0] - tx, fp[1] - ty))
        ok = err <= TOL_OK
        results.append((name, fp, (tx, ty), err, ok))
        print(f"[RESULT] {name} final=({fp[0]:.3f}, {fp[1]:.3f}, {fp[2]:.3f}) "
              f"target=({tx:.3f}, {ty:.3f}, 0.025) err={err:.3f}m -> {'OK' if ok else 'FAIL'}")

    sim.move_arm(HOME, 1.2, hook=sync)
    sim.settle(0.5, hook=sync)
    if first:
        capture("02_done")

    n_ok = sum(1 for r in results if r[4])
    mean_err = float(np.mean([r[3] for r in results]))
    print(f"\n[SUMMARY] success {n_ok}/{len(results)}, mean err={mean_err:.3f} m")
    return results


# ---------------- 실행 ----------------
run_task(first=True)

if viewer is not None:
    print("\n키: R = 처음부터 다시 실행 / C = 초기 상태로 리셋 (창 닫기 = 종료)")
    import time
    while viewer.is_running():
        if FLAG["run"]:
            FLAG["run"] = False
            reset_scene()
            run_task()
            print("\n키: R = 다시 실행 / C = 리셋 (창 닫기 = 종료)")
        elif FLAG["reset"]:
            FLAG["reset"] = False
            reset_scene()
        time.sleep(0.1)
