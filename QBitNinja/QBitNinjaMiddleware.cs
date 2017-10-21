using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using QBitNinja.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja
{
	public class QBitNinjaMiddleware
	{
		Network _Network;
		Encoding UTF8NoBOM;
		public QBitNinjaMiddleware(Network network)
		{
			_Network = network;
			UTF8NoBOM = new UTF8Encoding(false);
		}

		public async Task InvokeAsync(HttpContext context, RequestDelegate next)
		{
			Exception ex = null;
			try
			{
				await next(context);
			}
			catch(Exception innerEx)
			{
				ex = innerEx;
			}

			if(ex is FormatException)
			{
				ex = new QBitNinjaException(400, ex.Message);
			}
			if(ex is JsonObjectException)
			{
				ex = new QBitNinjaException(400, ex.Message)
				{
					Location = ((JsonObjectException)ex).Path
				};
			}
			if(ex is JsonReaderException)
			{
				ex = new QBitNinjaException(400, ex.Message)
				{
					Location = ((JsonReaderException)ex).Path
				};
			}
			if(ex is QBitNinjaException qbitEx)
			{
				ex = new HttpResponseException(new HttpResponseMessage()
				{
					StatusCode = (HttpStatusCode)qbitEx.StatusCode,
					ReasonPhrase = qbitEx.Message + (qbitEx.Location == null ? "" : " at " + qbitEx.Location),
					Content = new StringContent(Serializer.ToString(qbitEx.ToError(), _Network), UTF8NoBOM, "application/json")
				});
			}
			if(ex is StorageException storageEx)
			{
				if(storageEx.RequestInformation?.HttpStatusCode == 404)
					ex = new HttpResponseException(new HttpResponseMessage()
					{
						StatusCode = HttpStatusCode.NotFound
					});
			}

			if(ex is HttpResponseException httpEx)
			{
				context.Response.StatusCode = (int)httpEx.ResponseMessage.StatusCode;
				if(httpEx.ResponseMessage.Content != null)
				{
					await httpEx.ResponseMessage.Content.CopyToAsync(context.Response.Body);
					context.Response.ContentLength = httpEx.ResponseMessage.Content.Headers.ContentLength;
					context.Response.ContentType = httpEx.ResponseMessage.Content.Headers.ContentType.ToString();
				}
				ex = null;
			}
		}
	}
}
