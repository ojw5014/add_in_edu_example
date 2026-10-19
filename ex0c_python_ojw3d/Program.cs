using System;
using System.Windows.Forms;

namespace OmxPythonRunner
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // [2026-09-29] 첫 인자 = 시작할 때 편집창에 불러올 파이썬 파일 (강의 슬라이드 버튼용)
            string strScript = null, strDh = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--dh" && i + 1 < args.Length) { strDh = args[++i]; continue; }
                if (strScript == null) strScript = args[i];
            }
            Application.Run(new MainForm(strScript, strDh));
        }
    }
}
