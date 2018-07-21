using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

namespace CometD.NetCore.Client.Transport
{
    public abstract class HttpClientTransport : ClientTransport
    {
        private CookieCollection cookieCollection;
        private WebHeaderCollection headerCollection;

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
            this.cookieCollection = cookieCollection;
        }

        public void SetHeaderCollection(WebHeaderCollection headerCollection)
        {
            this.headerCollection = headerCollection;
        }

        protected internal void AddCookie(Cookie cookie)
        {
            var cookieCollection = this.cookieCollection;
            if (cookieCollection != null)
            {
                cookieCollection.Add(cookie);
            }
        }

        protected internal void AddHeaders(NameValueCollection headers)
        {
            var headerCollection = this.headerCollection;
            if (headerCollection != null)
            {
                headerCollection.Add(headers);
            }
        }

        protected CookieCollection GetCookieCollection()
        {
            return cookieCollection;
        }

        protected WebHeaderCollection GetHeaderCollection()
        {
            return headerCollection;
        }
    }
}
