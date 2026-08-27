# -*- coding: utf-8 -*-
"""
OMX Follower — MuJoCo 공용 헬퍼
================================
- MakeUrdf가 내보낸 robot_stl.xml(MJCF)을 메모리로 로딩 (한글 경로 안전)
- 장면 요소(물체·팔레트 등) XML 주입
- 관절 位置 액추에이터 기반 팔 제어 + 그리퍼 + 물체 부착(그랩) 유틸
- 오프스크린 PNG 저장 / 궤적(자취) 시각화
"""
import os
import re
import struct
import time
import zlib
import numpy as np
import mujoco

MODEL_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "model"))

ARM_ACTS = ["pos_T11", "pos_T12", "pos_T13", "pos_T14"]
GRIP_OPEN_DEG = 30.0    # T16 +30도 = 열림 (핑거 간격 49mm)
GRIP_HOLD_DEG = 8.0     # 3cm 상자 파지폭 (간격 약 28mm — 상자를 살짝 무는 정도)
GRIP_CLOSE_DEG = -25.0  # T16 -25도 = 완전 닫힘


def load_model(scene_xml: str = ""):
    """robot_stl.xml + 추가 장면 XML을 합쳐 MjModel 생성 (STL은 메모리 assets로)."""
    xml_path = os.path.join(MODEL_DIR, "robot_stl.xml")
    xml = open(xml_path, encoding="utf-8").read()
    if scene_xml:
        xml = xml.replace("</worldbody>", scene_xml + "\n    </worldbody>")
    # 오프스크린 렌더 버퍼 확장 (헤드리스 캡처용)
    if "<visual>" in xml:
        xml = xml.replace("<visual>", '<visual>\n        <global offwidth="1280" offheight="960"/>', 1)
    else:
        xml = xml.replace("</asset>", '</asset>\n    <visual><global offwidth="1280" offheight="960"/></visual>', 1)
    assets = {}
    for ref in set(re.findall(r'file="([^"]+\.stl)"', xml)):
        p = os.path.join(MODEL_DIR, ref.replace("/", os.sep))
        if not os.path.exists(p):
            p = os.path.join(MODEL_DIR, "stl", os.path.basename(ref))
        assets[ref] = open(p, "rb").read()
    return mujoco.MjModel.from_xml_string(xml, assets)


def save_png(path, rgb):
    """numpy (H,W,3) uint8 -> PNG (외부 라이브러리 없이 저장)."""
    h, w = rgb.shape[:2]
    raw = b"".join(b"\x00" + rgb[i].tobytes() for i in range(h))
    def chunk(t, data):
        c = t + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c))
    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


def default_camera():
    cam = mujoco.MjvCamera()
    cam.lookat[:] = [0.18, 0.0, 0.12]
    cam.distance = 0.85
    cam.azimuth = 135
    cam.elevation = -28
    return cam


def add_markers(scene, points, rgba=(0.1, 0.4, 1.0, 1.0), size=0.0025):
    """mjvScene에 작은 구를 얹어 자취/마커 표시 (뷰어 user_scn과 오프스크린 공용)."""
    mat = np.eye(3).flatten()
    for p in points:
        if scene.ngeom >= scene.maxgeom:
            break
        g = scene.geoms[scene.ngeom]
        mujoco.mjv_initGeom(g, mujoco.mjtGeom.mjGEOM_SPHERE,
                            np.array([size, size, size]),
                            np.asarray(p, dtype=np.float64),
                            mat, np.asarray(rgba, dtype=np.float32))
        scene.ngeom += 1


