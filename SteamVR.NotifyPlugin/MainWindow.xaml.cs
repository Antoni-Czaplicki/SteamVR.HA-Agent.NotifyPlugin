using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace SteamVR.NotifyPlugin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainController _controller;
        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private readonly GraphicsSingleton _graphics = GraphicsSingleton.Instance;
        private static Mutex _mutex = null; // Used to detect other instances of the same application

        public MainWindow()
        {
            InitializeComponent();

            // Prevent multiple instances
            _mutex = new Mutex(true, Properties.Resources.AppName, out bool createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    System.Windows.Application.Current.MainWindow,
                    "This application is already running!",
                    Properties.Resources.AppName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                System.Windows.Application.Current.Shutdown();
                return;
            }

            // Tray icon
            var icon = Properties.Resources.Icon.Clone() as System.Drawing.Icon;
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.MouseClick += NotifyIcon_Click;
            // implement a context menu
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (sender, args) =>
            {
                if (_controller != null)
                {
                    _controller.Shutdown();
                }
                else
                {
                    Shutdown();
                }
            });
            _notifyIcon.ContextMenuStrip.Items.Add("Open Agent", null,
                (sender, args) => { Process.Start("ha-vr-agent://"); });

            _notifyIcon.Text = "Home Assistant Agent for SteamVR: Notify Plugin\nNot connected";
            _notifyIcon.Icon = icon;
            _notifyIcon.Visible = true;


            GraphicsCompanion.StartOpenTK(this);
            Loaded += (sender, args) =>
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
            };

            _controller = new MainController(
                (pipeStatus) => { _notifyIcon.Text = pipeStatus ? "Connected to Agent" : "Not connected to Agent"; },
                this
            );
        }

        public void Shutdown()
        {
            Dispatcher.Invoke(Close);
        }

        private void NotifyIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) return;
            Process.Start("ha-vr-agent://");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_notifyIcon != null) _notifyIcon.Dispose();
        }

        private void OpenTKControl_OnRender(TimeSpan delta)
        {
            _graphics.OnRender(delta);
        }

        private void OpenTKControl_OnReady()
        {
            _graphics.OnReady();
        }
    }
}