// ====================================================================
// ex0c. 파이썬 코드 실행기 — OpenJigWare 3D (윈도우 프로그램)
// --------------------------------------------------------------------
// 개념: 예제 버튼 -> 파이썬 코드가 편집창에 나타남 -> ▶ Run ->
//       Ojw.CPython(강의 'C#에 Python 넣기')이 파이썬을 실행하고,
//       파이썬의 omx 모듈 함수(move/play/grip/grab...)가 출력한 명령을
//       이 프로그램이 받아 순서대로 로봇을 구동한다.
// 환경·동작은 ex0(픽앤플레이스)과 동일: 원통 제품·팔레트·그리기 판·펜 거치대,
// 파지는 제품/펜 굵기만큼만 쥔다.
// GUI(버튼·패널·편집창·타이머)는 MainForm.Designer.cs — 폼 디자이너에서 마우스로 수정.
// ====================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using OpenJigWare;
using Python.Runtime;

namespace OmxPythonRunner
{
    public partial class MainForm : Form
    {
        private const float GRIP_OPEN = 30f;
        private const float CYL_R = 12f;
        private const float CYL_H = 40f;
        private const float PEN_R = 6f;
        private const float PEN_LEN = 70f;
        private const float PEN_TIP = 55f;
        private const float PEN_X = 140f, PEN_Y = 170f;
        private const float PEN_STAND_H = 6f;
        private const float BOARD_X = 210f, BOARD_Y = 0f;
        private const float RECT_HX = 40f, RECT_HY = 35f;
        private const float BOARD_TOP = 8f;
        private static readonly float[] HOME = { 0f, -25f, 35f, 35f };

        private Ojw.C3d m_C3d;
        private readonly float[] m_afAngle = new float[256];   // 현재 관절각 (deg) — 보간용
        private TextBox m_txtDummy = new TextBox();      // MakeDHSkeleton_Urdf 출력 흡수용 (화면에 없음)
        private bool m_bRunActive = false;   // Run 시작 ~ [RUN] 종료 표시 사이

        private System.Threading.Thread m_thPy = null;
        private ulong m_ulPyThreadId = 0;                // 리셋(중지) 시 KeyboardInterrupt 전달용
        private volatile bool m_bStopReq = false;        // 리셋으로 중지 요청됨 — 예외를 [RESET]으로 표시
        private static bool s_bPyEngineReady = false;

        private bool MotionActive { get { return m_C3d != null && m_C3d.MoveJoints_IsMoving(); } }

        // ── 장면 상태 ──
        private class CItem
        {
            public string Name;
            public double X, Y, ZBottom;
            public double[] Start;
            public Color C;
            public bool Attached;
            public double[] Rel;
        }
        private readonly List<CItem> m_lstItems = new List<CItem>();
        private readonly double[][] m_adTargets = { new double[] { 170, -110 }, new double[] { 210, -110 }, new double[] { 250, -110 } };
        private readonly List<double[]> m_lstTrace = new List<double[]>();
        private bool m_bPenHeld = false;

