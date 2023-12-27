using MILLEC;

namespace Tests.Data
{
    internal static class MILLECTestHelpers
    {
        public static MILLEC<int> New(int itemCount, int capacity)
        {
            var millec = new MILLEC<int>(capacity);
            for (int i = 0; i < itemCount; i++)
                millec.Add(i);

            return millec;
        }
        
        public static void AssertThrows<ExceptionT>(this Action action) where ExceptionT: Exception
        {
            // The purpose of this method is to provide an alternative to Assert.Throws<T> that
            // swallow exceptions. This is paramount for ensuring that the debugger does NOT break on exception site,
            // while still failing the test should an exception be encountered.
            try
            {
                action();
            }

            // The exception must be of the generic type. Otherwise it will not return, causing the test to fail.
            catch (ExceptionT _)
            {
                return;
            }
            
            Assert.Fail();
        }
    }
}
