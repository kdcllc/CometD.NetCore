using System;
using System.Collections.Generic;

using CometD.NetCore.Bayeux;

namespace CometD.NetCore.Common
{
    /// <summary>
    /// Converts an object from one object type to another object type.
    /// </summary>
    internal sealed class ObjectConverter
    {
        public static bool ToBoolean(object obj, bool defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToBoolean(obj);
            }
            catch
            {
            }

            try
            {
                return bool.Parse(obj.ToString());
            }
            catch
            {
            }

            return defaultValue;
        }

        public static int ToInt32(object obj, int defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToInt32(obj);
            }
            catch
            {
            }

            try
            {
                return int.Parse(obj.ToString());
            }
            catch
            {
            }

            return defaultValue;
        }

        public static long ToInt64(object obj, long defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToInt64(obj);
            }
            catch
            {
            }

            try
            {
                return long.Parse(obj.ToString());
            }
            catch
            {
            }

            return defaultValue;
        }

        public static IList<IDictionary<string, object>> ToListOfDictionary(IList<IMutableMessage> M)
        {
            IList<IDictionary<string, object>> r = new List<IDictionary<string, object>>();

            foreach (var m in M)
            {
                r.Add(m);
            }

            return r;
        }

        public static IList<IMessage> ToListOfIMessage(IList<IMutableMessage> M)
        {
            var r = new List<IMessage>();
            foreach (var m in M)
            {
                r.Add(m);
            }

            return r;
        }

        public static string ToString(object obj, string defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try
            {
                return obj.ToString();
            }
            catch
            {
            }

            return defaultValue;
        }
    }
}
