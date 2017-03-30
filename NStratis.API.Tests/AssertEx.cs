﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Xunit;

namespace QBitNinja.Tests
{
    public class AssertEx
    {
        //[DebuggerHidden]
        public static void AssertJsonEqual(object expected, object actual)
        {
            Assert.Equal(Serializer.ToString(expected), Serializer.ToString(actual));
        }
        //[DebuggerHidden]
        public static void HttpError(int code, Action act)
        {
            try
            {
                act();
                Assert.False(true, "Should have thrown error " + code);
            }
            catch(HttpRequestException ex)
            {
                Assert.True(ex.Message.Contains(code.ToString(CultureInfo.InvariantCulture)), "expected error " + code + " but got " + ex.Message);
            }
        }

        public static void Eventually(Func<bool> act)
        {
            var cancel = new CancellationTokenSource(20000);
            while(!act())
            {
                cancel.Token.ThrowIfCancellationRequested();
                Thread.Sleep(1);
            }
        }
    }
}
