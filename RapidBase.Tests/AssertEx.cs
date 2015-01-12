using Xunit;

namespace RapidBase.Tests
{
    public class AssertEx
    {
        public static void AssertJsonEqual(object expected, object actual)
        {
            Assert.Equal(Serializer.ToString(expected), Serializer.ToString(actual));
        }
    }
}
