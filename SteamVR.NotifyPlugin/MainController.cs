using BOLL7708;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using SteamVR.NotifyPlugin.Notification;

namespace SteamVR.NotifyPlugin
{
    class MainController
    {
        public static Dispatcher UiDispatcher { get; private set; }
        private readonly EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private Action<bool> _pipeStatusAction;
        private bool _openVRConnected = false;
        private bool _shouldShutDown = false;
        private MainWindow _mainWindow;

        private NamedPipeClientStream _pipeClient;

        public MainController(Action<bool> pipeStatusAction, MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            UiDispatcher = Dispatcher.CurrentDispatcher;
            _pipeStatusAction = pipeStatusAction;
            var notificationsThread = new Thread(Worker);
            if (!notificationsThread.IsAlive) notificationsThread.Start();
            _pipeClient = new NamedPipeClientStream(".", "HomeAssistantAgentPipe", PipeDirection.InOut,
                PipeOptions.Asynchronous);
            StartListeningToPipe();
        }

        private async void StartListeningToPipe()
        {
            while (true)
            {
                try
                {
                    if (!_pipeClient.IsConnected)
                    {
                        await _pipeClient.ConnectAsync();
                        _pipeStatusAction.Invoke(true);
                        Debug.WriteLine("Connected to pipe.");
                    }

                    byte[] buffer = new byte[1024 * 1024];
                    int bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        PayloadWithSessionId payloadWithSessionId =
                            JsonConvert.DeserializeObject<PayloadWithSessionId>(message);
                        if (payloadWithSessionId.type == "exit")
                        {
                            Shutdown();
                            return;
                        }

                        PostImageNotification(payloadWithSessionId.SessionId, payloadWithSessionId);
                    }
                    else if (_pipeClient.IsConnected == false)
                    {
                        _pipeStatusAction.Invoke(false);
                        Debug.WriteLine("Pipe disconnected.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error connecting to pipe: {ex.Message}");
                    await Task.Delay(5000); // Wait for 5 seconds before trying to reconnect
                }
            }
        }

        void SendResponseToPipe(ResponseWithSessionId responseWithSessionId)
        {
            try
            {
                var response = JsonConvert.SerializeObject(responseWithSessionId);
                var buffer = Encoding.UTF8.GetBytes(response);
                _pipeClient.Write(buffer, 0, buffer.Length);
                _pipeClient.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending response to pipe: {ex.Message}");
            }
        }

        #region openvr

        private void Worker()
        {
            var initComplete = false;

            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if (_openVRConnected)
                {
                    if (!initComplete)
                    {
                        initComplete = true;
                        _vr.AddApplicationManifest("./notify_plugin.vrmanifest", "antek.steamvr_ha_agent_notify_plugin");
                    }
                    else
                    {
                        _vr.UpdateEvents(false);
                    }

                    Thread.Sleep(500);
                }
                else
                {
                    if (!_openVRConnected)
                    {
                        Debug.WriteLine("Initializing OpenVR...");
                        _openVRConnected = _vr.Init();
                    }

                    Thread.Sleep(2000);
                }

                if (_shouldShutDown)
                {
                    _shouldShutDown = false;
                    initComplete = false;
                    foreach (var overlay in Session.Overlays.Values) overlay.Deinit();
                    try
                    {
                        _vr.AcknowledgeShutdown();
                        Thread.Sleep(500); // Allow things to deinit properly
                        _vr.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error shutting down OpenVR: {ex.Message}");
                    }
                    // access main window and dispose notify icon
                    _mainWindow.Shutdown();
                    return;
                }
            }
        }

        private void PostImageNotification(string sessionId, Payload payload)
        {
            var channel = payload.customProperties.overlayChannel;
            Debug.WriteLine($"Posting image texture notification to channel {channel}!");
            Overlay overlay;
            if (!Session.Overlays.ContainsKey(channel))
            {
                overlay = new Overlay($"SteamVRHAClient[{channel}]", channel);
                if (overlay != null && overlay.IsInitialized())
                {
                    overlay.DoneEvent += (s, args) => { OnOverlayDoneEvent(args); };
                    Session.Overlays.TryAdd(channel, overlay);
                }
            }
            else overlay = Session.Overlays[channel];

            if (overlay != null && overlay.IsInitialized()) overlay.EnqueueNotification(sessionId, payload);
        }

        private void OnOverlayDoneEvent(string[] args)
        {
            if (args.Length == 3)
            {
                var sessionId = args[0];
                var nonce = args[1];
                var error = args[2];
                ResponseWithSessionId response =
                    new ResponseWithSessionId(sessionId, new Response(nonce, error == "", error));
                SendResponseToPipe(response);
            }
        }

        #endregion


        public void Shutdown()
        {
            _pipeClient.Close();
            _shouldShutDown = true;
        }
    }
}