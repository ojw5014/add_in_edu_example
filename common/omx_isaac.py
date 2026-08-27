# -*- coding: utf-8 -*-
"""
OMX Follower — Isaac Sim 공용 헬퍼 (Isaac Sim 4.5+ / 5.x)
==========================================================
※ 이 파일은 Isaac Sim 동봉 파이썬(python.bat)으로 실행해야 한다.
※ 작성 환경에는 Isaac Sim이 없어 실기 검증 전 상태 — API가 다르면 이 파일만 고치면 된다.

사용 순서 (예제 참조):
    import omx_isaac
    app = omx_isaac.boot(headless=False)   # 반드시 다른 omni import 이전!
    sim = omx_isaac.OmxIsaacSim()
"""
import os
import shutil
import sys
import tempfile

import numpy as np

_COMMON = os.path.dirname(os.path.abspath(__file__))
MODEL_DIR = os.path.normpath(os.path.join(_COMMON, "..", "model"))
ARM_JOINTS = ["T11", "T12", "T13", "T14"]
GRIP_JOINTS = ["T16", "t16_0", "t16_1", "t16_2"]  # isaac URDF는 mimic 제거 -> 함께 구동
GRIP_OPEN_DEG = 30.0
GRIP_HOLD_DEG = 8.0
GRIP_CLOSE_DEG = -25.0


def boot(headless=False):
    """SimulationApp 생성 — 모든 omni/isaacsim 모듈 import보다 먼저 호출할 것."""
    from isaacsim import SimulationApp  # Isaac Sim 4.1+ (구버전: omni.isaac.kit)
    return SimulationApp({"headless": headless})


def _ascii_safe_urdf():
    """URDF 임포터가 비ASCII 경로에서 실패할 수 있어, 한글 경로면 임시 폴더로 복사."""
    urdf = os.path.join(MODEL_DIR, "robot_stl_isaac.urdf")
    try:
        urdf.encode("ascii")
        return urdf
    except UnicodeEncodeError:
        dst = os.path.join(tempfile.gettempdir(), "omx_model_ascii")
        os.makedirs(dst, exist_ok=True)
        shutil.copy2(urdf, dst)
        stl_dst = os.path.join(dst, "stl")
        if os.path.isdir(stl_dst):
            shutil.rmtree(stl_dst)
        shutil.copytree(os.path.join(MODEL_DIR, "stl"), stl_dst)
        return os.path.join(dst, "robot_stl_isaac.urdf")


