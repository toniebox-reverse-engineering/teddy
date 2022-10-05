using System;
using System.Text;
using System.Windows.Forms;
using static TeddyBench.TeddyMain;

namespace TeddyBench
{
    static class Program
    {
        public static TeddyMain MainClass;

        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            

            MainClass = new TeddyMain();
            Application.Run(MainClass);
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MainClass.ReportException("Main Application Thread", e.Exception);
        }
    }
}
