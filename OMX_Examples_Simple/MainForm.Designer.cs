namespace OmxPythonRunner
{
    partial class MainForm
    {
        /// <summary>필수 디자이너 변수입니다.</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>사용 중인 모든 리소스를 정리합니다.</summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true, 아니면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다.
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요. (폼 디자이너에서 마우스로 수정)
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.pnDisp = new System.Windows.Forms.Panel();
            this.pnSide = new System.Windows.Forms.Panel();
            this.txtPython = new System.Windows.Forms.RichTextBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnRun = new System.Windows.Forms.Button();
            this.btnEx4 = new System.Windows.Forms.Button();
            this.btnEx5 = new System.Windows.Forms.Button();
            this.btnEx6 = new System.Windows.Forms.Button();
            this.btnEx3 = new System.Windows.Forms.Button();
            this.btnEx2 = new System.Windows.Forms.Button();
            this.btnEx1 = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.m_tmrDisp = new System.Windows.Forms.Timer(this.components);
            this.m_tmrPoll = new System.Windows.Forms.Timer(this.components);
            this.pnSide.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnDisp
            // 
            this.pnDisp.BackColor = System.Drawing.Color.Black;
            this.pnDisp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnDisp.Location = new System.Drawing.Point(0, 0);
            this.pnDisp.Name = "pnDisp";
            this.pnDisp.Size = new System.Drawing.Size(980, 860);
            this.pnDisp.TabIndex = 0;
            // 
            // pnSide
            // 
            this.pnSide.Controls.Add(this.txtPython);
            this.pnSide.Controls.Add(this.txtLog);
            this.pnSide.Controls.Add(this.btnReset);
            this.pnSide.Controls.Add(this.btnRun);
            this.pnSide.Controls.Add(this.btnEx6);
            this.pnSide.Controls.Add(this.btnEx5);
            this.pnSide.Controls.Add(this.btnEx4);
            this.pnSide.Controls.Add(this.btnEx3);
            this.pnSide.Controls.Add(this.btnEx2);
            this.pnSide.Controls.Add(this.btnEx1);
            this.pnSide.Controls.Add(this.lblTitle);
            this.pnSide.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnSide.Location = new System.Drawing.Point(980, 0);
            this.pnSide.Name = "pnSide";
            this.pnSide.Padding = new System.Windows.Forms.Padding(8);
            this.pnSide.Size = new System.Drawing.Size(470, 860);
            this.pnSide.TabIndex = 1;
            // 
            // txtPython
            // 
            this.txtPython.AcceptsTab = true;
            this.txtPython.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(18)))), ((int)(((byte)(22)))), ((int)(((byte)(28)))));
            this.txtPython.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtPython.Font = new System.Drawing.Font("Consolas", 10F);
            this.txtPython.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(226)))), ((int)(((byte)(235)))));
            this.txtPython.Location = new System.Drawing.Point(8, 282);
            this.txtPython.Name = "txtPython";
            this.txtPython.Size = new System.Drawing.Size(454, 380);
            this.txtPython.TabIndex = 0;
            this.txtPython.Text = "";
            this.txtPython.WordWrap = false;
            // 
            // txtLog
            // 
            this.txtLog.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(24)))), ((int)(((byte)(28)))));
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtLog.ForeColor = System.Drawing.Color.Gainsboro;
            this.txtLog.Location = new System.Drawing.Point(8, 662);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtLog.Size = new System.Drawing.Size(454, 190);
            this.txtLog.TabIndex = 1;
            this.txtLog.WordWrap = false;
            // 
            // btnReset
            // 
            this.btnReset.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(120)))), ((int)(((byte)(130)))));
            this.btnReset.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnReset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReset.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnReset.ForeColor = System.Drawing.Color.White;
            this.btnReset.Location = new System.Drawing.Point(8, 244);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(454, 38);
            this.btnReset.TabIndex = 2;
            this.btnReset.Text = "리셋 (초기 상태)";
            this.btnReset.UseVisualStyleBackColor = false;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // btnRun
            // 
            this.btnRun.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(140)))), ((int)(((byte)(80)))));
            this.btnRun.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnRun.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRun.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnRun.ForeColor = System.Drawing.Color.White;
            this.btnRun.Location = new System.Drawing.Point(8, 206);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(454, 38);
            this.btnRun.TabIndex = 3;
            this.btnRun.Text = "▶ Run (파이썬 실행)";
            this.btnRun.UseVisualStyleBackColor = false;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
            //
            // btnEx6
            //
            this.btnEx6.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(130)))), ((int)(((byte)(180)))));
            this.btnEx6.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnEx6.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEx6.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnEx6.ForeColor = System.Drawing.Color.White;
            this.btnEx6.Location = new System.Drawing.Point(8, 206);
            this.btnEx6.Name = "btnEx6";
            this.btnEx6.Size = new System.Drawing.Size(454, 38);
            this.btnEx6.TabIndex = 10;
            this.btnEx6.Text = "예제 6: 팔레타이징 2 (아치·경유 설정)";
            this.btnEx6.UseVisualStyleBackColor = false;
            this.btnEx6.Click += new System.EventHandler(this.btnEx6_Click);
            //
            // btnEx5
            //
            this.btnEx5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(130)))), ((int)(((byte)(180)))));
            this.btnEx5.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnEx5.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEx5.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnEx5.ForeColor = System.Drawing.Color.White;
            this.btnEx5.Location = new System.Drawing.Point(8, 206);
            this.btnEx5.Name = "btnEx5";
            this.btnEx5.Size = new System.Drawing.Size(454, 38);
            this.btnEx5.TabIndex = 9;
            this.btnEx5.Text = "예제 5: 사각형 (베지어 실시간 계산)";
            this.btnEx5.UseVisualStyleBackColor = false;
            this.btnEx5.Click += new System.EventHandler(this.btnEx5_Click);
            // 
            // btnEx4
            // 
            this.btnEx4.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(130)))), ((int)(((byte)(180)))));
            this.btnEx4.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnEx4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEx4.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnEx4.ForeColor = System.Drawing.Color.White;
            this.btnEx4.Location = new System.Drawing.Point(8, 168);
            this.btnEx4.Name = "btnEx4";
            this.btnEx4.Size = new System.Drawing.Size(454, 38);
            this.btnEx4.TabIndex = 4;
            this.btnEx4.Text = "예제 4: 사각형 (베지어 코너)";
            this.btnEx4.UseVisualStyleBackColor = false;
            this.btnEx4.Click += new System.EventHandler(this.btnEx4_Click);
            // 
            // btnEx3
            // 
            this.btnEx3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(130)))), ((int)(((byte)(180)))));
            this.btnEx3.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnEx3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEx3.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnEx3.ForeColor = System.Drawing.Color.White;
            this.btnEx3.Location = new System.Drawing.Point(8, 130);
            this.btnEx3.Name = "btnEx3";
            this.btnEx3.Size = new System.Drawing.Size(454, 38);
            this.btnEx3.TabIndex = 5;
            this.btnEx3.Text = "예제 3: 사각형 (직선)";
            this.btnEx3.UseVisualStyleBackColor = false;
            this.btnEx3.Click += new System.EventHandler(this.btnEx3_Click);
            // 
            // btnEx2
            // 
            this.btnEx2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(130)))), ((int)(((byte)(180)))));
            this.btnEx2.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnEx2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEx2.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnEx2.ForeColor = System.Drawing.Color.White;
            this.btnEx2.Location = new System.Drawing.Point(8, 92);
            this.btnEx2.Name = "btnEx2";
            this.btnEx2.Size = new System.Drawing.Size(454, 38);
            this.btnEx2.TabIndex = 6;
            this.btnEx2.Text = "예제 2: 팔레타이징 (3개)";
            this.btnEx2.UseVisualStyleBackColor = false;
            this.btnEx2.Click += new System.EventHandler(this.btnEx2_Click);
            // 
            // btnEx1
            // 
            this.btnEx1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(130)))), ((int)(((byte)(180)))));
            this.btnEx1.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnEx1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEx1.Font = new System.Drawing.Font("맑은 고딕", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnEx1.ForeColor = System.Drawing.Color.White;
            this.btnEx1.Location = new System.Drawing.Point(8, 54);
            this.btnEx1.Name = "btnEx1";
            this.btnEx1.Size = new System.Drawing.Size(454, 38);
            this.btnEx1.TabIndex = 7;
            this.btnEx1.Text = "예제 1: 픽앤플레이스";
            this.btnEx1.UseVisualStyleBackColor = false;
            this.btnEx1.Click += new System.EventHandler(this.btnEx1_Click);
            // 
            // lblTitle
            // 
            this.lblTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTitle.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(8, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(454, 46);
            this.lblTitle.TabIndex = 8;
            this.lblTitle.Text = "예제 버튼 = 파이썬 파일 로드 (파일 하나 = 프로젝트 하나)\r\nRun = Ojw.CPython 실행 (코드 수정 가능)";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // m_tmrDisp
            // 
            this.m_tmrDisp.Interval = 20;
            this.m_tmrDisp.Tick += new System.EventHandler(this.tmrDisp_Tick);
            // 
            // m_tmrPoll
            // 
            this.m_tmrPoll.Interval = 50;
            this.m_tmrPoll.Tick += new System.EventHandler(this.tmrPoll_Tick);
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1450, 860);
            this.Controls.Add(this.pnDisp);
            this.Controls.Add(this.pnSide);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OMX Examples Simple — OpenJigWare 3D (한 줄 한 명령)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.pnSide.ResumeLayout(false);
            this.pnSide.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnDisp;
        private System.Windows.Forms.Panel pnSide;
        private System.Windows.Forms.RichTextBox txtPython;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnEx4;
        private System.Windows.Forms.Button btnEx5;
        private System.Windows.Forms.Button btnEx6;
        private System.Windows.Forms.Button btnEx3;
        private System.Windows.Forms.Button btnEx2;
        private System.Windows.Forms.Button btnEx1;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Timer m_tmrDisp;
        private System.Windows.Forms.Timer m_tmrPoll;
    }
}
