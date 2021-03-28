// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet;
using YamlDotNet.Core;
using YamlDotNet.Helpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LogApp
{
    /// <summary>
    /// Main application class
    /// </summary>
    public sealed partial class App
    {
        private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        private static AppConfig appConfig = null;

        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">Command Line Parameters</param>
        /// <returns>0 on success</returns>
        public static int Main(string[] args)
        {
            appConfig = ReadAppConfig();

            // read config and set working directory to /deploy
            if (appConfig == null || !SetDeployDir())
            {
                return -1;
            }

            // delete deployments
            DeleteDeployments();

            // create new deployments
            if (!CreateDeployments())
            {
                return -1;
            }

            // success
            return 0;
        }

        // read AppConfig from gitops.dat
        private static AppConfig ReadAppConfig()
        {
            string file = "../gitops.dat";

            if (!File.Exists(file))
            {
                // handle running in debugger
                Directory.SetCurrentDirectory("../../../../");

                if (!File.Exists(file))
                {
                    Console.WriteLine("gitops.dat file not found");
                    return null;
                }
            }

            try
            {
                // deserialze the yaml
                return YamlDeserializer.Deserialize<AppConfig>(File.ReadAllText(file));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception reading gitops.dat: {ex.Message}");
                return null;
            }
        }

        // set working directory to /deploy
        private static bool SetDeployDir()
        {
            string dir = "deploy";

            // handle running in debugger
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("deploy directory doesn't exist");
                return false;
            }

            Directory.SetCurrentDirectory(dir);

            return true;
        }

        // delete existing deployments
        private static void DeleteDeployments()
        {
            // delete all deployment files
            foreach (string target in Directory.EnumerateDirectories("."))
            {
                string fn = $"{target}/{appConfig.Namespace}";

                if (Directory.Exists(fn))
                {
                    fn = $"{target}/{appConfig.Namespace}/{appConfig.Name}.yaml";

                    if (File.Exists(fn))
                    {
                        File.Delete(fn);
                    }
                }
            }
        }

        // create new deployments
        private static bool CreateDeployments()
        {
            try
            {
                if (appConfig.Targets != null && appConfig.Targets.Count > 0)
                {
                    // todo - get this from docker build
                    string version = DateTime.UtcNow.ToString("MMdd-HHmm");

                    Config config;
                    string text = File.ReadAllText($"../../gitops.yaml");

                    foreach (string target in appConfig.Targets)
                    {
                        // create the directory (by namespace)
                        string fn = $"{target}/{appConfig.Namespace}";
                        if (!Directory.Exists(fn))
                        {
                            Directory.CreateDirectory(fn);
                        }

                        // create namespace.yaml
                        fn = $"{fn}/namespace.yaml";
                        if (!File.Exists(fn))
                        {
                            File.WriteAllText(fn, $"apiVersion: v1\nkind: Namespace\nmetadata:\n  labels:\n    name: {appConfig.Namespace}\n  name: {appConfig.Namespace}\n");
                        }

                        string cfg = File.ReadAllText($"{target}/config.dat");
                        config = YamlDeserializer.Deserialize<Config>(cfg);

                        fn = $"{target}/{appConfig.Namespace}/{appConfig.Name}.yaml";
                        if (File.Exists(fn))
                        {
                            File.Delete(fn);
                        }

                        string s = text.Replace("{{gitops.Config.Region}}", config.Region)
                    .Replace("{{gitops.Config.Zone}}", config.Zone)
                    .Replace("{{gitops.Name}}", appConfig.Name)
                    .Replace("{{gitops.Namespace}}", appConfig.Namespace)
                    .Replace("{{gitops.Imagename}}", appConfig.Imagename)
                    .Replace("{{gitops.Imagetag}}", appConfig.Imagetag)
                    .Replace("{{gitops.Version}}", version);

                        File.WriteAllText(fn, s);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception creating deployments: {ex.Message}");
                return false;
            }
        }
    }
}