class OmxSim:
    """OMX Follower 시뮬레이션 래퍼 — 위치 액추에이터 물리 제어."""

    def __init__(self, model):
        self.m = model
        self.d = mujoco.MjData(model)
        self.act = {n: mujoco.mj_name2id(model, mujoco.mjtObj.mjOBJ_ACTUATOR, n)
                    for n in ARM_ACTS + ["pos_T15", "pos_T16"]}
        self.bid_wrist = mujoco.mj_name2id(model, mujoco.mjtObj.mjOBJ_BODY, "link_5")
        self.tcp_local = np.array([0.06, 0.0, 0.0])  # link_5 프레임 기준 TCP (교차 검증값)
        self._attached = None   # (joint qpos 주소, 상대 위치, 상대 회전행렬)
        self._grip_deg = GRIP_OPEN_DEG
        # 실시간 페이싱: 0 = 무제한(헤드리스), 1.0 = 실시간, 0.5 = 절반 속도
        self.realtime_speed = 0.0
        self._t_wall = None
        self._t_sim = 0.0
        mujoco.mj_forward(self.m, self.d)

    # ---------- 상태 ----------
    def tcp_pose(self):
        R = self.d.xmat[self.bid_wrist].reshape(3, 3)
        p = self.d.xpos[self.bid_wrist] + R @ self.tcp_local
        return p.copy(), R.copy()

    def body_pos(self, name):
        bid = mujoco.mj_name2id(self.m, mujoco.mjtObj.mjOBJ_BODY, name)
        return self.d.xpos[bid].copy()

    # ---------- 제어 ----------
    def set_arm_ctrl(self, q_deg):
        for n, v in zip(ARM_ACTS, q_deg):
            self.d.ctrl[self.act[n]] = np.radians(v)

    def set_grip(self, deg):
        self._grip_deg = deg
        self.d.ctrl[self.act["pos_T16"]] = np.radians(deg)

    def _pace(self):
        """시뮬레이션 시간을 벽시계에 동기화 (뷰어 모드 실시간 재생용)."""
        if self.realtime_speed <= 0:
            return
        now = time.perf_counter()
        if self._t_wall is None:
            self._t_wall = now
            self._t_sim = 0.0
        self._t_sim += self.m.opt.timestep / self.realtime_speed
        ahead = self._t_sim - (now - self._t_wall)
        if ahead > 0:
            time.sleep(ahead)
        elif ahead < -0.5:   # 렌더 지연 등으로 크게 밀리면 기준 재설정
            self._t_wall = now
            self._t_sim = 0.0

    def step(self, n=1):
        for _ in range(n):
            self._pace()
            if self._attached is not None:
                adr, rel_p, rel_R = self._attached
                R = self.d.xmat[self.bid_wrist].reshape(3, 3)
                p = self.d.xpos[self.bid_wrist]
                obj_p = p + R @ rel_p
                obj_R = R @ rel_R
                quat = np.empty(4)
                mujoco.mju_mat2Quat(quat, obj_R.flatten())
                self.d.qpos[adr:adr + 3] = obj_p
                self.d.qpos[adr + 3:adr + 7] = quat
                dof = self.m.jnt_dofadr[self._attached_jid]
                self.d.qvel[dof:dof + 6] = 0
            mujoco.mj_step(self.m, self.d)

    def move_arm(self, q_deg, seconds=1.2, hook=None):
        """현재 ctrl 목표 -> q_deg 로 smoothstep 보간하며 물리 스텝 진행."""
        q0 = np.degrees([self.d.ctrl[self.act[n]] for n in ARM_ACTS])
        q1 = np.asarray(q_deg, dtype=float)
        n_steps = max(1, int(seconds / self.m.opt.timestep))
        for i in range(1, n_steps + 1):
            s = i / n_steps
            s = s * s * (3 - 2 * s)
            self.set_arm_ctrl(q0 + (q1 - q0) * s)
            self.step(1)
            if hook and i % 8 == 0:
                hook()

    def settle(self, seconds=0.4, hook=None):
        n_steps = int(seconds / self.m.opt.timestep)
        for i in range(n_steps):
            self.step(1)
            if hook and i % 8 == 0:
                hook()

    # ---------- 그랩(부착) ----------
    def attach(self, body_name, joint_name):
        """그리퍼가 물체를 잡았다고 보고 TCP(link_5) 프레임에 물체를 고정한다.
        (초급 예제용 확정적 파지 — 실물의 흡착/전류 파지에 대응하는 단순화)"""
        bid = mujoco.mj_name2id(self.m, mujoco.mjtObj.mjOBJ_BODY, body_name)
        jid = mujoco.mj_name2id(self.m, mujoco.mjtObj.mjOBJ_JOINT, joint_name)
        R5 = self.d.xmat[self.bid_wrist].reshape(3, 3)
        p5 = self.d.xpos[self.bid_wrist]
        rel_p = R5.T @ (self.d.xpos[bid] - p5)
        rel_R = R5.T @ self.d.xmat[bid].reshape(3, 3)
        self._attached = (self.m.jnt_qposadr[jid], rel_p, rel_R)
        self._attached_jid = jid
        # 부착 중 로봇-물체 접촉force 방지 (해제 시 복원)
        self._saved_contype = []
        for g in range(self.m.ngeom):
            if self.m.geom_bodyid[g] == bid:
                self._saved_contype.append((g, self.m.geom_contype[g], self.m.geom_conaffinity[g]))
                self.m.geom_contype[g] = 0
                self.m.geom_conaffinity[g] = 0

    def release(self):
        self._attached = None
        for g, ct, ca in getattr(self, "_saved_contype", []):
            self.m.geom_contype[g] = ct
            self.m.geom_conaffinity[g] = ca
        self._saved_contype = []
