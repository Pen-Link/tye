﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    static partial class Program
    {
        public static Command CreatePushCommand()
        {
            var command = new Command("push", "build and push application containers to registry")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
                StandardOptions.Tags,
                StandardOptions.Framework,
                StandardOptions.Environment,
                StandardOptions.IncludeLatestTag,
                StandardOptions.BuildId,
                StandardOptions.CreateForce("Override validation and force push.")
            };

            command.Handler = CommandHandler.Create<PushCommandArguments>(async args =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (args.Path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(args.Console, args.Verbosity);
                output.WriteInfoLine("Loading Application Details...");

                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(args.Tags);

                var application = await ApplicationFactory.CreateAsync(output, args.Path, args.Framework, filter, args.Environment, args.BuildId);
                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
                }

                var executeOutput = new OutputContext(args.Console, args.Verbosity);
                await ExecutePushAsync(output, application, environment: args.Environment, args.Interactive, args.IncludeLatestTag);
            });

            return command;
        }

        private static async Task ExecutePushAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive, bool includeLatestTag)
        {
            await application.ProcessExtensionsAsync(options: null, output, ExtensionContext.OperationKind.Deploy);
            ApplyRegistry(output, application, interactive, requireRegistry: true);

            var executor = new ApplicationExecutor(output)
            {
                ServiceSteps =
                {
                    new ApplyContainerDefaultsStep(),
                    new CombineStep() { Environment = environment, },
                    new PublishProjectStep(),
                    new BuildDockerImageStep() { Environment = environment, },
                    new PushDockerImageStep() { Environment = environment, IncludeLatestTag = includeLatestTag, },
                },
            };

            await executor.ExecuteAsync(application);
        }

        private class PushCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public FileInfo Path { get; set; } = default!;

            public Verbosity Verbosity { get; set; }

            public bool Interactive { get; set; } = false;

            public string Framework { get; set; } = default!;

            public string[] Tags { get; set; } = Array.Empty<string>();

            public string Environment { get; set; } = default!;

            public bool IncludeLatestTag { get; set; } = true;

            public string? BuildId { get; set; }
        }
    }
}
