// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace TestAppThrowUnhandledException
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("** 1 WriteLine **");
            Console.Write("** 2 Write **");
            Console.Write("** 3 Write **");
            Console.WriteLine("** 4 WriteLine **");
            Console.Write("** 5 Write **");
            Console.Out.Flush();
            throw new Exception("** Unhandled Exception **");
        }
    }
}
