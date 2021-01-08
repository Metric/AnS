using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace AnS.UI
{
    public class SystemTrayIcon
    {
        public delegate void NotifEvent();
        public event NotifEvent OnShow;
        public event NotifEvent OnExit;

        private NotifyIcon icon;


        public bool IsActive { get; protected set; }

        public SystemTrayIcon()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IsActive = true;
                icon = new NotifyIcon();

                var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
                
                icon.Icon = new System.Drawing.Icon(embeddedProvider.GetFileInfo("Icons\\ansicon.ico").CreateReadStream(), new System.Drawing.Size(16, 16));
                icon.Visible = true;
                icon.ContextMenuStrip = new ContextMenuStrip();
                icon.ContextMenuStrip.Items.Add("Show");
                icon.ContextMenuStrip.Items.Add("Exit");
                icon.ContextMenuStrip.Items[0].Click += Show_Click;
                icon.ContextMenuStrip.Items[1].Click += Exit_Click;
                icon.DoubleClick += Icon_DoubleClick;
                icon.Text = "AnS";
                icon.Disposed += Icon_Disposed;
            }
            else
            {
                IsActive = false;
            }
        }

        private void Icon_Disposed(object sender, EventArgs e)
        {
            IsActive = false;
        }

        private void Icon_DoubleClick(object sender, EventArgs e)
        {
            OnShow?.Invoke();
        }

        public void ShowInfo(string msg)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                icon.BalloonTipText = msg;
                icon.BalloonTipIcon = ToolTipIcon.None;
                icon.ShowBalloonTip(2000);
            }
        }

        private void Exit_Click(object sender, EventArgs e)
        {
            OnExit?.Invoke();
        }

        private void Show_Click(object sender, EventArgs e)
        {
            OnShow?.Invoke();   
        }
    }
}
