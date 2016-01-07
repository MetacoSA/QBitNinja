using System;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class QBitNinjaException : Exception
    {
        public QBitNinjaException(QBitNinjaError error)
            : base(error.Message)
        {
            StatusCode = error.StatusCode;
        }
        public QBitNinjaException(int httpCode, string reason)
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
        public QBitNinjaError ToError()
        {
            return new QBitNinjaError
            {
                StatusCode = StatusCode,
                Message = Message,
                Location = Location
            };
        }
    }
    public class QBitNinjaError
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
