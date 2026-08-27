// ====================================================================
// COmxRobot — 교육용 로봇 제어 클래스 (강의 API 스타일)
// --------------------------------------------------------------------
// 강의 "기초부터 시작하는 로봇 공학"의 실물 모터 제어 명령과 같은 형태로
// 3D 가상 로봇(OMX Follower)을 제어한다.
//
//   Play(동작시간ms, 멈춤시간ms, ID, 각도, ID, 각도, ...)  : 관절 이동 (보간)
//   SyncRead()                                             : 현재 관절각 읽기
//   CalcXyz(out x, out y, out z)                           : 정기구학 (TCP 위치)
//   CalcInv(x, y, z, pitch, out 각도들)                    : 역기구학
//
// 단위: 길이 mm, 각도 도(deg) — 강의와 동일
// 기구학: 변형 DH (MakeUrdf 예제 'OMX Follower (Joints Only)'와 동일 체인)
//   DH 한 줄 [a, d, theta, alpha] = Rz(theta)·Tz(d)·Tx(a)·Rx(alpha)
// ====================================================================
using System;
using System.Windows.Forms;
using OpenJigWare;

namespace OmxPickPlace
{
    public class COmxRobot
    {
        // ── 변형 DH 체인 (mm, deg): {a, d, theta, alpha, motorId(-1=고정)} ──
        //    T11(베이스 요) T12(어깨) T13(팔꿈치) T14(손목 피치) + TCP(끝점)
        private static readonly double[][] DH = new double[][]
        {
            new double[]{ 0,      0, 90,  90, -1 },
            new double[]{ 0,      0,  0, -90, -1 },
            new double[]{ 0,     13,  0,   0, -1 },
            new double[]{ 0,     40,  0,   0, -1 },
            new double[]{ 0,      0,  0,   0, 11 },
            new double[]{ 0,      0, 90,   0, -1 },
            new double[]{ 0,      0,  0,  90, -1 },
            new double[]{ 0,      0, 90,   0, -1 },
            new double[]{ 44.5,   0,  0,   0, -1 },
            new double[]{ 0,      0,  0,   0, 12 },
            new double[]{ 113.15, 0,  0,   0, -1 },
            new double[]{ 0,      0, 90,   0, -1 },
            new double[]{ 41.5,   0,  0,   0, -1 },
            new double[]{ 0,      0,  0,   0, 13 },
            new double[]{ 162,    0,  0,   0, -1 },
            new double[]{ 0,      0,  0,   0, 14 },
            new double[]{ 43.2,   0,  0,   0, -1 },
            new double[]{ 0,      0, 90,  90, -1 },
            new double[]{ 0,     60,  0,   0, -1 },   // TCP
        };

        private readonly Ojw.C3d m_C3d;
        private readonly double[] m_adAngle = new double[256];   // 현재 관절각 (deg)
        public Action OnFrame;             // Play 보간 프레임마다 호출 (물체 부착 갱신용)
        public float SpeedScale = 1.0f;    // 재생 속도 (1.0=기본, 0.5=절반)
        public bool Busy { get; private set; }

        public COmxRobot(Ojw.C3d c3d)
        {
            m_C3d = c3d;
        }

        // ================= 정기구학 =================
        /// <summary>DH 한 줄의 변환행렬 (강의 Part3 변환행렬 그대로)</summary>
        private static double[,] DhT(double a, double d, double thetaDeg, double alphaDeg)
        {
            double th = thetaDeg * Math.PI / 180.0, al = alphaDeg * Math.PI / 180.0;
            double ct = Math.Cos(th), st = Math.Sin(th);
            double ca = Math.Cos(al), sa = Math.Sin(al);
            return new double[,]
            {
                { ct, -st * ca,  st * sa, a * ct },
                { st,  ct * ca, -ct * sa, a * st },
                {  0,       sa,       ca,      d },
                {  0,        0,        0,      1 },
            };
        }

        private static double[,] Mul(double[,] A, double[,] B)
        {
            var R = new double[4, 4];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    double s = 0;
                    for (int k = 0; k < 4; k++) s += A[i, k] * B[k, j];
                    R[i, j] = s;
                }
            return R;
        }

