using System;
using System.Threading;
using System.Windows.Forms;

namespace ClashXW
{
    internal static class Program
    {
        private const string MutexName = "ClashXW_SingleInstance_Mutex";
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running
                return;
            }

            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }
    }
}
