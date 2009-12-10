﻿/*
    This file is part of BabBot.

    BabBot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    BabBot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with BabBot.  If not, see <http://www.gnu.org/licenses/>.
  
    Copyright 2009 BabBot Team -
*/
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using BabBot.Forms;
using BabBot.Manager;

namespace BabBot
{
    internal static class Program
    {
        // List of required subdirectories under installation directory that
        // doesn't depend of config parameters
        private static string[] dirs = new string[] { "Data\\Export", "Data\\Import" };

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                string LuaInjectPath = Path.Combine(Application.StartupPath, "Dante.dll");
                string BSPath = Path.Combine(Application.StartupPath, "B@S.Reversing.dll");

                EasyHook.Config.Register(
                    "Dante",
                    LuaInjectPath);
               
                /*
                EasyHook.Config.Register(
                    "B@S.Reversing",
                    BSPath);
                */
            }
            catch (ApplicationException)
            {
                MessageBox.Show("Access denied !!!" + Environment.NewLine +
                    "You don't have administrative right to run this application.", 
                    "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Process.GetCurrentProcess().Kill();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check and parse command line parameters before start MainForm
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    if (arg.Equals("-a"))
                    {
                        // Set auto-mode
                        // TODO automode required profile name
                        ProcessManager.SetAutoRun();
                    } else if (arg.StartsWith("-p")) {
                        // Read profile file
                        int idx = arg.IndexOf("=");
                        if (idx < 0)
                            MessageBox.Show("Parameter name missing");
                        // Keep as usual
                        else
                        {
                            string fname = arg.Substring(idx + 1);
                            // TODO use profile
                        }
                    }
                }
            }

            // Check for required sub-directories
            foreach (string s in dirs)
                if (!Directory.Exists(s))
                    Directory.CreateDirectory(s);

            var mainForm = new MainForm();
            Application.ThreadException += mainForm.UnhandledThreadExceptionHandler;
            try
            {
#if !DEBUG
                //Common.AppHelper.StartHideProcess(); // comment this line if you have problem
#endif
                Application.Run(mainForm);
#if !DEBUG
                //Common.AppHelper.StopHideProcess(); // comment this line if you have problem
#endif
            }
            catch (Exception ex)
            {
                var form = new ExceptionForm(ex);
                DialogResult result = form.ShowDialog();
                Application.Exit();
            }
        }
    }
}