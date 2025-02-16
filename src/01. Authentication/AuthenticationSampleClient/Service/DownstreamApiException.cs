using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient
{
    [Serializable]
    public sealed class DownstreamApiException : ApplicationException
    {
        public HttpMethod Method { get; }

        public Uri Uri { get; }

        public HttpStatusCode StatusCode { get; }

        public byte[] RawContent { get; }

        public DownstreamApiException(HttpMethod method, Uri uri, HttpStatusCode statusCode, byte[] rawContent)
            : base($"{method} {uri} -> {statusCode:D}")
        {
            Method = method;
            Uri = uri;
            StatusCode = statusCode;
            RawContent = rawContent;
        }

        private DownstreamApiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Method = new HttpMethod(info.GetString("Method"));
            Uri = (Uri)info.GetValue("Uri", typeof(Uri));
            StatusCode = (HttpStatusCode)info.GetInt32("StatusCode");
            RawContent = (byte[])info.GetValue("RawContent", typeof(byte[]));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Method", Method.Method);
            info.AddValue("Uri", Uri);
            info.AddValue("StatusCode", (int)StatusCode);
            info.AddValue("RawContent", RawContent);
        }
    }
}
