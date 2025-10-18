using System;

namespace Tomat.TML.ClientBootstrap;

internal static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine($"TEST: {string.Join(", ", args)}");
    }
}