        // ── OMX Follower 변형 DH (ex0과 동일 — MakeUrdf 예제 14) ──
        private static readonly string DH_OMX = string.Join("\r\n", new[]
        {
            "// *OMX Follower (Joints Only)",
            "[0,0,90,90],[-1,0,0]",
            "@[omx-ai_follower_base_plate.stl,-16777216,1.0],[0,-1],[0,0,11],[0,0,0]",
            "[0,0,0,-90],[-1,0,0]",
            "[0,13,0,0],[-1,0,0]",
            "[0,40,0,0],[-1,0,0]",
            "@[xl-430.stl,-16777216,1.0],[1,11],[0,0.25,-19],[0,0,180]",
            "@[omx-ai_follower_frame1.stl,-16777216,1.0],[1,11],[0,0.25,1.2],[0,90,180]",
            "[0,0,0,0],[11,0,0] // - Axis11",
            "@[omx-ai_follower_frame2.stl,-16777216,1.0],[1,12],[0,0,21.5],[0,-90,0]",
            "@[xl-430.stl,-16777216,1.0],[1,12],[0,0,44.5],[0,90,90]",
            "[0,0,90,0],[-1,0,0]",
            "[0,0,0,90],[-1,0,0]",
            "[0,0,90,0],[-1,0,0]",
            "[44.5,0,0,0],[-1,0,0]",
            "[0,0,0,0],[12,0,0] // - Axis12",
            "@[omx-ai_follower_link_l.stl,-1.677722E+07,1.0],[1,12],[0,0,0],[90,0,0]",
            "[113.15,0,0,0],[-1,0,0]",
            "[0,0,90,0],[-1,0,0]",
            "[41.5,0,0,0],[-1,0,0]",
            "@[xl-430.stl,-16777216,1.0],[1,13],[0,0,0],[0,0,-90]",
            "[0,0,0,0],[13,0,0] // - Axis13",
            "@[omx-ai_follower_link_u.stl,-16777216,1.0],[1,13],[0,0,0],[90,0,0]",
            "[162,0,0,0],[-1,0,0]",
            "@[dc15_dummy_std.stl,-16777216,1.0],[1,14],[-7.5,0,1.5],[0,0,-90]",
            "[0,0,0,0],[14,0,0] // - Axis14",
            "@[omx-ai_follower_frame3.stl,-16777216,1.0],[1,14],[0,0,0],[90,0,180]",
            "[43.2,0,0,0],[-1,0,0]",
            "[0,0,90,90],[-1,0,0]",
            "@[dc15_dummy_std.stl,-16777216,1.0],[1,15],[-7.5,0,-12.8],[0,0,-90]",
            "[0,60,0,0],[-1,0,0],[2,0]",
            "!",
            "[0,-60,0,0],[-1,0,0]",
            "[0,0,0,0],[15,0,0] // - Axis15",
            "@[omx-ai_follower_frame4.stl,-16777216,1.0],[1,15],[0,0,1.5],[0,0,90]",
            "[0,15,-90,90],[-1,0,0]",
            "@[dc15_dummy_std.stl,-16777216,1.0],[1,16],[0,0,1.5],[0,0,90]",
            "[-7.5,0,0,0],[-1,0,0]",
            "[0,0,90,0],[16,0,0] // - Axis16",
            "@[omx-ai_follower_gripper_l.stl,-16777216,1.0],[2,0],[0,0,16.2],[0,0,-90]",
            "[60,0,0,0],[-1,0,0]",
            "[-60,0,0,0],[-1,0,0]",
            "[0,0,0,0],[16,1,0] // - Axis16",
            "[7.5,0,-90,0],[-1,0,0]",
            "[10.80,0,0,0],[-1,0,0]",
            "[0,0,90,0],[16,1,0] // - Axis16",
            "@[omx-ai_follower_gripper_r.stl,-16777216,1.0],[1,16],[0,0,16.2],[0,0,0]",
            "[60,0,0,0],[-1,0,0]",
            "[-60,0,0,0],[-1,0,0]",
            "[0,0,0,0],[16,0,0] // - Axis16",
            "[10.8,0,90,90],[-1,0,0]",
        });

        public MainForm()
        {
            InitializeComponent();
        }

        private void btnEx1_Click(object sender, EventArgs e) { LoadExample("ex1_pick_and_place.py"); }
        private void btnEx2_Click(object sender, EventArgs e) { LoadExample("ex2_palletizing.py"); }
        private void btnEx3_Click(object sender, EventArgs e) { LoadExample("ex3_rect.py"); }
        private void btnEx4_Click(object sender, EventArgs e) { LoadExample("ex4_rect_bezier.py"); }
        private void btnEx5_Click(object sender, EventArgs e) { LoadExample("ex5_rect_bezier_rt.py"); }
        private void btnRun_Click(object sender, EventArgs e) { RunPython(); }
        private void btnReset_Click(object sender, EventArgs e) { StopAndReset(); }

