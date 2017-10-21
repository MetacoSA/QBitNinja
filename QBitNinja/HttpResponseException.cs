using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace QBitNinja
{
    class HttpResponseException : Exception
    {
		public HttpResponseException(HttpResponseMessage responseMessage)
		{
			ResponseMessage = responseMessage;
		}

		public HttpResponseMessage ResponseMessage
		{
			get; set;
		}
	}
}
