﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PackagedCommandSpecFactory : IPackagedCommandSpecFactory
    {
        public CommandSpec CreateCommandSpecFromLibrary(
            LockFileTargetLibrary toolLibrary,
            string commandName,
            IEnumerable<string> commandArguments,
            IEnumerable<string> allowedExtensions,
            string nugetPackagesRoot,
            CommandResolutionStrategy commandResolutionStrategy,
            string depsFilePath)
        {

            var toolAssembly = toolLibrary?.RuntimeAssemblies
                    .FirstOrDefault(r => Path.GetFileNameWithoutExtension(r.Path) == commandName);

            if (toolAssembly == null)
            {
                return null;
            }
            
            var commandPath = GetCommandFilePath(nugetPackagesRoot, toolLibrary, toolAssembly);

            if (!File.Exists(commandPath))
            {
                return null;
            }

            var isPortable = IsPortableApp(commandPath);

            return CreateCommandSpecWrappingWithCorehostIfDll(
                commandPath, 
                commandArguments, 
                depsFilePath, 
                commandResolutionStrategy,
                nugetPackagesRoot,
                isPortable);
        }

        private string GetCommandFilePath(string nugetPackagesRoot, LockFileTargetLibrary toolLibrary, LockFileItem runtimeAssembly)
        {
            var packageDirectory = new VersionFolderPathResolver(nugetPackagesRoot)
                .GetInstallPath(toolLibrary.Name, toolLibrary.Version);

            var filePath = Path.Combine(packageDirectory, runtimeAssembly.Path);

            return filePath;
        }

        private CommandSpec CreateCommandSpecWrappingWithCorehostIfDll(
            string commandPath, 
            IEnumerable<string> commandArguments, 
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            string nugetPackagesRoot,
            bool isPortable)
        {
            var commandExtension = Path.GetExtension(commandPath);

            if (commandExtension == FileNameSuffixes.DotNet.DynamicLib)
            {
                return CreatePackageCommandSpecUsingCorehost(
                    commandPath, 
                    commandArguments, 
                    depsFilePath, 
                    commandResolutionStrategy,
                    nugetPackagesRoot,
                    isPortable);
            }
            
            return CreateCommandSpec(commandPath, commandArguments, commandResolutionStrategy);
        }

        private CommandSpec CreatePackageCommandSpecUsingCorehost(
            string commandPath, 
            IEnumerable<string> commandArguments, 
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            string nugetPackagesRoot,
            bool isPortable)
        {
            var host = string.Empty;
            var arguments = new List<string>();

            if (isPortable)
            {
                var muxer = new Muxer();

                host = muxer.MuxerPath;
                if (host == null)
                {
                    throw new Exception("Unable to locate dotnet multiplexer");
                }

                arguments.Add("exec");
            }
            else
            {
                host = CoreHost.LocalHostExePath;
            }

            arguments.Add(commandPath);

            if (depsFilePath != null)
            {
                arguments.Add("--depsfile");
                arguments.Add(depsFilePath);
            }

            arguments.Add("--additionalprobingpath");
            arguments.Add(nugetPackagesRoot);

            arguments.AddRange(commandArguments);

            return CreateCommandSpec(host, arguments, commandResolutionStrategy);
        }

        private CommandSpec CreateCommandSpec(
            string commandPath, 
            IEnumerable<string> commandArguments,
            CommandResolutionStrategy commandResolutionStrategy)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(commandArguments);

            return new CommandSpec(commandPath, escapedArgs, commandResolutionStrategy);
        }

        private bool IsPortableApp(string commandPath)
        {
            var commandDir = Path.GetDirectoryName(commandPath);

            var runtimeConfigPath = Directory.EnumerateFiles(commandDir)
                .FirstOrDefault(x => x.EndsWith("runtimeconfig.json"));

            if (runtimeConfigPath == null)
            {
                return false;
            }

            var runtimeConfig = new RuntimeConfig(runtimeConfigPath);

            return runtimeConfig.IsPortable;
        }
    }
}
