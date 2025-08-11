using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace BasicTodo
{
    static class Program
    {
        private static Mutex _mutex;

        public static readonly int WM_SHOWME =
            NativeMethods.RegisterWindowMessage("BasicTodo_ShowMe_{A6EE8CB4-BCF3-4971-A7DB-6D6E4F985729}");
            
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, @"Global\BasicTodo_{A6EE8CB4-BCF3-4971-A7DB-6D6E4F985729}", out createdNew);
            if (!createdNew)
            {
                NativeMethods.PostMessage((IntPtr)0xFFFF, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            _mutex.ReleaseMutex();
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = false)]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = false)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = false)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string lpString);
    }
}
