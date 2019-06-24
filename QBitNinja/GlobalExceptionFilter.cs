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
            switch (actionExecutedContext.Exception)
            {
                case FormatException _:
                    actionExecutedContext.Exception = new QBitNinjaException(400, actionExecutedContext.Exception.Message);
                    break;
                case JsonObjectException _:
                    actionExecutedContext.Exception = new QBitNinjaException(400, actionExecutedContext.Exception.Message)
                    {
                        Location = ((JsonObjectException)actionExecutedContext.Exception).Path
                    };
                    break;
                case JsonReaderException _:
                    actionExecutedContext.Exception = new QBitNinjaException(400, actionExecutedContext.Exception.Message)
                    {
                        Location = ((JsonReaderException)actionExecutedContext.Exception).Path
                    };
                    break;
                case QBitNinjaException _:
                    QBitNinjaException rapidEx = actionExecutedContext.Exception as QBitNinjaException;
                    actionExecutedContext.Exception = new HttpResponseException(new HttpResponseMessage
                    {
                        StatusCode = (HttpStatusCode)rapidEx.StatusCode,
                        ReasonPhrase = rapidEx.Message + (rapidEx.Location == null ? "" : " at " + rapidEx.Location),
                        Content = new ObjectContent<QBitNinjaError>(rapidEx.ToError(), actionExecutedContext.ActionContext.ControllerContext.Configuration.Formatters.JsonFormatter, "application/json")
                    });
                    break;
                case StorageException _:
                    StorageException storageEx = actionExecutedContext.Exception as StorageException;
                    if (storageEx?.RequestInformation != null && storageEx.RequestInformation.HttpStatusCode == 404)
                    {
                        actionExecutedContext.Exception = new HttpResponseException(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.NotFound
                        });
                    }

                    break;
            }

            base.OnException(actionExecutedContext);
        }
    }
}
