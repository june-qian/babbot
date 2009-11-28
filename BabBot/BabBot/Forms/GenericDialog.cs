﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;

namespace BabBot.Forms
{
    public partial class GenericDialog : Form
    {
        // Internal name
        private string _name;
        // Change tracking
        private bool _changed = false;

        protected bool IsChanged
        {
            get { return _changed; }
            set { _changed = value; }
        }

        GenericDialog()
        {
            InitializeComponent();
        }

        #region Public Methods

        public GenericDialog(string name)
            : this()
        {
            _name = name;

        }

        #endregion

        #region Protected Methods

        protected void ShowErrorMessage(string err)
        {
            MessageBox.Show(this, err, "ERROR",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected void ShowInfoMessage(string info)
        {
            MessageBox.Show(this, info, "INFO",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected virtual void GenericDialog_Load(object sender, EventArgs e)
        {
            _changed = false;
        }

        protected void OpenURL(string url)
        {
            Process.Start(GetDefaultBrowserPath(), url);
        }

        #endregion

        #region Private Methods

        private static string GetDefaultBrowserPath()
        {
            // Registry info for default browser
            string key = @"http\shell\open\command";
            RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(key, false);
            // get default browser path
            return ((string)registryKey.GetValue(null, null)).Split('"')[1];
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            // Open local URL in default browswer
            string url = "file:///" + Environment.CurrentDirectory.Replace("\\", "/") +
                "/Doc/index.html" + "#" + _name;

            OpenURL(url);
        }

        private void GenericDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = (IsChanged &&
                (MessageBox.Show(this, "Are you sure you want close and cancel changes ?",
                    "Confirmation", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes));
        }

        #endregion
    }
}