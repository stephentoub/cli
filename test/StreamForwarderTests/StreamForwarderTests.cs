// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;

namespace StreamForwarderTests
{
    public class StreamForwarderTests : TestBase
    {
        private string _testProjectsRoot = @"TestProjects";

        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint");
        }

        [Fact]
        public void Unbuffered()
        {
            Forward(4, true, "");
            Forward(4, true, "123", "123");
            Forward(4, true, "1234", "1234");
            Forward(3, true, "123456789", "123", "456", "789");
            Forward(4, true, "\r\n", "\n");
            Forward(4, true, "\r\n34", "\n", "34");
            Forward(4, true, "1\r\n4", "1\n", "4");
            Forward(4, true, "12\r\n", "12\n");
            Forward(4, true, "123\r\n", "123\n");
            Forward(4, true, "1234\r\n", "1234", "\n");
            Forward(3, true, "\r\n3456\r\n9", "\n", "3456", "\n", "9");
            Forward(4, true, "\n", "\n");
            Forward(4, true, "\n234", "\n", "234");
            Forward(4, true, "1\n34", "1\n", "34");
            Forward(4, true, "12\n4", "12\n", "4");
            Forward(4, true, "123\n", "123\n");
            Forward(4, true, "1234\n", "1234", "\n");
            Forward(3, true, "\n23456\n89", "\n", "23456", "\n", "89");
        }

        [Fact]
        public void LineBuffered()
        {
            Forward(4, false, "");
            Forward(4, false, "123", "123\n");
            Forward(4, false, "1234", "1234\n");
            Forward(3, false, "123456789", "123456789\n");
            Forward(4, false, "\r\n", "\n");
            Forward(4, false, "\r\n34", "\n", "34\n");
            Forward(4, false, "1\r\n4", "1\n", "4\n");
            Forward(4, false, "12\r\n", "12\n");
            Forward(4, false, "123\r\n", "123\n");
            Forward(4, false, "1234\r\n", "1234\n");
            Forward(3, false, "\r\n3456\r\n9", "\n", "3456\n", "9\n");
            Forward(4, false, "\n", "\n");
            Forward(4, false, "\n234", "\n", "234\n");
            Forward(4, false, "1\n34", "1\n", "34\n");
            Forward(4, false, "12\n4", "12\n", "4\n");
            Forward(4, false, "123\n", "123\n");
            Forward(4, false, "1234\n", "1234\n");
            Forward(3, false, "\n23456\n89", "\n", "23456\n", "89\n");
        }

        [Fact]
        public void TestOutputBeforeUnhandledException()
        {
            var root = Temp.CreateDirectory();
            var testOutputDirectory = root.CreateDirectory("TestOutput").Path;
            var testDirectory = root.CopyDirectory(Path.Combine(_testProjectsRoot, "TestAppThrowUnhandledException"));

            var publishCommand = new PublishCommand(Path.Combine(testDirectory.Path, "project.json"), output:testOutputDirectory);
            publishCommand.ExecuteWithCapturedOutput().Should().Pass();

            var outputExecutable = Path.Combine(testOutputDirectory, "dotnet-unhandled" + Constants.ExeSuffix);

            var oldDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(testOutputDirectory);
            var testCommand = new TestCommand("dotnet");
            var testCommandResult = testCommand.ExecuteWithCapturedOutput("unhandled");
            Directory.SetCurrentDirectory(oldDirectory);

            // todo will this be in stderr?
            var output = testCommandResult.StdOut;
            var outputLines = output.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            Console.WriteLine(output);

            outputLines.Length.Should().Be(5);
            outputLines[0].Should().Be("** 1 WriteLine **");
            outputLines[1].Should().Be("** 2 Write **** 3 Write **** 4 WriteLine **");
            outputLines[2].Should().Be("** 5 Write **");
            outputLines[3].Should().Be("Unhandled Exception: System.Exception: ** Unhandled Exception **");
            // Last line can be ignored
        }

        private static void Forward(int bufferSize, bool unbuffered, string str, params string[] expectedWrites)
        {
            var expectedCaptured = str.Replace("\r", "").Replace("\n", Environment.NewLine);

            // No forwarding.
            Forward(bufferSize, ForwardOptions.None, str, null, new string[0]);

            // Capture only.
            Forward(bufferSize, ForwardOptions.Capture, str, expectedCaptured, new string[0]);

            var writeOptions = unbuffered ?
                ForwardOptions.Write | ForwardOptions.WriteLine :
                ForwardOptions.WriteLine;

            // Forward.
            Forward(bufferSize, writeOptions, str, null, expectedWrites);

            // Forward and capture.
            Forward(bufferSize, writeOptions | ForwardOptions.Capture, str, expectedCaptured, expectedWrites);
        }

        private enum ForwardOptions
        {
            None = 0x0,
            Capture = 0x1,
            Write = 0x02,
            WriteLine = 0x04,
        }

        private static void Forward(int bufferSize, ForwardOptions options, string str, string expectedCaptured, string[] expectedWrites)
        {
            var forwarder = new StreamForwarder(bufferSize);
            var writes = new List<string>();
            if ((options & ForwardOptions.WriteLine) != 0)
            {
                forwarder.ForwardTo(
                    write: (options & ForwardOptions.Write) == 0 ? (Action<string>)null : writes.Add,
                    writeLine: s => writes.Add(s + "\n"));
            }
            if ((options & ForwardOptions.Capture) != 0)
            {
                forwarder.Capture();
            }
            forwarder.Read(new StringReader(str));
            Assert.Equal(expectedWrites, writes);
            var captured = forwarder.GetCapturedOutput();
            Assert.Equal(expectedCaptured, captured);
        }
    }
}
