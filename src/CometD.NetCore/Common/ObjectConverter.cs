using System;
using System.Collections.Generic;
using CometD.NetCore.Bayeux;

namespace CometD.NetCore.Common
{
    /// <summary>
    /// Converts an object from one object type to another object type.
    /// </summary>
    internal class ObjectConverter
    {
        public static bool ToBoolean(object obj, bool defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try { return Convert.ToBoolean(obj); }
            catch (Exception) { }

            try { return bool.Parse(obj.ToString()); }
            catch (Exception) { }

            return defaultValue;
        }

        public static int ToInt32(object obj, int defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try { return Convert.ToInt32(obj); }
            catch (Exception) { }

            try { return int.Parse(obj.ToString()); }
            catch (Exception) { }

            return defaultValue;
        }

        public static long ToInt64(object obj, long defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try { return Convert.ToInt64(obj); }
            catch (Exception) { }

            try { return long.Parse(obj.ToString()); }
            catch (Exception) { }

            return defaultValue;
        }

        public static IList<IDictionary<string, object>> ToListOfDictionary(IList<IMutableMessage> M)
        {
            IList<IDictionary<string, object>> R = new List<IDictionary<string, object>>();

            foreach (var m in M)
            {
                R.Add(m);
            }
            return R;
        }

        public static IList<IMessage> ToListOfIMessage(IList<IMutableMessage> M)
        {
            var R = new List<IMessage>();
            foreach (var m in M)
            {
                R.Add(m);
            }
            return R;
        }

        public static string ToString(object obj, string defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            try { return obj.ToString(); }
            catch (Exception) { }

            return defaultValue;
        }
    }
}