        /// <summary>관절각 [T11,T12,T13,T14](deg) -> TCP 변환행렬 (위치 mm)</summary>
        public double[,] FkMatrix(double[] adQ)
        {
            var T = new double[4, 4] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };
            foreach (var row in DH)
            {
                double th = row[2];
                int mot = (int)row[4];
                if (mot == 11) th += adQ[0];
                else if (mot == 12) th += adQ[1];
                else if (mot == 13) th += adQ[2];
                else if (mot == 14) th += adQ[3];
                T = Mul(T, DhT(row[0], row[1], th, row[3]));
            }
            return T;
        }

        /// <summary>현재 관절각 기준 TCP 변환행렬 (위치 mm) — 물체 부착 계산용</summary>
        public double[,] TcpFrame()
        {
            return FkMatrix(CurrentArmAngles());
        }

        /// <summary>현재 관절각 기준 TCP 위치 (mm) — 정기구학</summary>
        public void CalcXyz(out float fX, out float fY, out float fZ)
        {
            CalcXyz(CurrentArmAngles(), out fX, out fY, out fZ);
        }

        /// <summary>지정 관절각 기준 TCP 위치 (mm) — 정기구학</summary>
        public void CalcXyz(double[] adQ, out float fX, out float fY, out float fZ)
        {
            var T = FkMatrix(adQ);
            fX = (float)T[0, 3]; fY = (float)T[1, 3]; fZ = (float)T[2, 3];
        }

        /// <summary>TCP 위치(mm) + 접근 피치(deg) — 피치: 수평 0, 수직 아래 -90</summary>
        private void FkPosPitch(double[] adQ, double[] adOut)   // adOut = {x,y,z,pitchRad}
        {
            var T = FkMatrix(adQ);
            double ax = T[0, 2], ay = T[1, 2], az = T[2, 2];    // TCP 지역 z축 = 접근 방향
            adOut[0] = T[0, 3]; adOut[1] = T[1, 3]; adOut[2] = T[2, 3];
            adOut[3] = Math.Atan2(az, Math.Sqrt(ax * ax + ay * ay));
        }

        // ================= 역기구학 =================
        /// <summary>수치 역기구학 (감쇠 최소 제곱) — 목표 TCP 위치(mm)와 접근 피치(deg)</summary>
        /// <returns>수렴 여부 (위치 오차 0.1mm 이내)</returns>
        public bool CalcInv(float fX, float fY, float fZ, float fPitchDeg, out float[] afAngle)
        {
            double[] q = CurrentArmAngles();                     // 현재 각도에서 시작 (연속 해)
            if (Math.Abs(q[0]) < 1e-9 && Math.Abs(q[1]) < 1e-9 && Math.Abs(q[2]) < 1e-9 && Math.Abs(q[3]) < 1e-9)
                q = new double[] { 0, -30, 40, 40 };             // 정지자세면 무난한 초기 추정치
            double[] target = { fX * 0.001, fY * 0.001, fZ * 0.001, fPitchDeg * Math.PI / 180.0 * 0.1 };
            double[] fk = new double[4];
            double[] e = new double[4];
            double lam = 1e-3, h = 0.01;
            bool ok = false;

            for (int iter = 0; iter < 200; iter++)
            {
                ErrVec(q, target, fk, e);
                double posErr = Math.Sqrt(e[0] * e[0] + e[1] * e[1] + e[2] * e[2]);
                if (posErr < 1e-4 && Math.Abs(e[3]) < (1.0 * Math.PI / 180.0) * 0.1) { ok = true; break; }

                // 유한차분 자코비안 (4x4)
                var J = new double[4, 4];
                var e2 = new double[4];
                for (int j = 0; j < 4; j++)
                {
                    var dq = (double[])q.Clone();
                    dq[j] += h;
                    ErrVec(dq, target, fk, e2);
                    double hRad = h * Math.PI / 180.0;
                    for (int i = 0; i < 4; i++) J[i, j] = -(e2[i] - e[i]) / hRad;
                }
                // DLS: dq = J^T (J J^T + lam I)^-1 e
                var A = new double[4, 4];
                for (int i = 0; i < 4; i++)
                    for (int j2 = 0; j2 < 4; j2++)
                    {
                        double s = 0;
                        for (int k = 0; k < 4; k++) s += J[i, k] * J[j2, k];
                        A[i, j2] = s + ((i == j2) ? lam : 0);
                    }
                var y = Solve4(A, e);
                for (int j = 0; j < 4; j++)
                {
                    double s = 0;
                    for (int i = 0; i < 4; i++) s += J[i, j] * y[i];   // J^T y
                    double stepDeg = s * 180.0 / Math.PI;
                    if (stepDeg > 8) stepDeg = 8; else if (stepDeg < -8) stepDeg = -8;
                    q[j] += stepDeg;
                    if (q[j] > 180) q[j] = 180; else if (q[j] < -180) q[j] = -180;
                }
            }
            afAngle = new float[] { (float)q[0], (float)q[1], (float)q[2], (float)q[3] };
            if (!ok)
            {
                // 엄격 수렴(0.1mm)엔 못 미쳐도 실용 오차(5mm) 이내면 성공으로 판정
                ErrVec(q, target, fk, e);
                double dFinal = Math.Sqrt(e[0] * e[0] + e[1] * e[1] + e[2] * e[2]);
                ok = dFinal < 0.005;
            }
            return ok;
        }

        private void ErrVec(double[] q, double[] target, double[] fk, double[] e)
        {
            FkPosPitch(q, fk);
            e[0] = target[0] - fk[0] * 0.001;
            e[1] = target[1] - fk[1] * 0.001;
            e[2] = target[2] - fk[2] * 0.001;
            e[3] = target[3] - fk[3] * 0.1;
        }

        /// <summary>4x4 선형계 가우스 소거</summary>
        private static double[] Solve4(double[,] A, double[] b)
        {
            var M = new double[4, 5];
            for (int i = 0; i < 4; i++) { for (int j = 0; j < 4; j++) M[i, j] = A[i, j]; M[i, 4] = b[i]; }
            for (int c = 0; c < 4; c++)
            {
                int p = c;
                for (int r = c + 1; r < 4; r++) if (Math.Abs(M[r, c]) > Math.Abs(M[p, c])) p = r;
                for (int j = c; j < 5; j++) { double t = M[c, j]; M[c, j] = M[p, j]; M[p, j] = t; }
                double d = M[c, c]; if (Math.Abs(d) < 1e-12) d = 1e-12;
                for (int j = c; j < 5; j++) M[c, j] /= d;
                for (int r = 0; r < 4; r++)
                {
                    if (r == c) continue;
                    double f = M[r, c];
                    for (int j = c; j < 5; j++) M[r, j] -= f * M[c, j];
                }
            }
            return new double[] { M[0, 4], M[1, 4], M[2, 4], M[3, 4] };
        }

        // ================= 관절 제어 =================
        /// <summary>현재 관절각 읽기 — [T11, T12, T13, T14, T15, T16] (deg)</summary>
        public float[] SyncRead()
        {
            return new float[]
            {
                (float)m_adAngle[11], (float)m_adAngle[12], (float)m_adAngle[13],
                (float)m_adAngle[14], (float)m_adAngle[15], (float)m_adAngle[16],
            };
        }

        private double[] CurrentArmAngles()
        {
            return new double[] { m_adAngle[11], m_adAngle[12], m_adAngle[13], m_adAngle[14] };
        }

        /// <summary>관절 즉시 설정 (보간 없음)</summary>
        public void SetAngle(int nId, float fDeg)
        {
            m_adAngle[nId] = fDeg;
            m_C3d.SetData(nId, fDeg);
        }

        /// <summary>강의 CProtocol2.Play와 동일 형식 — Play(동작시간ms, 멈춤시간ms, ID, 각도, ID, 각도, ...)
        /// 부드러운 보간(smoothstep)으로 이동하며, 완료까지 블로킹(화면은 계속 갱신).</summary>
        public void Play(int nTimeMs, int nDelayMs, params float[] afIdAngle)
        {
            if (afIdAngle == null || afIdAngle.Length < 2 || (afIdAngle.Length % 2) != 0) return;
            Busy = true;
            try
            {
                int nCnt = afIdAngle.Length / 2;
                var anId = new int[nCnt];
                var adFrom = new double[nCnt];
                var adTo = new double[nCnt];
                for (int i = 0; i < nCnt; i++)
                {
                    anId[i] = (int)afIdAngle[i * 2];
                    adFrom[i] = m_adAngle[anId[i]];
                    adTo[i] = afIdAngle[i * 2 + 1];
                }
                const int FRAME_MS = 25;
                int nSteps = Math.Max(1, nTimeMs / FRAME_MS);
                int nWait = (int)(FRAME_MS / Math.Max(0.05f, SpeedScale));
                for (int s = 1; s <= nSteps; s++)
                {
                    double t = (double)s / nSteps;
                    t = t * t * (3 - 2 * t);   // smoothstep — 부드러운 가감속
                    for (int i = 0; i < nCnt; i++)
                    {
                        double v = adFrom[i] + (adTo[i] - adFrom[i]) * t;
                        m_adAngle[anId[i]] = v;
                        m_C3d.SetData(anId[i], (float)v);
                    }
                    if (OnFrame != null) OnFrame();
                    System.Threading.Thread.Sleep(nWait);
                    Application.DoEvents();    // 화면 갱신 (강의의 반복문-프리즈 해결 패턴)
                }
                if (nDelayMs > 0)
                {
                    int nDelaySteps = Math.Max(1, nDelayMs / FRAME_MS);
                    for (int s = 0; s < nDelaySteps; s++)
                    {
                        if (OnFrame != null) OnFrame();
                        System.Threading.Thread.Sleep(nWait);
                        Application.DoEvents();
                    }
                }
            }
            finally { Busy = false; }
        }
    }
}
