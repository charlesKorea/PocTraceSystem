using System;
using System.IO;
using System.Windows.Forms;

namespace PocTrace.DdwTraceBench
{
    static class Program
    {
        [STAThread]
        static int Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DdwTraceBench-startup.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + ex + "\r\n");
                }
                catch
                {
                }
                MessageBox.Show(ex.ToString(), "PocTrace.DdwTraceBench", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
