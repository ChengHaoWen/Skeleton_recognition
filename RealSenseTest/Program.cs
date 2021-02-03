using System;
using System.Windows.Forms;
using nuitrack;
using Exception = System.Exception;

namespace RealSenseTest
{
    static class Program
    {
        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        [STAThread]
        static public void Main()
        {
            Console.CancelKeyPress += delegate {
                Nuitrack.Release();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                // something not used
            };
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
    }
}