        private void SetRunUi(bool bRunning)
        {
            btnRun.Enabled = !bRunning;
            btnRun.Text = bRunning ? "실행 중... (중지 = 리셋 버튼)" : "▶ Run (파이썬 실행)";
        }

        /// <summary>리셋 = 탈출구. 실행 중이면 남은 명령을 끊고(현재 동작까지만),
        /// 멈춘 상태면 장면·자세를 초기화한다.</summary>
        private void StopAndReset()
        {
            if (m_bRunActive && m_ulPyThreadId != 0)
            {
                ulong ulId = m_ulPyThreadId;
                m_bStopReq = true;
                var thInt = new System.Threading.Thread(() =>
                { try { using (Py.GIL()) PythonEngine.Interrupt(ulId); } catch { } });
                thInt.IsBackground = true;
                thInt.Start();
                m_C3d.MoveJoints_Stop();
                Ojw.printf(txtLog, "[RESET] 실행 중지 요청 — 파이썬에 KeyboardInterrupt 전달\r\n");
                return;
            }
            if (MotionActive) { m_C3d.MoveJoints_Stop(); SetRunUi(false); return; }
            SetRunUi(false);
            try { ResetScene(); }
            catch (Exception ex) { Ojw.printf(txtLog, "[ERROR] {0}\r\n", ex.Message); }
        }

        private void LoadExample(string strFile)
        {
            string strPath = Path.Combine(Application.StartupPath, "examples", strFile);
            if (!File.Exists(strPath)) { Ojw.printf(txtLog, "[ERROR] 예제 파일 없음: {0}\r\n", strPath); return; }
            txtPython.Text = File.ReadAllText(strPath);
            Ojw.printf(txtLog, "\r\n예제 로드: {0} — 코드를 살펴보고 ▶ Run 을 누르세요.\r\n", strFile);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 어디서 실행하든(바로가기·다른 폴더) 작업 폴더를 실행 파일 위치로 고정
            //  - 라이브러리가 현재 폴더에 파일을 쓰고, CPython 임시 py도 여기 있어야 함
            Directory.SetCurrentDirectory(Application.StartupPath);

            // 자식 python 프로세스의 표준출력 인코딩을 .NET(cp949)과 일치시킨다.
            // 파이썬 예제 코드에 인코딩 처리(import sys...)를 넣지 않아도 한글이 안 깨진다.
            Environment.SetEnvironmentVariable("PYTHONIOENCODING", "cp949:replace");

            Ojw.printf_Init(txtLog);

            var hDummy = m_txtDummy.Handle;

            m_C3d = new Ojw.C3d();
            m_C3d.Init(pnDisp);
            m_C3d.SetFilePath_Design(Application.StartupPath);
            m_C3d.SetStandardAxis(true);
            m_C3d.SetVirtualClass_Enable(false);
            m_C3d.SetAseFile_Path("stl");
            m_C3d.SetSkeletonView(false);
            m_C3d.SetMeshView(true);
            m_C3d.SetMouseMode(0);
            m_C3d.SetCoordMode(1);
            m_C3d.SetAngle_Display(20.0f, 0.0f, 15.0f);

            string strCmd = "", strXml = "";
            m_C3d.m_CHeader.pstrKinematics[0] = DH_OMX;
            m_C3d.CheckForward();
            m_C3d.m_CHeader.strDrawModel = m_C3d.MakeDHSkeleton_Urdf(
                true, true, false, true, 10.0f, 20.0f, Color.Violet,
                DH_OMX, 0, 0, 0, 0, 0, 0, 0,
                ref m_txtDummy, ref strCmd, ref strXml);
            m_C3d.CompileDesign();
            m_C3d.SetScale(0.34f);

            m_C3d.CalcInv_SetIKAlgorithm(5);   // TRAC-IK (ROS) — 라이브러리 IK 사용

            m_lstItems.Add(NewItem("obj1", 200, 160, Color.IndianRed));
            m_lstItems.Add(NewItem("obj2", 200, 110, Color.MediumSeaGreen));
            m_lstItems.Add(NewItem("obj3", 200, 60, Color.CornflowerBlue));

            // 장면·실물 브리지(scene) = 라이브러리 클래스. 렌더 틱이 모션을 진행하므로 외부 펌프 선언
            m_Scene = new Ojw.CScene_t(m_C3d, REAL_IDS, REAL_SIGN);
            m_C3d.MoveJoints_SetExternalPump(true);

            SetJoint(11, HOME[0]); SetJoint(12, HOME[1]);
            SetJoint(13, HOME[2]); SetJoint(14, HOME[3]);
            SetJoint(15, 0); SetJoint(16, GRIP_OPEN);

            m_tmrDisp.Enabled = true;   // 33ms 렌더
            m_tmrPoll.Enabled = true;   // 50ms 파이썬 출력 폴링 (명령 수집 + 순차 실행)

            LoadExample("ex1_pick_and_place.py");
            Ojw.printf(txtLog, "준비 완료 — Run 을 누르면 파이썬 코드가 이 창의 3D 를 직접 구동합니다.\r\n\r\n");
        }

