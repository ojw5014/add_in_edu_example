// ====================================================================
// ex0b. 모션 테이블 실행기 — OpenJigWare 3D (윈도우 프로그램)
// --------------------------------------------------------------------
// 개념: GUI는 배경일 뿐, 동작은 전부 OpenJigWare.dll 함수로 처리한다.
//   - 예제 버튼 -> 모션 그리드(GridMotionEditor)에 명령(행)이 나타남
//   - Run 버튼  -> 라이브러리의 재생 시스템이 행을 순서대로 실행
//                  (PlayFrame + WaitAction_ByTimer: 스무스 시뮬레이션을
//                   라이브러리가 자체 펌핑 — COjw_12_3D.cs의 애니메이션 처리부)
//   - IK: m_C3d.CalcInv (CCDIK) / FK: m_C3d.CalcF / 궤적: Ojw.CMath.SampleCorners*
// 앱(GUI) 코드는 버튼·그리드 배치와 장면 표시(User_* 호출)만 담당한다.
// ====================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenJigWare;

namespace OmxMotionTable
{
    public class MainForm : Form
    {
        // ── 작업 파라미터 (mm, deg) ──
        private const float Z_APPROACH = 100f;
        private const float Z_GRASP = 45f;
        private const float Z_PLACE = 55f;
        private const float Z_DRAW = 30f;
        private const float GRIP_OPEN = 30f;
        private const float GRIP_HOLD = 4f;
        private const float CYL_R = 12f;
        private const float CYL_H = 40f;
        private static readonly float[] HOME = { 0f, -25f, 35f, 35f };

        // ── UI (배경) ──
        private Panel pnDisp;
        private Panel pnSide;
        private DataGridView dgMotion;
        private TextBox txtLog;
        private TextBox m_txtDummy = new TextBox();

        // ── 3D ──
        private Ojw.C3d m_C3d;
        private Timer m_tmrDisp;
        private bool m_bBusy = false;
        private bool m_bStopReq = false;

        // ── 장면 상태 (표시용) ──
        private class CItem
        {
            public string Name;
            public double X, Y, ZBottom;
            public double[] Start;
            public Color C;
            public bool Attached;
            public double[] RelWorld;   // 부착 시 TCP 기준 월드 오프셋 (원통이라 회전 불필요)
        }
        private readonly List<CItem> m_lstItems = new List<CItem>();
        private readonly double[][] m_adTargets = { new double[] { 170, -110 }, new double[] { 210, -110 }, new double[] { 250, -110 } };
        private readonly List<double[]> m_lstTrace = new List<double[]>();
        private bool m_bTraceOn = false;

        // ── 모션 그리드 행 관리 (행 번호 -> 액션/설명) ──
        private int m_nRows = 0;
        private readonly List<string> m_lstAction = new List<string>();    // "", "grab:0", "rel:0", "trace:on", "trace:off"
        private readonly List<string> m_lstCaption = new List<string>();
        private const int MAX_ROWS = 200;

