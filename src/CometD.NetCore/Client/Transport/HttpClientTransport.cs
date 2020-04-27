using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

namespace CometD.NetCore.Client.Transport
{
    public abstract class HttpClientTransport : ClientTransport
    {
        private CookieCollection _cookieCollection;
        private WebHeaderCollection _headerCollection;

        protected HttpClientTransport(string name, IDictionary<string, object> options, NameValueCollection headers)
            : base(name, options)
        {
            SetHeaderCollection(new WebHeaderCollection());
            AddHeaders(headers);
        }

        public string Url
        {
            get; set;
        }

        public void SetCookieCollection(CookieCollection cookieCollection)
        {
            _cookieCollection = cookieCollection;
        }

        public void SetHeaderCollection(WebHeaderCollection headerCollection)
        {
            _headerCollection = headerCollection;
        }

        protected internal void AddCookie(Cookie cookie)
        {
            var cookieCollection = _cookieCollection;
            cookieCollection?.Add(cookie);
        }

        protected internal void AddHeaders(NameValueCollection headers)
        {
            var headerCollection = _headerCollection;
            headerCollection?.Add(headers);
        }

        protected CookieCollection GetCookieCollection()
        {
            return _cookieCollection;
        }

        protected WebHeaderCollection GetHeaderCollection()
        {
            return _headerCollection;
        }
    }
}
