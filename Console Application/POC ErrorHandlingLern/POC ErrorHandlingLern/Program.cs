using System;
using System.Collections.Concurrent;

namespace ErrorHandlingTutorial
{
    /// <summary>Entry point for the demo program.</summary>
    public static class Program
    {
        public static void Main()
        {
            int number = 99999999;
            Console.WriteLine(CustomCompiler.CompileIntegers(number));
            string val = "I LOVE YOU LEKEY";
            Console.WriteLine(CustomCompiler.CompileStrings(val));
        }
    }

    public static class CustomCompiler
    {
        public static string CompileIntegers(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");

            if (value == 0) return "0";

            var buffer = new char[128];  
            int pos = buffer.Length;

            while (value > 0)
            {
                buffer[--pos] = (value & 1) == 0 ? '0' : '1';
                value >>= 1;
            }

            return new string(buffer, pos, buffer.Length - pos);
        }

        public static string CompileStrings(string stringValue)
        {
            string storeBinary = "";

            foreach (var item in stringValue)
            {
                storeBinary += CompileIntegers((int)item);
            }

            return storeBinary;
        }
    }
}