        // ── OMX Follower 변형 DH (MakeUrdf 예제 14와 동일) ──
        private static readonly string DH_OMX = string.Join("\r\n", new[]
        {
            "// *OMX Follower (Joints Only)",
            "[0,0,90,90],[-1,0,0]",
            "@[omx-ai_follower_base_plate.stl,-16777216,1.0],[0,-1],[0,0,11],[0,0,0]",
            "[0,0,0,-90],[-1,0,0]",
            "[0,13,0,0],[-1,0,0]",
            "[0,40,0,0],[-1,0,0]",
            "@[xl-430.stl,-16777216,1.0],[1,0],[0,0.25,-19],[0,0,180]",
            "@[omx-ai_follower_frame1.stl,-16777216,1.0],[1,0],[0,0.25,1.2],[0,90,180]",
            "[0,0,0,0],[0,0,0] // - Axis11",
            "@[omx-ai_follower_frame2.stl,-16777216,1.0],[1,1],[0,0,21.5],[0,-90,0]",
            "@[xl-430.stl,-16777216,1.0],[1,1],[0,0,44.5],[0,90,90]",
            "[0,0,90,0],[-1,0,0]",
            "[0,0,0,90],[-1,0,0]",
            "[0,0,90,0],[-1,0,0]",
            "[44.5,0,0,0],[-1,0,0]",
            "[0,0,0,0],[1,0,0] // - Axis12",
            "@[omx-ai_follower_link_l.stl,-1.677722E+07,1.0],[1,1],[0,0,0],[90,0,0]",
            "[113.15,0,0,0],[-1,0,0]",
            "[0,0,90,0],[-1,0,0]",
            "[41.5,0,0,0],[-1,0,0]",
            "@[xl-430.stl,-16777216,1.0],[1,2],[0,0,0],[0,0,-90]",
            "[0,0,0,0],[2,0,0] // - Axis13",
            "@[omx-ai_follower_link_u.stl,-16777216,1.0],[1,2],[0,0,0],[90,0,0]",
            "[162,0,0,0],[-1,0,0]",
            "@[dc15_dummy_std.stl,-16777216,1.0],[1,3],[-7.5,0,1.5],[0,0,-90]",
            "[0,0,0,0],[3,0,0] // - Axis14",
            "@[omx-ai_follower_frame3.stl,-16777216,1.0],[1,3],[0,0,0],[90,0,180]",
            "[43.2,0,0,0],[-1,0,0]",
            "[0,0,90,90],[-1,0,0]",
            "@[dc15_dummy_std.stl,-16777216,1.0],[1,4],[-7.5,0,-12.8],[0,0,-90]",
            "[0,60,0,0],[-1,0,0],[2,0]",
            "!",
            "[0,-60,0,0],[-1,0,0]",
            "[0,0,0,0],[4,0,0] // - Axis15",
            "@[omx-ai_follower_frame4.stl,-16777216,1.0],[1,4],[0,0,1.5],[0,0,90]",
            "[0,15,-90,90],[-1,0,0]",
            "@[dc15_dummy_std.stl,-16777216,1.0],[1,5],[0,0,1.5],[0,0,90]",
            "[-7.5,0,0,0],[-1,0,0]",
            "[0,0,90,0],[5,0,0] // - Axis16",
            "@[omx-ai_follower_gripper_l.stl,-16777216,1.0],[2,0],[0,0,16.2],[0,0,-90]",
            "[60,0,0,0],[-1,0,0]",
            "[-60,0,0,0],[-1,0,0]",
            "[0,0,0,0],[5,1,0] // - Axis16",
            "[7.5,0,-90,0],[-1,0,0]",
            "[10.80,0,0,0],[-1,0,0]",
            "[0,0,90,0],[5,1,0] // - Axis16",
            "@[omx-ai_follower_gripper_r.stl,-16777216,1.0],[1,5],[0,0,16.2],[0,0,0]",
            "[60,0,0,0],[-1,0,0]",
            "[-60,0,0,0],[-1,0,0]",
            "[0,0,0,0],[5,0,0] // - Axis16",
            "[10.8,0,90,90],[-1,0,0]",
        });

        public MainForm()
        {
            BuildUi();
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        // ================= UI (배경) =================
        private void BuildUi()
        {
            Text = "OMX 모션 테이블 실행기 — OpenJigWare 3D";
            ClientSize = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;

            pnDisp = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            pnSide = new Panel { Dock = DockStyle.Right, Width = 320, Padding = new Padding(8) };
            dgMotion = new DataGridView
            {
                Dock = DockStyle.Bottom, Height = 250,
                AllowUserToAddRows = false, RowHeadersWidth = 60,
                BackgroundColor = Color.FromArgb(30, 30, 34),
            };

            txtLog = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 9f), BackColor = Color.FromArgb(24, 24, 28), ForeColor = Color.Gainsboro,
            };
            pnSide.Controls.Add(txtLog);

            pnSide.Controls.Add(MakeButton("■ Stop (현재 행까지)", Color.FromArgb(160, 60, 60), (s, e) => { m_bStopReq = true; }));
            pnSide.Controls.Add(MakeButton("▶ Run (모션 테이블 실행)", Color.FromArgb(50, 140, 80), (s, e) => RunGuard(RunTable)));

            pnSide.Controls.Add(MakeButton("예제 3: 사각형 (베지어 코너)", Color.FromArgb(70, 130, 180), (s, e) => RunGuard(() => BuildRectExample(true))));
            pnSide.Controls.Add(MakeButton("예제 2: 사각형 (직선)", Color.FromArgb(70, 130, 180), (s, e) => RunGuard(() => BuildRectExample(false))));
            pnSide.Controls.Add(MakeButton("예제 1: 픽앤플레이스", Color.FromArgb(70, 130, 180), (s, e) => RunGuard(BuildPickPlaceExample)));

