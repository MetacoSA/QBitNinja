using System;

namespace RapidBase.Models
{
    public class RapidBaseException : Exception
    {
        public RapidBaseException(RapidBaseError error)
            : base(error.Message)
        {
            StatusCode = error.StatusCode;
        }
        public RapidBaseException(int httpCode, string reason)
            : base(reason)
        {
            StatusCode = httpCode;
        }
        public int StatusCode
        {
            get;
            private set;
        }

        public string Location
        {
            get;
            set;
        }
        public RapidBaseError ToError()
        {
            return new RapidBaseError
            {
                StatusCode = StatusCode,
                Message = Message,
                Location = Location
            };
        }
    }
    public class RapidBaseError
    {
        public int StatusCode
        {
            get;
            set;
        }
        public string Message
        {
            get;
            set;
        }
        public string Location
        {
            get;
            set;
        }
    }
}
