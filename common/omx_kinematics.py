# -*- coding: utf-8 -*-
"""
OMX Follower (OpenManipulator-X 계열) 기구학 모듈
==================================================
- 강의 "기초부터 시작하는 로봇 공학"의 변형 DH 표기 그대로 FK를 계산한다.
  (MakeUrdf 예제 'OMX Follower (Joints Only)'의 기구학 체인과 동일)
- DH 한 줄 [a, d, theta, alpha] 의 변환: T = Rz(theta)·Tz(d)·Tx(a)·Rx(alpha)  (표준 DH, 도 단위)
- 관절: T11(베이스 요), T12(어깨), T13(팔꿈치), T14(손목 피치) — 4자유도
  (T15=툴 롤, T16=그리퍼는 FK/IK 체인에서 제외 — 강의 '!' 마커와 동일한 구분)
- IK: 감쇠 최소 제곱(DLS) 수치 해법. 목표 = TCP 위치(x,y,z) + 접근 피치(도)

단위: 길이 m, 각도 도(deg) — 시뮬레이터에 넘길 때만 라디안으로 변환한다.
"""
import numpy as np

# 변형 DH 체인 (mm, deg) — MakeUrdf 예제14의 기구학 라인( @STL 제외, '!' 이전 TCP까지 )
# 각 원소: (a, d, theta, alpha, joint)  joint=None 이면 고정 변환, 'T11'~'T14'면 해당 관절각이 theta에 더해짐
_DH = [
    (0,     0, 90,  90, None),
    (0,     0,  0, -90, None),
    (0,    13,  0,   0, None),
    (0,    40,  0,   0, None),
    (0,     0,  0,   0, "T11"),
    (0,     0, 90,   0, None),
    (0,     0,  0,  90, None),
    (0,     0, 90,   0, None),
    (44.5,  0,  0,   0, None),
    (0,     0,  0,   0, "T12"),
    (113.15, 0, 0,   0, None),
    (0,     0, 90,   0, None),
    (41.5,  0,  0,   0, None),
    (0,     0,  0,   0, "T13"),
    (162,   0,  0,   0, None),
    (0,     0,  0,   0, "T14"),
    (43.2,  0,  0,   0, None),
    (0,     0, 90,  90, None),
    (0,    60,  0,   0, None),   # TCP (강의 DH의 [0,60,0,0],[.. ],[2,0] 지점)
]

JOINT_NAMES = ["T11", "T12", "T13", "T14"]


def _dh_T(a, d, theta_deg, alpha_deg):
    """표준 DH 변환행렬 (mm, deg) — 강의 Part3 '변환행렬' 그대로."""
    th = np.radians(theta_deg)
    al = np.radians(alpha_deg)
    ct, st = np.cos(th), np.sin(th)
    ca, sa = np.cos(al), np.sin(al)
    return np.array([
        [ct, -st * ca,  st * sa, a * ct],
        [st,  ct * ca, -ct * sa, a * st],
        [0.0,      sa,       ca,      d],
        [0.0,     0.0,      0.0,    1.0],
    ])


def fk(q_deg):
    """관절각 [T11,T12,T13,T14] (deg) -> TCP 동차변환행렬 (위치는 m 단위)."""
    q = dict(zip(JOINT_NAMES, q_deg))
    T = np.eye(4)
    for a, d, theta, alpha, joint in _DH:
        th = theta + (q[joint] if joint else 0.0)
        T = T @ _dh_T(a, d, th, alpha)
    T[:3, 3] *= 0.001  # mm -> m
    return T


def fk_pos_pitch(q_deg):
    """TCP 위치(m)와 접근 피치(deg)를 반환.
    접근 벡터 = TCP 프레임이 뻗는 방향(정지자세에서 전방 +X).
    피치 = 접근 벡터의 고도각 (수평 0도, 수직 아래 -90도)."""
    T = fk(q_deg)
    approach = T[:3, 2]  # TCP 지역 z축 = 마지막 d=60 이동 방향(접근 방향)
    pitch = np.degrees(np.arctan2(approach[2], np.hypot(approach[0], approach[1])))
    return T[:3, 3], pitch


def ik(target_xyz, target_pitch_deg, q0_deg=None, iters=200, tol=1e-4):
    """수치 IK (감쇠 최소 제곱, 유한차분 자코비안).
    target_xyz: TCP 목표 위치 (m) / target_pitch_deg: 접근 피치 (deg, 아래 방향 = -90)
    반환: (관절각 4개 deg, 수렴 여부, 최종 위치 오차 m)"""
    q = np.array(q0_deg if q0_deg is not None else [0.0, -30.0, 40.0, 40.0], dtype=float)
    target = np.array([*target_xyz, np.radians(target_pitch_deg) * 0.1], dtype=float)  # 피치 가중 0.1

    def err_vec(qv):
        p, pitch = fk_pos_pitch(qv)
        return target - np.array([*p, np.radians(pitch) * 0.1])

    lam = 1e-3
    for _ in range(iters):
        e = err_vec(q)
        if np.linalg.norm(e[:3]) < tol and abs(e[3]) < np.radians(1.0) * 0.1:
            return q, True, float(np.linalg.norm(e[:3]))
        # 유한차분 자코비안 (4x4)
        J = np.zeros((4, 4))
        h = 0.01
        for j in range(4):
            dq = q.copy()
            dq[j] += h
            J[:, j] = (-(err_vec(dq) - e)) / np.radians(h)  # d(오차)/d(관절 rad)
        # DLS: dq = J^T (J J^T + λI)^-1 e
        JJT = J @ J.T + lam * np.eye(4)
        dq_rad = J.T @ np.linalg.solve(JJT, e)
        step = np.degrees(dq_rad)
        step = np.clip(step, -8.0, 8.0)  # 한 번에 최대 8도
        q = np.clip(q + step, -180.0, 180.0)
    e = err_vec(q)
    return q, False, float(np.linalg.norm(e[:3]))


def interpolate(q_from, q_to, steps):
    """관절 공간 보간 (smoothstep) — 부드러운 가감속 궤적 (강의 '부드러운 움직임' 원칙)."""
    q_from = np.asarray(q_from, dtype=float)
    q_to = np.asarray(q_to, dtype=float)
    out = []
    for i in range(1, steps + 1):
        s = i / steps
        s = s * s * (3 - 2 * s)  # smoothstep
        out.append(q_from + (q_to - q_from) * s)
    return out


if __name__ == "__main__":
    # 자가 검증: 정지자세 TCP = (306.7, 0, 210.65) mm (ojwSimul에서 검증된 값)
    p, pitch = fk_pos_pitch([0, 0, 0, 0])
    print(f"rest TCP = ({p[0]*1000:.1f}, {p[1]*1000:.1f}, {p[2]*1000:.1f}) mm, pitch={pitch:.1f} deg")
    q, ok, err = ik([0.25, 0.05, 0.05], -90.0)
    p2, pitch2 = fk_pos_pitch(q)
    print(f"IK test: ok={ok} err={err*1000:.2f}mm q={np.round(q,1)}")
    print(f"  -> TCP ({p2[0]*1000:.1f}, {p2[1]*1000:.1f}, {p2[2]*1000:.1f}) mm, pitch={pitch2:.1f}")