            var lblTitle = new Label
            {
                Dock = DockStyle.Top, Height = 48,
                Text = "예제 버튼 = 모션 테이블 생성\r\nRun = 라이브러리 재생 (PlayFrame)",
                TextAlign = ContentAlignment.MiddleCenter, Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold),
            };
            pnSide.Controls.Add(lblTitle);

            Controls.Add(pnDisp);
            Controls.Add(pnSide);
            Controls.Add(dgMotion);
        }

        private Button MakeButton(string strText, Color c, EventHandler onClick)
        {
            var btn = new Button
            {
                Dock = DockStyle.Top, Height = 40, Text = strText,
                FlatStyle = FlatStyle.Flat, Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold),
                BackColor = c, ForeColor = Color.White,
            };
            btn.Click += onClick;
            return btn;
        }

        private void RunGuard(Action action)
        {
            if (m_bBusy) { Ojw.printf(txtLog, "[BUSY] 동작 중입니다...\r\n"); return; }
            m_bBusy = true;
            try { action(); }
            catch (Exception ex) { Ojw.printf(txtLog, "[ERROR] {0}\r\n", ex.Message); }
            finally { m_bBusy = false; }
        }

        // ================= 초기화 =================
        private void MainForm_Load(object sender, EventArgs e)
        {
            Ojw.printf_Init(txtLog);

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

            // 로봇 로딩
            string strCmd = "", strXml = "";
            m_C3d.m_CHeader.pstrKinematics[0] = DH_OMX;
            m_C3d.CheckForward();
            m_C3d.m_CHeader.strDrawModel = m_C3d.MakeDHSkeleton_Urdf(
                true, true, false, true, 10.0f, 20.0f, Color.Violet,
                DH_OMX, 0, 0, 0, 0, 0, 0, 0,
                ref m_txtDummy, ref strCmd, ref strXml);
            m_C3d.CompileDesign();

            // 모션 그리드 연결 + 스무스 시뮬레이션 (라이브러리 애니메이션 처리부)
            m_C3d.GridMotionEditor_Init(dgMotion, 46, MAX_ROWS);
            m_C3d.SetSimulation_With_PlayFrame(true);
            m_C3d.SetSimulation_Smooth(true);

            // 장면: 원통 제품 3개 + 팔레트
            m_lstItems.Add(NewItem("obj1", 200, 160, Color.IndianRed));
            m_lstItems.Add(NewItem("obj2", 200, 110, Color.MediumSeaGreen));
            m_lstItems.Add(NewItem("obj3", 200, 60, Color.CornflowerBlue));

            m_tmrDisp = new Timer { Interval = 33 };
            m_tmrDisp.Tick += tmrDisp_Tick;
            m_tmrDisp.Enabled = true;

            SetPose(HOME, GRIP_OPEN);

            Ojw.printf(txtLog, "준비 완료 — 예제 버튼을 누르면 모션 테이블이 생성됩니다.\r\n");
            Ojw.printf(txtLog, "테이블 값은 직접 수정해서 Run 해볼 수 있습니다.\r\n\r\n");
        }

        private CItem NewItem(string strName, double dX, double dY, Color c)
        {
            return new CItem { Name = strName, X = dX, Y = dY, ZBottom = 0, Start = new double[] { dX, dY }, C = c };
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_bStopReq = true;
            if (m_tmrDisp != null) { m_tmrDisp.Stop(); m_tmrDisp = null; }
            if (m_C3d != null) { m_C3d.Stop(); m_C3d.DInit(); m_C3d = null; }
        }

        // ================= 렌더 (장면 표시 — User_* 라이브러리 호출) =================
        private void tmrDisp_Tick(object sender, EventArgs e)
        {
            if (m_C3d == null) return;

            // 부착된 제품은 TCP(라이브러리 FK)를 따라 이동
            float tx, ty, tz;
            m_C3d.CalcF(0, false, out tx, out ty, out tz);
            foreach (var item in m_lstItems)
            {
                if (!item.Attached) continue;
                item.X = tx + item.RelWorld[0];
                item.Y = ty + item.RelWorld[1];
                item.ZBottom = tz + item.RelWorld[2] - CYL_H / 2;
            }
            // 자취 기록
            if (m_bTraceOn)
            {
                if (m_lstTrace.Count == 0 ||
                    Math.Abs(m_lstTrace[m_lstTrace.Count - 1][0] - tx) > 0.5 ||
                    Math.Abs(m_lstTrace[m_lstTrace.Count - 1][1] - ty) > 0.5)
                    m_lstTrace.Add(new double[] { tx, ty, tz });
            }

            m_C3d.User_Clear();
            // 팔레트 + 목표 마킹
            DrawBox(210, -110, 0, 150, 110, 10, Color.Peru, 1.0f);
            foreach (var t in m_adTargets)
                DrawCyl(t[0], t[1], 10.0, 14f, 1.2f, Color.Gold, 0.9f);
            // 원통 제품
            foreach (var item in m_lstItems)
                DrawCyl(item.X, item.Y, item.ZBottom, CYL_R, CYL_H, item.C, 1.0f);
            // 자취
            for (int i = 0; i < m_lstTrace.Count; i += 2)
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

        // ================= 모션 테이블 만들기 (예제 = 그리드 행) =================
        private void SetPose(float[] afArm, float fGrip)
        {
            // 이 앱의 DH는 모션 그리드 규약대로 모터 0~5 (0~3=팔, 4=툴 롤, 5=그리퍼)
            m_C3d.SetData(0, afArm[0]); m_C3d.SetData(1, afArm[1]);
            m_C3d.SetData(2, afArm[2]); m_C3d.SetData(3, afArm[3]);
            m_C3d.SetData(4, 0); m_C3d.SetData(5, fGrip);
        }

        private void ClearTable()
        {
            for (int i = 0; i < Math.Max(m_nRows, 1); i++)
                m_C3d.GridMotionEditor_SetEnable(i, false);
            m_nRows = 0;
            m_lstAction.Clear();
            m_lstCaption.Clear();
        }

        /// <summary>그리드에 행 추가 — 관절각 직접 지정</summary>
        private void AddRow(string strCaption, float[] afArm, float fGrip, int nTime, int nDelay, string strAction = "")
        {
            if (m_nRows >= MAX_ROWS) return;
            int n = m_nRows;
            m_C3d.GridMotionEditor_SetEnable(n, true);
            m_C3d.GridMotionEditor_SetMotor(n, 0, afArm[0]);
            m_C3d.GridMotionEditor_SetMotor(n, 1, afArm[1]);
            m_C3d.GridMotionEditor_SetMotor(n, 2, afArm[2]);
            m_C3d.GridMotionEditor_SetMotor(n, 3, afArm[3]);
            m_C3d.GridMotionEditor_SetMotor(n, 4, 0);
            m_C3d.GridMotionEditor_SetMotor(n, 5, fGrip);
            m_C3d.GridMotionEditor_SetTime(n, nTime);
            m_C3d.GridMotionEditor_SetDelay(n, nDelay);
            m_lstAction.Add(strAction);
            m_lstCaption.Add(strCaption);
            m_nRows++;
        }

        /// <summary>그리드에 행 추가 — 목표 좌표를 라이브러리 IK(CCDIK)로 풀어서 기록.
        /// CalcInv는 현재 자세에서 시작하므로, 직전 행의 각도를 SetData로 시드해 연속 해를 얻는다.</summary>
        private float[] AddRowXyz(string strCaption, float fX, float fY, float fZ, float fGrip, int nTime, int nDelay, string strAction = "")
        {
            float[] afAngles = m_C3d.CalcInv(0, fX, fY, fZ, 300, 0.5f);
            int[] anIds = m_C3d.CalcInv_MotorIDs(0);
            var afArm = new float[4];
            if (afAngles != null && anIds != null)
            {
                for (int i = 0; i < anIds.Length && i < afAngles.Length; i++)
                {
                    m_C3d.SetData(anIds[i], afAngles[i]);   // 다음 IK의 시드
                    if (anIds[i] >= 0 && anIds[i] <= 3) afArm[anIds[i]] = afAngles[i];
                }
            }
            else
            {
                Ojw.printf(txtLog, "[WARN] IK 실패: ({0:F0}, {1:F0}, {2:F0})\r\n", fX, fY, fZ);
            }
            AddRow(strCaption, afArm, fGrip, nTime, nDelay, strAction);
            return afArm;
        }

        private void ResetSceneObjects()
        {
            foreach (var item in m_lstItems)
            {
                item.Attached = false;
                item.X = item.Start[0]; item.Y = item.Start[1]; item.ZBottom = 0;
            }
            m_lstTrace.Clear();
            m_bTraceOn = false;
        }

        // ── 예제 1: 픽앤플레이스 (제품 1개) ──
        private void BuildPickPlaceExample()
        {
            ResetSceneObjects();
            ClearTable();
            m_C3d.CalcInv_SetIKAlgorithm(0);   // CCDIK (강의 Part3)
            SetPose(HOME, GRIP_OPEN);          // IK 시드 = 홈

            CItem item = m_lstItems[1];        // obj2 (200, 110)
            double tx = m_adTargets[1][0], ty = m_adTargets[1][1];

            AddRow("홈 자세", HOME, GRIP_OPEN, 1200, 0);
            AddRowXyz("제품 위 접근", (float)item.X, (float)item.Y, Z_APPROACH, GRIP_OPEN, 1200, 0);
            AddRowXyz("내려가기", (float)item.X, (float)item.Y, Z_GRASP, GRIP_OPEN, 1000, 100, "grab:1");
            float[] a = AddRowXyz("파지 (그리퍼 닫기)", (float)item.X, (float)item.Y, Z_GRASP, GRIP_HOLD, 400, 100);
            AddRowXyz("들어올리기", (float)item.X, (float)item.Y, Z_APPROACH, GRIP_HOLD, 1000, 0);
            AddRowXyz("목표 위로 이동", (float)tx, (float)ty, Z_APPROACH, GRIP_HOLD, 1400, 0);
            AddRowXyz("내려놓을 높이", (float)tx, (float)ty, Z_PLACE, GRIP_HOLD, 1000, 100);
            AddRowXyz("그리퍼 열기", (float)tx, (float)ty, Z_PLACE, GRIP_OPEN, 400, 100, "rel:1");
            AddRowXyz("복귀 경유점", (float)tx, (float)ty, Z_APPROACH, GRIP_OPEN, 800, 0);
            AddRow("홈 복귀", HOME, GRIP_OPEN, 1200, 0);

            SetPose(HOME, GRIP_OPEN);
            PrintTable("예제 1: 픽앤플레이스");
        }

        // ── 예제 2/3: 사각형 그리기 (직선 / 베지어 코너) ──
        private void BuildRectExample(bool bBezier)
        {
            ResetSceneObjects();
            ClearTable();
            m_C3d.CalcInv_SetIKAlgorithm(0);
            SetPose(HOME, GRIP_OPEN);

            // 닫힌 사각형 (중심 210, 0 / 80 x 70) — 궤적 샘플링은 라이브러리 함수 사용
            var lstCorners = new List<float[]>
            {
                new float[] { 170, -35, Z_DRAW },
                new float[] { 250, -35, Z_DRAW },
                new float[] { 250,  35, Z_DRAW },
                new float[] { 170,  35, Z_DRAW },
            };
            lstCorners.Add(lstCorners[0]);
            List<float[]> lstPath = bBezier
                ? Ojw.CMath.SampleCornersBlend(lstCorners, 32)
                : Ojw.CMath.SampleCornersLinear(lstCorners, 32);

            AddRow("홈 자세", HOME, GRIP_OPEN, 1200, 0);
            AddRowXyz("시작점 위 접근", lstPath[0][0], lstPath[0][1], Z_APPROACH, GRIP_OPEN, 1200, 0);
            AddRowXyz("펜 내리기", lstPath[0][0], lstPath[0][1], Z_DRAW, GRIP_OPEN, 800, 100, "trace:on");
            for (int i = 1; i < lstPath.Count; i++)
                AddRowXyz("경로 " + i, lstPath[i][0], lstPath[i][1], Z_DRAW, GRIP_OPEN, 150, 0,
                          (i == lstPath.Count - 1) ? "trace:off" : "");
            AddRowXyz("펜 들기", lstPath[0][0], lstPath[0][1], Z_APPROACH, GRIP_OPEN, 800, 0);
            AddRow("홈 복귀", HOME, GRIP_OPEN, 1200, 0);

            SetPose(HOME, GRIP_OPEN);
            PrintTable(bBezier ? "예제 3: 사각형 (베지어 코너)" : "예제 2: 사각형 (직선)");
        }

        private void PrintTable(string strName)
        {
            Ojw.printf(txtLog, "\r\n===== {0} =====\r\n", strName);
            for (int i = 0; i < m_nRows; i++)
                Ojw.printf(txtLog, "행 {0,2}: {1}\r\n", i, m_lstCaption[i]);
            Ojw.printf(txtLog, "모션 테이블 {0}행 생성 — ▶ Run 을 누르세요.\r\n", m_nRows);
        }

        // ================= Run — 라이브러리 재생 시스템 =================
        private void RunTable()
        {
            if (m_nRows <= 0) { Ojw.printf(txtLog, "먼저 예제 버튼으로 모션 테이블을 만드세요.\r\n"); return; }
            m_bStopReq = false;
            ResetSceneObjects();

            Ojw.printf(txtLog, "\r\n[RUN] 모션 테이블 재생 시작 ({0}행)\r\n", m_nRows);
            m_C3d.WaitAction_SetTimer();       // 라이브러리 재생 타이머 초기화
            m_C3d.Start_Set();

            for (int nLine = 0; nLine < m_nRows; nLine++)
            {
                if (m_bStopReq) { Ojw.printf(txtLog, "[STOP] 사용자 정지 (행 {0}에서 중단)\r\n", nLine); break; }

                int nTime = m_C3d.GridMotionEditor_GetTime(nLine);
                int nDelay = m_C3d.GridMotionEditor_GetDelay(nLine);

                m_C3d.PlayFrame(nLine);                        // 스무스 시뮬레이션 시작 (라이브러리)
                m_C3d.WaitAction_ByTimer(nTime + nDelay);      // 라이브러리가 애니메이션 펌핑하며 대기

                DoAction(m_lstAction[nLine]);                  // 씬 이벤트 (잡기/놓기/자취)
            }

            m_C3d.Start_Reset();
            m_bTraceOn = false;
            Ojw.printf(txtLog, "[RUN] 재생 완료\r\n");
            ReportResult();
        }

        private void DoAction(string strAction)
        {
            if (String.IsNullOrEmpty(strAction)) return;
            string[] parts = strAction.Split(':');
            float tx, ty, tz;

            if (parts[0] == "grab")
            {
                CItem item = m_lstItems[int.Parse(parts[1])];
                m_C3d.CalcF(0, false, out tx, out ty, out tz);         // TCP = 라이브러리 FK
                double cx = item.X, cy = item.Y, cz = item.ZBottom + CYL_H / 2;
                double dist = Math.Sqrt((tx - cx) * (tx - cx) + (ty - cy) * (ty - cy) + (tz - cz) * (tz - cz));
                if (dist < 45)
                {
                    item.RelWorld = new double[] { cx - tx, cy - ty, cz - tz };
                    item.Attached = true;
                    Ojw.printf(txtLog, "[GRIP] {0} 파지 성공 (TCP-제품 거리 {1:F0}mm)\r\n", item.Name, dist);
                }
                else Ojw.printf(txtLog, "[GRIP] {0} 파지 실패 (거리 {1:F0}mm)\r\n", item.Name, dist);
            }
            else if (parts[0] == "rel")
            {
                CItem item = m_lstItems[int.Parse(parts[1])];
                item.Attached = false;
                item.ZBottom = 10;   // 팔레트 위 안착
                Ojw.printf(txtLog, "[GRIP] {0} 내려놓기\r\n", item.Name);
            }
            else if (parts[0] == "trace")
            {
                m_bTraceOn = (parts[1] == "on");
                Ojw.printf(txtLog, "[DRAW] 자취 {0}\r\n", m_bTraceOn ? "시작" : "종료");
            }
        }

        private void ReportResult()
        {
            // 픽앤플레이스 결과 판정 (부착 이력이 있는 제품만)
            for (int i = 0; i < m_lstItems.Count; i++)
            {
                CItem item = m_lstItems[i];
                if (item.X == item.Start[0] && item.Y == item.Start[1]) continue;
                double tx = m_adTargets[1][0], ty = m_adTargets[1][1];
                double dErr = Math.Sqrt((item.X - tx) * (item.X - tx) + (item.Y - ty) * (item.Y - ty));
                Ojw.printf(txtLog, "[RESULT] {0} final=({1:F1}, {2:F1}) target=({3:F0}, {4:F0}) err={5:F1}mm -> {6}\r\n",
                    item.Name, item.X, item.Y, tx, ty, dErr, (dErr <= 50) ? "OK" : "FAIL");
            }
            if (m_lstTrace.Count > 0)
                Ojw.printf(txtLog, "[RESULT] 자취 {0}점 기록\r\n", m_lstTrace.Count);
        }
    }
}
