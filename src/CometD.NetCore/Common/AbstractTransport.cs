using System.Collections.Generic;
using CometD.NetCore.Bayeux;

namespace CometD.NetCore.Common
{
    public class AbstractTransport : ITransport
    {
        protected IDictionary<string, object> _options;
        protected string[] _prefix;

        public AbstractTransport(string name, IDictionary<string, object> options)
        {
            Name = name;
            _options = options ?? new Dictionary<string, object>();
            _prefix = new string[0];
        }

        public string Name { get; private set; }

        public ICollection<string> OptionNames
        {
            get
            {
                var names = new HashSet<string>();
                foreach (var name in _options.Keys)
                {
                    var lastDot = name.LastIndexOf('.');
                    if (lastDot >= 0)
                    {
                        names.Add(name.Substring(lastDot + 1));
                    }
                    else
                    {
                        names.Add(name);
                    }
                }
                return names;
            }
        }

        public string OptionPrefix
        {
            get
            {
                string prefix = null;
                foreach (var segment in _prefix)
                {
                    prefix = prefix == null ? segment : (prefix + "." + segment);
                }

                return prefix;
            }
            set => _prefix = value.Split('.');
        }

        public object GetOption(string name)
        {
            _options.TryGetValue(name, out var value);

            string prefix = null;

            foreach (var segment in _prefix)
            {
                prefix = prefix == null ? segment : (prefix + "." + segment);
                var key = prefix + "." + name;

                if (_options.ContainsKey(key))
                {
                    value = key;
                }
            }

            return value;
        }

        public string GetOption(string option, string dftValue)
        {
            var value = GetOption(option);
            return ObjectConverter.ToString(value, dftValue);
        }

        public long GetOption(string option, long dftValue)
        {
            var value = GetOption(option);
            return ObjectConverter.ToInt64(value, dftValue);
        }

        public int GetOption(string option, int dftValue)
        {
            var value = GetOption(option);
            return ObjectConverter.ToInt32(value, dftValue);
        }

        public bool GetOption(string option, bool dftValue)
        {
            var value = GetOption(option);
            return ObjectConverter.ToBoolean(value, dftValue);
        }

        public void SetOption(string name, object value)
        {
            var prefix = OptionPrefix;
            _options.Add(prefix == null ? name : (prefix + "." + name), value);
        }
    }
}
