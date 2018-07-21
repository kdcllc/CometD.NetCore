using System;
using System.Collections.Generic;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;
using CometD.NetCore.Common;

namespace CometD.NetCore.Client.Extension
{
    public class TimesyncClientExtension : IExtension
    {
        public int Offset { get; private set; }
        public int Lag { get; private set; }

        public long ServerTime => (DateTime.Now.Ticks - 621355968000000000) / 10000 + Offset;

        public bool Receive(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        public bool ReceiveMeta(IClientSession session, IMutableMessage message)
        {
            var ext = (Dictionary<string, object>)message.GetExt(false);
            if (ext != null)
            {
                var sync = (Dictionary<string, object>)ext["timesync"];
                if (sync != null)
                {
                    var now = (System.DateTime.Now.Ticks - 621355968000000000) / 10000;

                    var tc = ObjectConverter.ToInt64(sync["tc"], 0);
                    var ts = ObjectConverter.ToInt64(sync["ts"], 0);
                    var p = ObjectConverter.ToInt32(sync["p"], 0);
                    // final int a=((Number)sync.get("a")).intValue();

                    var l2 = (int)((now - tc - p) / 2);
                    var o2 = (int)(ts - tc - l2);

                    Lag = Lag == 0 ? l2 : (Lag + l2) / 2;
                    Offset = Offset == 0 ? o2 : (Offset + o2) / 2;
                }
            }

            return true;
        }

        public bool Send(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        public bool SendMeta(IClientSession session, IMutableMessage message)
        {
            var ext = (Dictionary<string, object>)message.GetExt(true);
            var now = (System.DateTime.Now.Ticks - 621355968000000000) / 10000;
            // Changed JSON.Literal to string
            var timesync = "{\"tc\":" + now + ",\"l\":" + Lag + ",\"o\":" + Offset + "}";
            ext["timesync"] = timesync;
            return true;
        }
    }
}
