using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Filters;
using Microsoft.WindowsAzure.Storage;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using QBitNinja.Models;

namespace QBitNinja
{
    public class GlobalExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            if (actionExecutedContext.Exception is FormatException)
            {
                actionExecutedContext.Exception = new QBitNinjaException(400, actionExecutedContext.Exception.Message);
            }

            if (actionExecutedContext.Exception is JsonObjectException jsonEx)
            {
                actionExecutedContext.Exception = new QBitNinjaException(400, actionExecutedContext.Exception.Message)
                {
                    Location = jsonEx.Path
                };
            }

            if (actionExecutedContext.Exception is JsonReaderException jsonReaderException)
            {
                actionExecutedContext.Exception = new QBitNinjaException(400, actionExecutedContext.Exception.Message)
                {
                    Location = jsonReaderException.Path
                };
            }

            if (actionExecutedContext.Exception is QBitNinjaException rapidEx)
            {
                actionExecutedContext.Exception = new HttpResponseException(new HttpResponseMessage
                {
                    StatusCode = (HttpStatusCode)rapidEx.StatusCode,
                    ReasonPhrase = rapidEx.Message + (rapidEx.Location == null ? "" : " at " + rapidEx.Location),
                    Content = new ObjectContent<QBitNinjaError>(rapidEx.ToError(), actionExecutedContext.ActionContext.ControllerContext.Configuration.Formatters.JsonFormatter, "application/json")
                });
            }

            if (actionExecutedContext.Exception is StorageException storageEx)
            {
                if (storageEx.RequestInformation != null
                    && storageEx.RequestInformation.HttpStatusCode == 404)
                {
                    actionExecutedContext.Exception = new HttpResponseException(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.NotFound
                    });
                }
            }

            base.OnException(actionExecutedContext);
        }
    }
}
