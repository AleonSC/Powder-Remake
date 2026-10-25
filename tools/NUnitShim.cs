// Minimal stand-in for the parts of NUnit the simulation tests use, so they
// can run outside Unity with plain Mono. See tools/run-sim-tests.sh.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        static void Check(bool ok, string what, string message)
        {
            if (!ok) throw new AssertionException(what + (message != null ? " (" + message + ")" : ""));
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            Check(Equals(expected, actual), "expected " + expected + " but was " + actual, message);
        }

        public static void IsTrue(bool c, string message = null) { Check(c, "expected true", message); }
        public static void IsFalse(bool c, string message = null) { Check(!c, "expected false", message); }

        public static void Greater(double a, double b, string message = null) { Check(a > b, a + " is not > " + b, message); }
        public static void GreaterOrEqual(double a, double b, string message = null) { Check(a >= b, a + " is not >= " + b, message); }
        public static void Less(double a, double b, string message = null) { Check(a < b, a + " is not < " + b, message); }
        public static void LessOrEqual(double a, double b, string message = null) { Check(a <= b, a + " is not <= " + b, message); }
    }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable expected, IEnumerable actual)
        {
            if (!expected.Cast<object>().SequenceEqual(actual.Cast<object>()))
                throw new AssertionException("collections differ");
        }
    }

    public static class ShimRunner
    {
        public static int Main()
        {
            int failed = 0;
            var tests = Assembly.GetExecutingAssembly().GetTypes()
                .SelectMany(t => t.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0));
            foreach (var m in tests)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    m.Invoke(Activator.CreateInstance(m.DeclaringType), null);
                    Console.WriteLine("ok   " + m.Name + " (" + sw.ElapsedMilliseconds + "ms)");
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine("FAIL " + m.Name + "\n     " + e.InnerException.Message);
                }
            }
            return failed == 0 ? 0 : 1;
        }
    }
}