        private CItem NewItem(string strName, double dX, double dY, Color c)
        {
            return new CItem { Name = strName, X = dX, Y = dY, ZBottom = 0, Start = new double[] { dX, dY }, C = c };
        }

        private bool m_bClosing = false;   // 닫는 중

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_bClosing = true;
            if (m_C3d != null) m_C3d.MoveJoints_Stop();   // 진행 중 모션 중단
            m_tmrDisp.Stop();
            m_tmrPoll.Stop();
            if (m_Scene != null) m_Scene.close();   // 실물 연결이 남아 있으면 해제 (토크 상태는 그대로 둔다)
            if (m_C3d != null) { m_C3d.Stop(); m_C3d.DInit(); m_C3d = null; }
        }

        private static string PickPythonDll(string strDir)
        {
            try
            {
                foreach (string s in Directory.GetFiles(strDir, "python3*.dll"))
                    if (!s.EndsWith("python3.dll")) return s;   // python310.dll 등 본체
            }
            catch { }
            return null;
        }

        private static string FindPythonDll()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "where", Arguments = "python",
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string strOut = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    foreach (string strLine in strOut.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string strDir = Path.GetDirectoryName(strLine.Trim());
                        if (strDir == null || !Directory.Exists(strDir)) continue;
                        string strDll = PickPythonDll(strDir);
                        if (strDll != null) return strDll;
                        string strCfg = Path.Combine(Path.GetDirectoryName(strDir) ?? "", "pyvenv.cfg");
                        if (File.Exists(strCfg))
                            foreach (string strC in File.ReadAllLines(strCfg))
                                if (strC.TrimStart().StartsWith("home"))
                                {
                                    strDll = PickPythonDll(strC.Substring(strC.IndexOf('=') + 1).Trim());
                                    if (strDll != null) return strDll;
                                }
                    }
                }
            }
            catch { }
            return null;
        }

        private bool EnsurePyEngine()
        {
            if (s_bPyEngineReady) return true;
            string strDll = FindPythonDll();
            if (strDll == null)
            {
                Ojw.printf(txtLog, "[ERROR] python(x64)을 찾을 수 없습니다 — 설치/PATH 확인\r\n");
                return false;
            }
            try
            {
                Python.Runtime.Runtime.PythonDLL = strDll;
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
                s_bPyEngineReady = true;
                Ojw.printf(txtLog, "[PY] 파이썬 엔진 초기화: {0}\r\n", strDll);
                return true;
            }
            catch (Exception ex)
            {
                Ojw.printf(txtLog, "[ERROR] 파이썬 엔진 초기화 실패: {0}\r\n", ex.Message);
                return false;
            }
        }

        internal void PrintLogSafe(string strMsg)
        {
            try { Ojw.printf(txtLog, "{0}", strMsg); } catch { }
        }

        private void RunPython()
        {
            if (m_bRunActive) { Ojw.printf(txtLog, "[BUSY] 실행 중입니다... (중지 = 리셋 버튼)\r\n"); return; }
            if (MotionActive) m_C3d.MoveJoints_Stop();
            if (!EnsurePyEngine()) return;
            Directory.SetCurrentDirectory(Application.StartupPath);

            ResetSceneState();              
            // 장면만 초기 상태로 — 자세 복귀는 예제 코드의 home()
            string strCode = txtPython.Text;
            m_bStopReq = false;
            m_bRunActive = true;
            SetRunUi(true);
            Ojw.printf(txtLog, "\r\n[RUN] 파이썬 실행 — 이 창의 3D 를 직접 구동합니다\r\n");

            m_thPy = new System.Threading.Thread(() =>
            {
                try
                {
                    using (Py.GIL())
                    using (var scope = Py.CreateScope())
                    {
                        m_ulPyThreadId = PythonEngine.GetPythonThreadID();
                        using (var sys = Py.Import("sys"))
                        {
                            sys.SetAttr("stdout", new CPyLog(this).ToPython());   // print → 로그 창
                            sys.SetAttr("stderr", new CPyLog(this).ToPython());
                        }
                        // 파이썬에는 scene(= 라이브러리 Ojw.CScene_t)만 건넨다 — 3D 객체는 예제 코드가
                        // 'from OpenJigWare import Ojw' + 'c3d = scene.c3d' 로 명시적으로 가져간다
                        scope.Set("scene", m_Scene.ToPython());
                        scope.Exec(strCode);
                    }
                }
                catch (PythonException ex)
                {
                    PrintLogSafe(m_bStopReq ||
                                 ex.Message.IndexOf("KeyboardInterrupt", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "[RESET] 파이썬 실행이 중지되었습니다\r\n"
                        : "[ERROR] 파이썬 오류:\r\n" + ex.Message + "\r\n");
                }
                catch (Exception ex) { PrintLogSafe("[ERROR] " + ex.Message + "\r\n"); }
                finally
                {
                    m_ulPyThreadId = 0;
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            m_bRunActive = false;
                            SetRunUi(false);
                            Ojw.printf(txtLog, "[RUN] 종료\r\n");
                        }));
                    }
                    catch { }
                }
            });
            m_thPy.IsBackground = true;
            m_thPy.Start();
        }

        private void tmrPoll_Tick(object sender, EventArgs e)
        {
        }

        public class CPyLog
        {
            private readonly MainForm m_Form;
            private string m_strBuf = "";
            public CPyLog(MainForm f) { m_Form = f; }
            public void write(string s)
            {
                m_strBuf += s;
                int nPos;
                while ((nPos = m_strBuf.IndexOf('\n')) >= 0)
                {
                    string strLine = m_strBuf.Substring(0, nPos).TrimEnd('\r');
                    m_strBuf = m_strBuf.Substring(nPos + 1);
                    if (strLine.Trim().Length > 0) m_Form.PrintLogSafe("[py] " + strLine + "\r\n");
                }
            }
            public void flush() { }
        }

        // scene 객체는 라이브러리의 Ojw.CScene_t (COjw_42_Sim2Real.cs) — 앱에는 래퍼가 없다.
        // 잡기/놓기/잉크는 AutoGripScene(그리퍼 각도 자동 판정)이, 통신은 CScene_t 가 전담한다.

        private void SetJoint(int nId, float fDeg)
        {
            if (m_C3d == null) return;   // 닫는 중 — FormClosing 이 3D 를 해제한 뒤에도 호출될 수 있다
            m_afAngle[nId] = fDeg;
            m_C3d.SetData(nId, fDeg);
        }

        private void TcpPos(out float fX, out float fY, out float fZ)
        {
            fX = fY = fZ = 0f;
            if (m_C3d == null) return;   // 닫는 중
            m_C3d.CalcF(0, false, out fX, out fY, out fZ);
        }

        // 실물 로봇 — 라이브러리 Ojw.CScene_t 가 전담 (COjw_42_Sim2Real.cs)
        // 파이썬의 scene 객체 = Ojw.CScene_t 인스턴스 (c3d + open/close/torqon/torqoff/syncread/diag).
        // 통신 절차(포트 확인·프로토콜 2.0·토크 실측·정착 재시도 읽기·모션 중 스트리밍)는 전부 DLL 안.
        private Ojw.CScene_t m_Scene = null;
        private static readonly int[] REAL_IDS = { 11, 12, 13, 14, 15, 16 };
        // 시뮬(DH) 각 -> 실물 모터각 부호. 실물에서 방향이 반대로 도는 관절만 -1 (현장 보정용)
        private static readonly int[] REAL_SIGN = { 1, 1, 1, 1, 1, 1 };

        // (수동 grab/put 제거 — 자동 파지 AutoGripScene 가 전담)

        internal void ResetSceneState()
        {
            if (InvokeRequired) { try { Invoke((Action)ResetSceneState); } catch { } return; }
            foreach (var item in m_lstItems)
            {
                item.Attached = false;
                item.X = item.Start[0]; item.Y = item.Start[1]; item.ZBottom = 0;
            }
            m_lstTrace.Clear();
            m_bPenHeld = false;
        }

        private void ResetScene()
        {
            ResetSceneState();
            m_C3d.MoveJoints_Start(800, new[] { 11, 12, 13, 14, 15, 16 },
                                   new[] { HOME[0], HOME[1], HOME[2], HOME[3], 0f, GRIP_OPEN });
        }

        private bool m_bGripCloseLatch = false;   // 닫힘 파지 시도 1회 래치 (실물처럼 '닫는 순간' 판정)

        /// <summary>자동 파지/놓기 — 그리퍼 각도로 판정한다 (닫힘 &lt; 12도, 열림 &gt; 20도).
        /// scene.grab/put 같은 장면 명령 없이 로봇 명령(그리퍼 여닫기)만으로 잡고 놓는다.
        /// 파지 판정은 물리 조건: 수평 오차 12mm 이내(개방폭 49 − 제품 지름 24 기준) + 파지 높이 구간.
        /// 어긋난 채 닫으면 파지 실패 로그를 남긴다.</summary>
        private void AutoGripScene(float fTx, float fTy, float fTz)
        {
            float fGrip = m_C3d.GetData(16);
            bool bHolding = m_bPenHeld || m_lstItems.Exists(it => it.Attached);

            if (fGrip < 12f)
            {
                if (m_bGripCloseLatch) return;
                m_bGripCloseLatch = true;              // 닫히는 순간 1회만 판정
                if (bHolding) return;

                // 펜: 거치대 근처에서 닫으면 펜을 잡는다
                double dPenLat = Math.Sqrt((fTx - PEN_X) * (fTx - PEN_X) + (fTy - PEN_Y) * (fTy - PEN_Y));
                if (dPenLat < 15 && fTz > 40 && fTz < 95)
                {
                    m_bPenHeld = true;
                    Ojw.printf(txtLog, "[PEN] 펜 파지 (수평 오차 {0:F1}mm)\r\n", dPenLat);
                    return;
                }
                // 제품: 가장 가까운 원통으로 판정 (수평 오차가 파지 성패를 가른다)
                CItem best = null;
                double dBestLat = double.MaxValue, dBestDz = 0;
                foreach (var item in m_lstItems)
                {
                    double cz = item.ZBottom + CYL_H / 2;
                    double dLat = Math.Sqrt((fTx - item.X) * (fTx - item.X) + (fTy - item.Y) * (fTy - item.Y));
                    if (dLat < dBestLat) { dBestLat = dLat; dBestDz = fTz - cz; best = item; }
                }
                if (best == null) return;
                if (dBestLat < 12 && Math.Abs(dBestDz) <= 30)
                {
                    double cz = best.ZBottom + CYL_H / 2;
                    best.Rel = new double[] { best.X - fTx, best.Y - fTy, cz - fTz };
                    best.Attached = true;
                    Ojw.printf(txtLog, "[GRIP] {0} 파지 성공 (수평 오차 {1:F1}mm)\r\n", best.Name, dBestLat);
                }
                else if (dBestLat < 60 && Math.Abs(dBestDz) <= 60)
                {
                    // 근처에 제품이 있는데 어긋난 채 닫았다 — 원인을 알 수 있게 알려준다
                    if (dBestLat >= 12)
                        Ojw.printf(txtLog, "[GRIP] {0} 파지 실패 (수평 오차 {1:F1}mm > 12mm) — 제품 중심 위에서 닫아야 합니다\r\n",
                                   best.Name, dBestLat);
                    else
                        Ojw.printf(txtLog, "[GRIP] {0} 파지 실패 (높이 차 {1:F0}mm > 30mm) — 파지 높이까지 내려간 뒤 닫아야 합니다\r\n",
                                   best.Name, Math.Abs(dBestDz));
                }
            }
            else if (fGrip > 20f)
            {
                m_bGripCloseLatch = false;
                if (!bHolding) return;
                // 여는 중 — 잡고 있던 것을 그 자리에 놓는다
                if (m_bPenHeld)
                {
                    m_bPenHeld = false;
                    Ojw.printf(txtLog, "[PEN] 펜 반납\r\n");
                }
                foreach (var item in m_lstItems)
                {
                    if (!item.Attached) continue;
                    item.Attached = false;
                    bool bOnPallet = item.X > 135 && item.X < 285 && item.Y > -165 && item.Y < -55;
                    item.ZBottom = bOnPallet ? 10 : 0;   // 팔레트 위 안착 / 바닥
                    Ojw.printf(txtLog, "[GRIP] {0} 내려놓기 -> ({1:F1}, {2:F1})\r\n", item.Name, item.X, item.Y);
                }
            }
        }

        private void tmrDisp_Tick(object sender, EventArgs e)
        {
            if (m_C3d == null) return;

            m_C3d.MoveJoints_Step();
            // 실물 스트리밍은 라이브러리가 처리 — MoveJoints_Step 이 모션 싱크(CScene_t.SendPose)를 부른다

            float tx, ty, tz;
            TcpPos(out tx, out ty, out tz);

            // 자동 장면 처리 — 파이썬 코드는 로봇 명령만 쓴다.
            // 그리퍼를 닫으면 근처 제품/펜이 잡히고, 열면 놓인다 (실물과 같은 개념)
            AutoGripScene(tx, ty, tz);

            foreach (var item in m_lstItems)
            {
                if (!item.Attached) continue;
                item.X = tx + item.Rel[0];
                item.Y = ty + item.Rel[1];
                item.ZBottom = tz + item.Rel[2] - CYL_H / 2;
            }
            // 잉크 자취 — 펜을 든 채 펜 끝이 판에 닿아 있으면 자동으로 그려진다 (trace 명령 불필요)
            if (m_bPenHeld &&
                Math.Abs(tx - BOARD_X) <= RECT_HX + 6 && Math.Abs(ty - BOARD_Y) <= RECT_HY + 6 &&
                Math.Abs((tz - PEN_TIP) - BOARD_TOP) < 2.5)
            {
                double dTipZ = BOARD_TOP + 0.4;   // 잉크는 판 표면에 남는다
                if (m_lstTrace.Count == 0 ||
                    Math.Abs(m_lstTrace[m_lstTrace.Count - 1][0] - tx) > 0.5 ||
                    Math.Abs(m_lstTrace[m_lstTrace.Count - 1][1] - ty) > 0.5)
                {
                    if (m_lstTrace.Count > 0)
                    {
                        double[] adPrev = m_lstTrace[m_lstTrace.Count - 1];
                        double dDx = tx - adPrev[0], dDy = ty - adPrev[1], dDz = dTipZ - adPrev[2];
                        double dDist = Math.Sqrt(dDx * dDx + dDy * dDy);
                        if (dDist > 1.2 && dDist < 15)
                        {
                            int nFill = (int)(dDist / 0.8);
                            for (int k = 1; k <= nFill; k++)
                            {
                                double dR = (double)k / (nFill + 1);
                                m_lstTrace.Add(new double[] { adPrev[0] + dDx * dR, adPrev[1] + dDy * dR, adPrev[2] + dDz * dR });
                            }
                        }
                    }
                    m_lstTrace.Add(new double[] { tx, ty, dTipZ });
                }
            }

            m_C3d.User_Clear();

            // 팔레트 + 적재 목표 마킹
            DrawBox(210, -110, 0, 150, 110, 10, Color.Peru, 1.0f);
            foreach (var t in m_adTargets)
                DrawCyl(t[0], t[1], 10.0, 14f, 1.2f, Color.Gold, 0.9f);

            // 그리기 판 + 펜 거치대
            DrawBox(BOARD_X, BOARD_Y, 0, RECT_HX * 2, RECT_HY * 2, BOARD_TOP, Color.WhiteSmoke, 1.0f);
            DrawBox(PEN_X, PEN_Y, 0, 26, 26, PEN_STAND_H, Color.DimGray, 1.0f);

            // 펜
            if (m_bPenHeld)
            {
                float px, py, pz;
                TcpPos(out px, out py, out pz);
                DrawCyl(px, py, pz - PEN_TIP, PEN_R, PEN_LEN, Color.MidnightBlue, 1.0f);
            }
            else
            {
                DrawCyl(PEN_X, PEN_Y, PEN_STAND_H, PEN_R, PEN_LEN, Color.MidnightBlue, 1.0f);
            }

            // 원통 제품
            foreach (var item in m_lstItems)
                DrawCyl(item.X, item.Y, item.ZBottom, CYL_R, CYL_H, item.C, 1.0f);

            // 펜 자취 — 전 점 표시 (하나 걸러 그리면 선이 끊겨 보인다)
            for (int i = 0; i < m_lstTrace.Count; i++)
                DrawBall(m_lstTrace[i][0], m_lstTrace[i][1], m_lstTrace[i][2], 2.0f, Color.Crimson, 1.0f);

            m_C3d.OjwDraw();
        }

        private void DrawBox(double dX, double dY, double dZBottom, float fSizeX, float fSizeY, float fSizeZ, Color c, float fAlpha)
        {
            m_C3d.User_Set_Init(true);
            m_C3d.User_Set_Model("#0");
            m_C3d.User_Set_Color(c);
            m_C3d.User_Set_Alpha(fAlpha);
            m_C3d.User_Set_Width_Or_Radius(fSizeX);
            m_C3d.User_Set_Height_Or_Depth(fSizeY);
            m_C3d.User_Set_Depth_Or_Cnt(fSizeZ);
            m_C3d.User_Set_Rotation(0, 0, 0, 0);
            m_C3d.User_Set_Translation(0, (float)dX, (float)(dY + fSizeY / 2), (float)(dZBottom + fSizeZ / 2));
            m_C3d.User_Add();
        }

        private void DrawCyl(double dX, double dY, double dZBottom, float fR, float fH, Color c, float fAlpha)
        {
            m_C3d.User_Set_Init(true);
            m_C3d.User_Set_Model("#8");
            m_C3d.User_Set_Color(c);
            m_C3d.User_Set_Alpha(fAlpha);
            m_C3d.User_Set_Width_Or_Radius(fR);
            m_C3d.User_Set_Height_Or_Depth(fH);
            m_C3d.User_Set_Depth_Or_Cnt(24);
            m_C3d.User_Set_Rotation(0, 0, 0, 0);
            m_C3d.User_Set_Translation(0, (float)dX, (float)dY, (float)dZBottom);
            m_C3d.User_Add();
        }

        private void DrawBall(double dX, double dY, double dZ, float fR, Color c, float fAlpha)
        {
            m_C3d.User_Set_Init(true);
            m_C3d.User_Set_Model("#2");
            m_C3d.User_Set_Color(c);
            m_C3d.User_Set_Alpha(fAlpha);
            m_C3d.User_Set_Width_Or_Radius(fR);
            m_C3d.User_Set_Translation(0, (float)dX, (float)dY, (float)dZ);
            m_C3d.User_Add();
        }
    }
}
