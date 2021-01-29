using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AgOpenGPS
{
    internal static class Program
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
        
        [STAThread]
        private static void Main()
        {
            bool trystatus = false;
            Mutex mutex = new Mutex(false, "{8F6F0AC4-B9A1-55fd-A8CF-72F04E6BDE8F}");
            try
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    //ungroup in taskbar
                    string AppID = "AgOpenGPS";
                    SetCurrentProcessExplicitAppUserModelID(AppID);

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new FormGPS());
                    mutex.ReleaseMutex();
                }
                else
                    trystatus = true;
            }
            catch (AbandonedMutexException)
            {
                trystatus = true;
            }
            if (trystatus && false)
            {
                Mutex mutex2 = new Mutex(false, "{8F6F0AC4-B9A1-55fd-A8CF-72F04E6BDE8G}");
                if (mutex2.WaitOne(TimeSpan.Zero))
                {

                    //ungroup in taskbar
                    string AppID = "AgIO";
                    SetCurrentProcessExplicitAppUserModelID(AppID);

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new FormLoop());
                    mutex2.ReleaseMutex();
                }
            }
        }
    }
}