using Microsoft.WindowsAzure.Storage;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Filters;

namespace RapidBase
{
    public class GlobalExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            if (actionExecutedContext.Exception is FormatException)
            {
                actionExecutedContext.Exception =  new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    ReasonPhrase = actionExecutedContext.Exception.Message
                });
            }
            if (actionExecutedContext.Exception is StorageException)
            {
                var storageEx = actionExecutedContext.Exception as StorageException;
                if(storageEx.RequestInformation != null && storageEx.RequestInformation.HttpStatusCode == 404)
                    actionExecutedContext.Exception = new HttpResponseException(new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.NotFound
                    });
            }
            base.OnException(actionExecutedContext);
        }

        public override bool Match(object obj)
        {
            return base.Match(obj);
        }
        public override Task OnExceptionAsync(HttpActionExecutedContext actionExecutedContext, System.Threading.CancellationToken cancellationToken)
        {
            return base.OnExceptionAsync(actionExecutedContext, cancellationToken);
        }
    }
}
