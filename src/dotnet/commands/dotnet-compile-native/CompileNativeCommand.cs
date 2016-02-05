// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dotnet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class CompileNativeCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            try
            {
                var nativeCompilerCommandArgs = new NativeCompilerCommandApp("dotnet compile-native", ".NET Native Compiler", "Native Compiler for the .NET Platform");
                return nativeCompilerCommandArgs.Execute(OnExecute, args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

        private static bool OnExecute(List<ProjectContext> contexts, NativeCompilerCommandApp args)
        {
            var success = true;

            foreach (var context in contexts)
            {
                success &= CompileNative(context, args);
            }
            
            return success;
        }

        private static bool CompileNative(
            ProjectContext context,
            NativeCompilerCommandApp args)
        {
            var outputPathCalculator = context.GetOutputPathCalculator(args.OutputValue);
            var outputPath = outputPathCalculator.GetOutputDirectoryPath(args.ConfigValue);
            var nativeOutputPath = Path.Combine(outputPath, "native");
            var intermediateOutputPath =
                outputPathCalculator.GetIntermediateOutputDirectoryPath(args.ConfigValue, args.IntermediateValue);
            var nativeIntermediateOutputPath = Path.Combine(intermediateOutputPath, "native");
            Directory.CreateDirectory(nativeOutputPath);
            Directory.CreateDirectory(nativeIntermediateOutputPath);

            var compilationOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, args.ConfigValue);
            var managedOutput = outputPathCalculator.GetAssemblyPath(args.ConfigValue);

            var nativeArgs = new List<string>();

            // Input Assembly
            nativeArgs.Add($"{managedOutput}");
            // ILC Args
            if (!string.IsNullOrWhiteSpace(args.IlcArgsValue))
            {
                nativeArgs.Add("--ilcargs");
                nativeArgs.Add($"{args.IlcArgsValue}");
            }

            // ILC Path
            if (!string.IsNullOrWhiteSpace(args.IlcPathValue))
            {
                nativeArgs.Add("--ilcpath");
                nativeArgs.Add(args.IlcPathValue);
            }

            // ILC SDK Path
            if (!string.IsNullOrWhiteSpace(args.IlcSdkPathValue))
            {
                nativeArgs.Add("--ilcsdkpath");
                nativeArgs.Add(args.IlcSdkPathValue);
            }

            // AppDep SDK Path
            if (!string.IsNullOrWhiteSpace(args.AppDepSdkPathValue))
            {
                nativeArgs.Add("--appdepsdk");
                nativeArgs.Add(args.AppDepSdkPathValue);
            }

            // CodeGen Mode
            if (args.IsCppModeValue)
            {
                nativeArgs.Add("--mode");
                nativeArgs.Add("cpp");
            }

            if (!string.IsNullOrWhiteSpace(args.CppCompilerFlagsValue))
            {
                nativeArgs.Add("--cppcompilerflags");
                nativeArgs.Add(args.CppCompilerFlagsValue);
            }

            // Configuration
            if (args.ConfigValue != null)
            {
                nativeArgs.Add("--configuration");
                nativeArgs.Add(args.ConfigValue);
            }

            // Architecture
            if (args.ArchValue != null)
            {
                nativeArgs.Add("--arch");
                nativeArgs.Add(args.ArchValue);
            }

            // Intermediate Path
            nativeArgs.Add("--temp-output");
            nativeArgs.Add($"{nativeIntermediateOutputPath}");

            // Output Path
            nativeArgs.Add("--output");
            nativeArgs.Add($"{nativeOutputPath}");

            // Write Response File
            var rsp = Path.Combine(nativeIntermediateOutputPath, $"dotnet-compile-native.{context.ProjectFile.Name}.rsp");
            File.WriteAllLines(rsp, nativeArgs);

            // TODO Add -r assembly.dll for all Nuget References
            //     Need CoreRT Framework published to nuget

            // Do Native Compilation
            var exitCode = CompileNativeCommandInternal.Run(new string[] { "--rsp", $"{rsp}" });

            return exitCode == 0;
        }
    }
}
