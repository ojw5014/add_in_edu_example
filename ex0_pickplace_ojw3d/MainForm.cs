// ====================================================================
// ex0. 로봇팔 픽앤플레이스 / 사각형 그리기 — OpenJigWare 3D (윈도우 프로그램)
// --------------------------------------------------------------------
// 강의 "기초부터 시작하는 로봇 공학"의 도구·API 스타일 그대로:
//   - 3D: Ojw.C3d (ojwSimul/MakeUrdf와 동일 패턴)
//   - 제어: COmxRobot.Play / SyncRead / CalcInv / CalcXyz (강의 명령 형식)
//   - 로그: Ojw.printf
// 로봇: OMX Follower (MakeUrdf 예제 'OMX Follower (Joints Only)' 변형 DH)
// 장면: 원통 제품 3개(캔) + 팔레트 + 그리기 판 + 펜 거치대
// ====================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenJigWare;

namespace OmxPickPlace
{
    public class MainForm : Form
    {
        // ── 작업 파라미터 (mm, deg) ──
        private const float Z_APPROACH = 100f;   // 접근 높이 (경유점)
        private const float PITCH = -90f;        // 위에서 아래로 집는 자세
        private const float GRIP_OPEN = 30f;     // 그리퍼 열림 (간격 49mm)
        private const float GRIP_HOLD = 4f;      // 원통(지름 24mm) 파지폭 — 살짝 무는 정도
        private const float GRIP_PEN = -10f;     // 펜(지름 12mm) 파지폭 — 펜 굵기만큼만 닫음
        private const float TOL_OK = 50f;        // 판정 허용 오차 (mm)

        // 원통 제품 (캔): 지름 24, 높이 40
        private const float CYL_R = 12f;
        private const float CYL_H = 40f;
        private const float Z_GRASP = 45f;       // 집는 높이 (TCP) — 원통 윗부분을 감쌈
        private const float Z_PLACE = 55f;       // 놓는 높이 (팔레트 위)

        // 펜: 지름 12, 길이 70. 파지 시 TCP 아래로 55mm가 나옴 (끝 = TCP-55)
        private const float PEN_R = 6f;
        private const float PEN_LEN = 70f;
        private const float PEN_TIP = 55f;                 // TCP에서 펜 끝까지
        private const float PEN_X = 140f, PEN_Y = 170f;    // 펜 거치대 위치
        private const float PEN_STAND_H = 6f;              // 거치대 받침 높이

        // 그리기 판: 중심 (210, 0), 두께 8 — 크기는 그리는 사각형(80x70)과 일치
        //   (빨간 라인이 판의 테두리를 따라 그려지는 느낌이 되도록)
        private const float BOARD_X = 210f, BOARD_Y = 0f;
        private const float RECT_HX = 40f, RECT_HY = 35f;   // 사각형 반폭 (80 x 70)
        private const float BOARD_TOP = 8f;
        private const float Z_DRAW = BOARD_TOP + PEN_TIP + 0.5f;   // 펜 끝이 판에 닿는 TCP 높이

        // ── UI ──
        private Panel pnDisp;
        private Panel pnSide;
        private Button btnPick1, btnPick3, btnRect, btnRectBez, btnReset;
        private ComboBox cmbSpeed;
        private TextBox txtLog;
        private TextBox m_txtDummy = new TextBox();   // MakeDHSkeleton_Urdf ref 파라미터용

        // ── 3D / 로봇 ──
        private Ojw.C3d m_C3d;
        private Timer m_tmrDisp;
        private COmxRobot m_Robot;
        private bool m_bBusy = false;

        // ── 장면 상태 ──
        private class CItem
        {
            public string Name;
            public double X, Y, ZBottom;      // 바닥 기준 위치 (mm)
            public double[] Start;            // 리셋용 시작 위치
            public Color C;
            public bool Attached;
            public double[] Rel;              // 부착 시 TCP 프레임 기준 상대 위치 (중심)
        }
        private readonly List<CItem> m_lstItems = new List<CItem>();
        private readonly double[][] m_adTargets = { new double[] { 170, -110 }, new double[] { 210, -110 }, new double[] { 250, -110 } };
        private readonly List<double[]> m_lstTrace = new List<double[]>();
        private bool m_bPenDown = false;      // 그리기 중 (자취 기록)
        private bool m_bPenHeld = false;      // 펜을 잡고 있는가

