using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using Xunit;

namespace RapidBase.Tests
{
    class AssetEx
    {
        [DebuggerHidden]
        public static void HttpError(int code, Action act)
        {
            try
            {
                act();
                Assert.False(true, "Should have thrown error " + code);
            }
            catch (HttpRequestException ex)
            {
                Assert.True(ex.Message.Contains(code.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}
