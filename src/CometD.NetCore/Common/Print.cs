using System.Collections.Generic;

using CometD.NetCore.Bayeux;

namespace CometD.NetCore.Common
{
    internal sealed class Print
    {
        public static string Dictionary(IDictionary<string, object> dictionary)
        {
            if (dictionary == null)
            {
                return " (null)";
            }

            if (!(dictionary is IDictionary<string, object>))
            {
                return " (invalid)";
            }

            var s = string.Empty;

            foreach (var kvp in dictionary)
            {
                s += " '" + kvp.Key + ":";
                if (kvp.Value is IDictionary<string, object>)
                {
                    s += Dictionary(kvp.Value as IDictionary<string, object>);
                }
                else
                {
                    s += kvp.Value.ToString();
                }

                s += "'";
            }

            return s;
        }

        public static string List(IList<string> L)
        {
            var s = string.Empty;
            foreach (var e in L)
            {
                s += " '" + e + "'";
            }

            return s;
        }

        public static string Messages(IList<IMessage> messageList)
        {
            if (messageList == null)
            {
                return " (null)";
            }

            if (!(messageList is IList<IMessage>))
            {
                return " (invalid)";
            }

            var s = "[";

            foreach (var m in messageList)
            {
                s += " " + m;
            }

            s += " ]";

            return s;
        }

        public static string Messages(IList<IMutableMessage> messageList)
        {
            if (messageList == null)
            {
                return " (null)";
            }

            if (!(messageList is IList<IMutableMessage>))
            {
                return " (invalid)";
            }

            var s = "[";

            foreach (var m in messageList)
            {
                s += " " + m;
            }

            s += " ]";
            return s;
        }
    }
}