        // ── OMX Follower 변형 DH (MakeUrdf 예제 14와 동일) ──
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
            BuildUi();
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        // ================= UI 구성 =================
        private void BuildUi()
        {
            Text = "OMX 픽앤플레이스 — OpenJigWare 3D";
            ClientSize = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;

            pnDisp = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            pnSide = new Panel { Dock = DockStyle.Right, Width = 330, Padding = new Padding(8) };

            txtLog = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 9f), BackColor = Color.FromArgb(24, 24, 28), ForeColor = Color.Gainsboro,
            };
            pnSide.Controls.Add(txtLog);

            cmbSpeed = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Height = 30 };
            cmbSpeed.Items.AddRange(new object[] { "속도 0.5배", "속도 1배", "속도 2배" });
            cmbSpeed.SelectedIndex = 1;
            cmbSpeed.SelectedIndexChanged += (s, e) =>
            {
                if (m_Robot != null) m_Robot.SpeedScale = new float[] { 0.5f, 1f, 2f }[cmbSpeed.SelectedIndex];
            };
            pnSide.Controls.Add(cmbSpeed);

            btnReset = MakeButton("리셋 (초기 상태)", (s, e) => RunGuard(ResetScene));
            pnSide.Controls.Add(btnReset);
            btnRectBez = MakeButton("판 테두리 사각형 (베지어 코너)", (s, e) => RunGuard(() => RunDrawRect(true)));
            pnSide.Controls.Add(btnRectBez);
            btnRect = MakeButton("판 테두리 사각형 (직선)", (s, e) => RunGuard(() => RunDrawRect(false)));
            pnSide.Controls.Add(btnRect);
            btnPick3 = MakeButton("팔레타이징 (제품 3개)", (s, e) => RunGuard(() => RunPickPlace(3)));
            pnSide.Controls.Add(btnPick3);
            btnPick1 = MakeButton("픽앤플레이스 (제품 1개)", (s, e) => RunGuard(() => RunPickPlace(1)));
            pnSide.Controls.Add(btnPick1);

            var lblTitle = new Label
            {
                Dock = DockStyle.Top, Height = 34, Text = "OMX Follower — Play / SyncRead / CalcInv",
                TextAlign = ContentAlignment.MiddleCenter, Font = new Font("맑은 고딕", 10f, FontStyle.Bold),
            };
            pnSide.Controls.Add(lblTitle);

            Controls.Add(pnDisp);
            Controls.Add(pnSide);
        }

        private Button MakeButton(string strText, EventHandler onClick)
        {
            var btn = new Button
            {
                Dock = DockStyle.Top, Height = 42, Text = strText,
                FlatStyle = FlatStyle.Flat, Font = new Font("맑은 고딕", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(70, 130, 180), ForeColor = Color.White,
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

            // 3D 초기화 (ojwSimul Init3d 패턴)
            m_C3d = new Ojw.C3d();
            m_C3d.Init(pnDisp);
            m_C3d.SetFilePath_Design(Application.StartupPath);
            m_C3d.SetStandardAxis(true);
            m_C3d.SetVirtualClass_Enable(false);
            m_C3d.SetAseFile_Path("stl");
            m_C3d.SetSkeletonView(false);   // STL 메시만 표시
            m_C3d.SetMeshView(true);
            m_C3d.SetMouseMode(0);          // 우클릭 드래그 = 카메라 회전
            m_C3d.SetCoordMode(1);          // X=정면, Z=위
            m_C3d.SetAngle_Display(20.0f, 0.0f, 15.0f);

            // 로봇 로딩 (변형 DH -> 3D)
            string strCmd = "", strXml = "";
            m_C3d.m_CHeader.pstrKinematics[0] = DH_OMX;
            m_C3d.CheckForward();
            m_C3d.m_CHeader.strDrawModel = m_C3d.MakeDHSkeleton_Urdf(
                true, true,
                false,          // 스켈레톤 숨김 (STL만)
                true,           // 메시 표시
                10.0f, 20.0f, Color.Violet,
                DH_OMX, 0,
                0, 0, 0, 0, 0, 0,
                ref m_txtDummy, ref strCmd, ref strXml);
            m_C3d.CompileDesign();

            m_Robot = new COmxRobot(m_C3d);
            m_Robot.OnFrame = OnRobotFrame;

            // 장면: 원통 제품 3개 (간격 50mm — 그리퍼가 이웃과 닿지 않는 배치)
            m_lstItems.Add(NewItem("obj1", 200, 160, Color.IndianRed));
            m_lstItems.Add(NewItem("obj2", 200, 110, Color.MediumSeaGreen));
            m_lstItems.Add(NewItem("obj3", 200, 60, Color.CornflowerBlue));

            // 렌더 루프
            m_tmrDisp = new Timer { Interval = 33 };
            m_tmrDisp.Tick += tmrDisp_Tick;
            m_tmrDisp.Enabled = true;

            // 초기 자세
            m_Robot.SetAngle(11, 0); m_Robot.SetAngle(12, -25);
            m_Robot.SetAngle(13, 35); m_Robot.SetAngle(14, 35);
            m_Robot.SetAngle(15, 0); m_Robot.SetAngle(16, GRIP_OPEN);

            Ojw.printf(txtLog, "OMX Follower 준비 완료 — 버튼으로 실행하세요.\r\n");
            Ojw.printf(txtLog, "(마우스 우클릭+드래그 = 카메라 회전)\r\n\r\n");
        }

        private CItem NewItem(string strName, double dX, double dY, Color c)
        {
            return new CItem { Name = strName, X = dX, Y = dY, ZBottom = 0, Start = new double[] { dX, dY }, C = c };
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_tmrDisp != null) { m_tmrDisp.Stop(); m_tmrDisp = null; }
            if (m_C3d != null) { m_C3d.Stop(); m_C3d.DInit(); m_C3d = null; }
        }

        // ================= 렌더 =================
        private static readonly bool PROBE = Environment.CommandLine.Contains("--probe");

        private void tmrDisp_Tick(object sender, EventArgs e)
        {
            if (m_C3d == null) return;
            m_C3d.User_Clear();

            if (PROBE)
            {
                DrawBall(300, 0, 50, 12, Color.Red, 1f);
                DrawBall(0, 300, 50, 12, Color.Lime, 1f);
                DrawBall(0, 0, 300, 12, Color.Blue, 1f);
                DrawBox(150, 0, 0, 200, 10, 10, Color.Orange, 1f);
                DrawBox(0, 150, 0, 10, 200, 10, Color.Magenta, 1f);
                DrawBox(150, -150, 0, 10, 10, 200, Color.Cyan, 1f);
                m_C3d.OjwDraw();
                return;
            }

            // 팔레트 (판) + 적재 목표 마킹 (바닥의 원형 표시)
            DrawBox(210, -110, 0, 150, 110, 10, Color.Peru, 1.0f);
            foreach (var t in m_adTargets)
                DrawCyl(t[0], t[1], 10.0, 14f, 1.2f, Color.Gold, 0.9f);

            // 그리기 판 (화이트보드, 사각형과 동일 크기) + 펜 거치대
            DrawBox(BOARD_X, BOARD_Y, 0, RECT_HX * 2, RECT_HY * 2, BOARD_TOP, Color.WhiteSmoke, 1.0f);
            DrawBox(PEN_X, PEN_Y, 0, 26, 26, PEN_STAND_H, Color.DimGray, 1.0f);

            // 펜: 잡고 있으면 TCP 아래에 매달림, 아니면 거치대에 세워짐
            if (m_bPenHeld)
            {
                float x, y, z;
                m_Robot.CalcXyz(out x, out y, out z);
                DrawCyl(x, y, z - PEN_TIP, PEN_R, PEN_LEN, Color.MidnightBlue, 1.0f);
            }
            else
            {
                DrawCyl(PEN_X, PEN_Y, PEN_STAND_H, PEN_R, PEN_LEN, Color.MidnightBlue, 1.0f);
            }

            // 원통 제품 (캔)
            foreach (var item in m_lstItems)
                DrawCyl(item.X, item.Y, item.ZBottom, CYL_R, CYL_H, item.C, 1.0f);

            // 펜 자취 (판 위의 잉크 점)
            for (int i = 0; i < m_lstTrace.Count; i += 2)
                DrawBall(m_lstTrace[i][0], m_lstTrace[i][1], m_lstTrace[i][2], 2.0f, Color.Crimson, 1.0f);

            m_C3d.OjwDraw();
        }

        /// <summary>바닥 기준 직육면체 — (x, y) 중심 / zBottom 바닥.
        /// #0 박스 실측 규약: Width→로봇X(중심 기준), Height→로봇Y(0에서 -H로), Depth→로봇Z(중심 기준)</summary>
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

        /// <summary>바닥 기준 원통 — (x, y) 중심 / zBottom 바닥에서 +Z로 fH만큼</summary>
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

        // ================= 부착(파지) 처리 =================
        private void OnRobotFrame()
        {
            // 부착된 제품은 TCP 프레임을 따라 이동 (원통은 축 대칭이라 회전이 드러나지 않음)
            foreach (var item in m_lstItems)
            {
                if (!item.Attached) continue;
                var T = m_Robot.TcpFrame();
                double cx = T[0, 3] + T[0, 0] * item.Rel[0] + T[0, 1] * item.Rel[1] + T[0, 2] * item.Rel[2];
                double cy = T[1, 3] + T[1, 0] * item.Rel[0] + T[1, 1] * item.Rel[1] + T[1, 2] * item.Rel[2];
                double cz = T[2, 3] + T[2, 0] * item.Rel[0] + T[2, 1] * item.Rel[1] + T[2, 2] * item.Rel[2];
                item.X = cx; item.Y = cy; item.ZBottom = cz - CYL_H / 2;
            }
            // 펜이 내려간 상태면 펜 끝 위치를 자취로 기록
            if (m_bPenDown)
            {
                float x, y, z;
                m_Robot.CalcXyz(out x, out y, out z);
                m_lstTrace.Add(new double[] { x, y, z - PEN_TIP });
            }
        }

        private bool Grab(CItem item)
        {
            float x, y, z;
            m_Robot.CalcXyz(out x, out y, out z);                       // 정기구학으로 TCP 확인
            double cx = item.X, cy = item.Y, cz = item.ZBottom + CYL_H / 2;
            double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy) + (z - cz) * (z - cz));
            if (dist < 40)
            {
                var T = m_Robot.TcpFrame();
                double dx = cx - T[0, 3], dy = cy - T[1, 3], dz = cz - T[2, 3];
                item.Rel = new double[]
                {
                    T[0, 0] * dx + T[1, 0] * dy + T[2, 0] * dz,
                    T[0, 1] * dx + T[1, 1] * dy + T[2, 1] * dz,
                    T[0, 2] * dx + T[1, 2] * dy + T[2, 2] * dz,
                };
                item.Attached = true;
                Ojw.printf(txtLog, "[GRIP] {0} 파지 성공 (TCP-제품 거리 {1:F0}mm)\r\n", item.Name, dist);
                return true;
            }
            Ojw.printf(txtLog, "[GRIP] {0} 파지 실패 (거리 {1:F0}mm > 40mm)\r\n", item.Name, dist);
            return false;
        }

        private void Release(CItem item, double dRestZBottom)
        {
            item.Attached = false;
            item.ZBottom = dRestZBottom;   // 안착 (팔레트 위)
        }

        // ================================================================
        // ★ 여기부터가 '학생이 작성하는' 제어 코드 — 강의 API 형식 그대로 ★
        //    CalcInv(x, y, z, 피치) -> 각도 계산
        //    Play(동작시간, 멈춤시간, ID, 각도, ID, 각도, ...) -> 이동
        //    SyncRead() -> 현재 각도 읽기 / CalcXyz() -> 현재 TCP 위치
        // ================================================================
        private void Home()
        {
            m_Robot.Play(1200, 0, 11, 0, 12, -25, 13, 35, 14, 35, 15, 0);
        }

        private void MoveTo(float fX, float fY, float fZ, int nTimeMs)
        {
            float[] a;
            if (!m_Robot.CalcInv(fX, fY, fZ, PITCH, out a))
                Ojw.printf(txtLog, "[WARN] IK 미수렴: ({0:F0}, {1:F0}, {2:F0})\r\n", fX, fY, fZ);
            m_Robot.Play(nTimeMs, 0, 11, a[0], 12, a[1], 13, a[2], 14, a[3]);
        }

        private void RunPickPlace(int nCount)
        {
            Ojw.printf(txtLog, "\r\n===== 픽앤플레이스 ({0}개) =====\r\n", nCount);
            Home();
            m_Robot.Play(400, 0, 16, GRIP_OPEN);

            int nOk = 0;
            double dErrSum = 0;
            for (int i = 0; i < nCount && i < m_lstItems.Count; i++)
            {
                CItem item = m_lstItems[i];
                double tx = m_adTargets[i][0], ty = m_adTargets[i][1];
                Ojw.printf(txtLog, "--- {0}: ({1:F0},{2:F0}) -> ({3:F0},{4:F0}) ---\r\n",
                    item.Name, item.X, item.Y, tx, ty);

                MoveTo((float)item.X, (float)item.Y, Z_APPROACH, 1200);   // 1) 제품 위 경유점
                MoveTo((float)item.X, (float)item.Y, Z_GRASP, 1000);      // 2) 내려가기
                bool bGrab = Grab(item);                                  // 3) 파지 판정(부착)
                if (bGrab) m_Robot.Play(400, 0, 16, GRIP_HOLD);           //    파지폭으로 닫기
                MoveTo((float)item.X, (float)item.Y, Z_APPROACH, 1000);   // 4) 들어올리기
                MoveTo((float)tx, (float)ty, Z_APPROACH, 1400);           // 5) 목표 위로 이동
                MoveTo((float)tx, (float)ty, Z_PLACE, 1000);              // 6) 내려놓을 높이
                m_Robot.Play(300, 0, 16, GRIP_OPEN);                      // 7) 손가락 벌리고
                Release(item, 10);                                        //    부착 해제 (팔레트 위 안착)
                MoveTo((float)tx, (float)ty, Z_APPROACH, 800);            // 8) 복귀 경유점

                double dErr = Math.Sqrt((item.X - tx) * (item.X - tx) + (item.Y - ty) * (item.Y - ty));
                bool bOk = bGrab && dErr <= TOL_OK;
                if (bOk) nOk++;
                dErrSum += dErr;
                Ojw.printf(txtLog, "[RESULT] {0} final=({1:F1}, {2:F1}) target=({3:F0}, {4:F0}) err={5:F1}mm -> {6}\r\n",
                    item.Name, item.X, item.Y, tx, ty, dErr, bOk ? "OK" : "FAIL");
            }
            Home();
            Ojw.printf(txtLog, "[SUMMARY] success {0}/{1}, mean err={2:F1}mm\r\n", nOk, nCount, dErrSum / nCount);
        }

        private void RunDrawRect(bool bBezier)
        {
            // 판 테두리 사각형 (판과 동일 크기 80 x 70) — 꼭짓점을 닫힌 도형으로 정의
            var lstCorners = new List<float[]>
            {
                new float[] { BOARD_X - RECT_HX, BOARD_Y - RECT_HY, Z_DRAW },
                new float[] { BOARD_X + RECT_HX, BOARD_Y - RECT_HY, Z_DRAW },
                new float[] { BOARD_X + RECT_HX, BOARD_Y + RECT_HY, Z_DRAW },
                new float[] { BOARD_X - RECT_HX, BOARD_Y + RECT_HY, Z_DRAW },
            };
            lstCorners.Add(lstCorners[0]);   // 첫 점 == 끝 점 -> 닫힌 도형

            // 경로 샘플링 — ojwSimul에서 라이브러리로 옮겨진 궤적 함수 사용 (강의 Part3 궤적)
            //   직선: 모서리에서 멈칫하며 방향 전환 / 베지어: 모서리를 둥글게 안 멈추고 통과
            List<float[]> lstPath = bBezier
                ? Ojw.CMath.SampleCornersBlend(lstCorners, 72)
                : Ojw.CMath.SampleCornersLinear(lstCorners, 72);

            Ojw.printf(txtLog, "\r\n===== 판 테두리 사각형 ({0}) =====\r\n", bBezier ? "베지어 코너" : "직선");
            m_lstTrace.Clear();
            Home();

            // 1) 펜 거치대에서 펜 집기 (펜 굵기만큼만 파지 — 자연스러운 그립)
            m_Robot.Play(400, 0, 16, GRIP_OPEN);
            MoveTo(PEN_X, PEN_Y, Z_APPROACH + 20, 1200);
            MoveTo(PEN_X, PEN_Y, PEN_STAND_H + PEN_TIP, 900);     // 펜 상단을 감싸는 높이
            m_Robot.Play(400, 0, 16, GRIP_PEN);
            m_bPenHeld = true;
            Ojw.printf(txtLog, "[PEN] 펜 파지 완료\r\n");
            MoveTo(PEN_X, PEN_Y, Z_APPROACH + 20, 800);

            // 2) 경로 시작점으로 이동 -> 펜 내리기
            MoveTo(lstPath[0][0], lstPath[0][1], Z_APPROACH, 1200);
            MoveTo(lstPath[0][0], lstPath[0][1], Z_DRAW, 800);
            Ojw.printf(txtLog, "[DRAW] 펜 내림 (판 위)\r\n");
            m_bPenDown = true;

            // 3) 경로 추종 — 웨이포인트마다 IK
            int nQuarter = lstPath.Count / 4;
            for (int i = 1; i < lstPath.Count; i++)
            {
                MoveTo(lstPath[i][0], lstPath[i][1], Z_DRAW, 110);
                if (nQuarter > 0 && i % nQuarter == 0)
                    Ojw.printf(txtLog, "[DRAW] 진행 {0}/{1}\r\n", i, lstPath.Count - 1);
            }
            m_bPenDown = false;

            // 4) 펜 들고 거치대에 반납
            MoveTo(lstPath[0][0], lstPath[0][1], Z_APPROACH, 800);
            MoveTo(PEN_X, PEN_Y, Z_APPROACH + 20, 1200);
            MoveTo(PEN_X, PEN_Y, PEN_STAND_H + PEN_TIP, 900);
            m_Robot.Play(400, 0, 16, GRIP_OPEN);
            m_bPenHeld = false;
            Ojw.printf(txtLog, "[PEN] 펜 반납 완료\r\n");
            MoveTo(PEN_X, PEN_Y, Z_APPROACH + 20, 800);
            Home();

            // 5) 정확도 평가
            if (bBezier)
            {
                Ojw.printf(txtLog, "[SUMMARY] 자취 {0}점 — 모서리를 베지어로 둥글게 통과 (멈춤 없는 연속 궤적)\r\n", m_lstTrace.Count);
            }
            else
            {
                double dMax = 0;
                for (int i = 0; i < 4; i++)
                {
                    double dBest = 1e9;
                    foreach (var p in m_lstTrace)
                    {
                        double d = Math.Sqrt((p[0] - lstCorners[i][0]) * (p[0] - lstCorners[i][0])
                                           + (p[1] - lstCorners[i][1]) * (p[1] - lstCorners[i][1]));
                        if (d < dBest) dBest = d;
                    }
                    if (dBest > dMax) dMax = dBest;
                    Ojw.printf(txtLog, "[RESULT] corner{0} 최근접 자취 오차={1:F1}mm\r\n", i + 1, dBest);
                }
                Ojw.printf(txtLog, "[SUMMARY] 자취 {0}점, 꼭짓점 최대 오차={1:F1}mm\r\n", m_lstTrace.Count, dMax);
            }
        }

        private void ResetScene()
        {
            foreach (var item in m_lstItems)
            {
                item.Attached = false;
                item.X = item.Start[0]; item.Y = item.Start[1]; item.ZBottom = 0;
            }
            m_lstTrace.Clear();
            m_bPenDown = false;
            m_bPenHeld = false;
            Home();
            m_Robot.Play(300, 0, 16, GRIP_OPEN);
            Ojw.printf(txtLog, "[RESET] 초기 상태 복귀\r\n");
        }
    }
}