class OmxIsaacSim:
    def __init__(self):
        # --- Isaac 코어 API (5.x 우선, 4.x 폴백) ---
        try:
            from isaacsim.core.api import World
            from isaacsim.core.api.objects import DynamicCuboid, FixedCuboid, VisualCuboid
            from isaacsim.core.prims import SingleArticulation, SingleXFormPrim
        except ImportError:
            from omni.isaac.core import World
            from omni.isaac.core.objects import DynamicCuboid, FixedCuboid, VisualCuboid
            from omni.isaac.core.articulations import Articulation as SingleArticulation
            from omni.isaac.core.prims import XFormPrim as SingleXFormPrim
        import omni.kit.commands
        try:
            from isaacsim.core.utils.types import ArticulationAction
        except ImportError:
            from omni.isaac.core.utils.types import ArticulationAction

        self._ArticulationAction = ArticulationAction
        self._DynamicCuboid = DynamicCuboid
        self._FixedCuboid = FixedCuboid
        self._VisualCuboid = VisualCuboid
        self._XFormPrim = SingleXFormPrim

        # --- 월드 ---
        self.world = World(stage_units_in_meters=1.0)
        self.world.scene.add_default_ground_plane()

        # --- URDF 임포트 ---
        urdf_path = _ascii_safe_urdf()
        ok, cfg = omni.kit.commands.execute("URDFCreateImportConfig")
        cfg.merge_fixed_joints = False
        cfg.fix_base = True
        cfg.make_default_prim = False
        cfg.distance_scale = 1.0
        cfg.default_drive_strength = 10000.0
        cfg.default_position_drive_damping = 500.0
        ok, robot_path = omni.kit.commands.execute(
            "URDFParseAndImportFile", urdf_path=urdf_path, import_config=cfg,
            get_articulation_root=True)
        print("[ISAAC] robot prim:", robot_path)
        self.robot_path = robot_path

        self.robot = self.world.scene.add(SingleArticulation(prim_path=robot_path, name="omx"))
        self.world.reset()
        try:
            self.robot.initialize()
        except Exception:
            pass

        self.dof = {n: self.robot.get_dof_index(n) for n in ARM_JOINTS}
        self.grip_dof = []
        for n in GRIP_JOINTS:
            try:
                self.grip_dof.append(self.robot.get_dof_index(n))
            except Exception:
                pass

        # 손목(link_5) 프림 — TCP = link_5 프레임 + (0.06, 0, 0)
        base_prim = robot_path.rsplit("/", 1)[0] if robot_path.endswith("base_link") else robot_path
        cand = [robot_path + "/link_5", base_prim + "/link_5"]
        self.wrist = None
        for c in cand:
            try:
                self.wrist = self._XFormPrim(c)
                self.wrist.get_world_pose()
                break
            except Exception:
                self.wrist = None
        if self.wrist is None:
            raise RuntimeError("link_5 프림을 찾지 못했습니다. 스테이지 트리에서 경로 확인 후 cand 수정")
        self.tcp_local = np.array([0.06, 0.0, 0.0])
        self._attached = None   # (cuboid, rel_p(3), rel_R(3x3))

    # ---------- 수학 ----------
    @staticmethod
    def _quat_to_R(q):
        w, x, y, z = q
        return np.array([
            [1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)],
            [2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)],
            [2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)],
        ])

    @staticmethod
    def _R_to_quat(R):
        w = np.sqrt(max(0.0, 1 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
        if w < 1e-6:
            return np.array([1.0, 0, 0, 0])
        x = (R[2, 1] - R[1, 2]) / (4 * w)
        y = (R[0, 2] - R[2, 0]) / (4 * w)
        z = (R[1, 0] - R[0, 1]) / (4 * w)
        return np.array([w, x, y, z])

    # ---------- 상태 ----------
    def wrist_pose(self):
        p, q = self.wrist.get_world_pose()
        return np.asarray(p, dtype=float), self._quat_to_R(np.asarray(q, dtype=float))

    def tcp_pos(self):
        p, R = self.wrist_pose()
        return p + R @ self.tcp_local

    # ---------- 제어 ----------
    def _apply(self, q_deg, grip_deg):
        idx = [self.dof[n] for n in ARM_JOINTS] + self.grip_dof
        pos = list(np.radians(q_deg)) + [np.radians(grip_deg)] * len(self.grip_dof)
        act = self._ArticulationAction(joint_positions=np.array(pos), joint_indices=np.array(idx))
        self.robot.get_articulation_controller().apply_action(act)

    def step(self, n=1, render=True):
        for _ in range(n):
            if self._attached is not None:
                cub, rel_p, rel_R = self._attached
                p, R = self.wrist_pose()
                cub.set_world_pose(p + R @ rel_p, self._R_to_quat(R @ rel_R))
            self.world.step(render=render)

    def move_arm(self, q_from, q_to, grip_deg, seconds=1.2, hook=None):
        steps = max(1, int(seconds * 60))
        q_from = np.asarray(q_from, dtype=float)
        q_to = np.asarray(q_to, dtype=float)
        for i in range(1, steps + 1):
            s = i / steps
            s = s * s * (3 - 2 * s)
            self._apply(q_from + (q_to - q_from) * s, grip_deg)
            self.step(1)
            if hook and i % 4 == 0:
                hook()
        return q_to

    def settle(self, grip_deg, seconds=0.5, hook=None):
        for i in range(int(seconds * 60)):
            self.step(1)
            if hook and i % 4 == 0:
                hook()

    # ---------- 물체 ----------
    def add_cube(self, name, pos, half, color, mass=0.03):
        cub = self.world.scene.add(self._DynamicCuboid(
            prim_path=f"/World/{name}", name=name, position=np.array(pos),
            scale=np.array([half * 2] * 3), color=np.array(color), mass=mass))
        return cub

    def add_static_box(self, name, pos, size_xyz, color):
        return self.world.scene.add(self._FixedCuboid(
            prim_path=f"/World/{name}", name=name, position=np.array(pos),
            scale=np.array(size_xyz), color=np.array(color)))

    def add_marker(self, name, pos, size, color):
        return self.world.scene.add(self._VisualCuboid(
            prim_path=f"/World/{name}", name=name, position=np.array(pos),
            scale=np.array([size, size, 0.001]), color=np.array(color)))

    def attach(self, cuboid):
        p, R = self.wrist_pose()
        cp, cq = cuboid.get_world_pose()
        rel_p = R.T @ (np.asarray(cp, dtype=float) - p)
        rel_R = R.T @ self._quat_to_R(np.asarray(cq, dtype=float))
        try:
            cuboid.disable_rigid_body_physics()
        except Exception:
            pass
        self._attached = (cuboid, rel_p, rel_R)

    def release(self):
        if self._attached is not None:
            cub = self._attached[0]
            try:
                cub.enable_rigid_body_physics()
            except Exception:
                pass
        self._attached = None
