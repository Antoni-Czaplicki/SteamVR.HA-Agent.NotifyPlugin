using System.Collections.Concurrent;
using SteamVR.NotifyPlugin.Notification;

namespace SteamVR.NotifyPlugin
{
    class Session
    {
        public readonly static ConcurrentDictionary<int, Overlay> Overlays = new ConcurrentDictionary<int, Overlay>();
    }
}
