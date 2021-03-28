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
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">Command Line Parameters</param>
        /// <returns>0 on success</returns>
        public static int Main(string[] args)
        {
            string file = "../gitops.dat";

            if (!File.Exists(file))
            {
                file = "../../../" + file;

                if (!File.Exists(file))
                {
                    Console.WriteLine("gitops.dat file not found");
                    return -1;
                }
            }

            string text = File.ReadAllText(file);

            var yaml = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var p = yaml.Deserialize<AppConfig>(text);

            string dir = "deploy";
            if (!Directory.Exists(dir))
            {
                dir = "../../../" + dir;
                if (!Directory.Exists(dir))
                {
                    Console.WriteLine("deploy directory doesn't exist");
                    return -1;
                }
            }

            Directory.SetCurrentDirectory(dir);

            // todo - check every directory
            foreach (string target in new string[] { "azure", "do", "gcp" })
            {
                if (!Directory.Exists(target))
                {
                    Console.WriteLine($"deploy/{target} directory does not exist");
                    return -1;
                }

                string fn = $"{target}/{p.Namespace}";

                if (Directory.Exists(fn))
                {
                    fn = $"{target}/{p.Namespace}/{p.Name}.yaml";

                    if (File.Exists(fn))
                    {
//                        File.Delete(fn);
                    }
                }
            }

            if (p.Targets != null && p.Targets.Count > 0)
            {
                // todo - get this from docker build
                string semver = DateTime.UtcNow.ToString("MMdd-HHmm");

                Config config;
                text = File.ReadAllText($"../../gitops.yaml");

                foreach (string target in p.Targets)
                {
                    // create the directory (by namespace)
                    string fn = $"{target}/{p.Namespace}";
                    if (!Directory.Exists(fn))
                    {
                        Directory.CreateDirectory(fn);
                    }

                    // create namespace.yaml
                    fn = $"{fn}/namespace.yaml";
                    if (!File.Exists(fn))
                    {
                        File.WriteAllText(fn, $"apiVersion: v1\nkind: Namespace\nmetadata:\n  labels:\n    name: {p.Namespace}\n  name: {p.Namespace}\n");
                    }

                    string cfg = File.ReadAllText($"{target}/config.dat");
                    config = yaml.Deserialize<Config>(cfg);

                    fn = $"{target}/{p.Namespace}/{p.Name}.yaml";
                    if (File.Exists(fn))
                    {
                        File.Delete(fn);
                    }

                    string s = text.Replace("{{gitops.Config.Region}}", config.Region)
                    .Replace("{{gitops.Config.Zone}}", config.Zone)
                    .Replace("{{gitops.Name}}", p.Name)
                    .Replace("{{gitops.Namespace}}", p.Namespace)
                    .Replace("{{gitops.Imagename}}", p.Imagename)
                    .Replace("{{gitops.Imagetag}}", p.Imagetag)
                    .Replace("{{gitops.Semver}}", semver);

                    File.WriteAllText(fn, s);
                }
            }

            return 0;
        }
    }

    public class Config
    {
        public string Region { get; set; }
        public string Zone { get; set; }
    }

    public class AppConfig
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Imagename { get; set; }
        public string Imagetag { get; set; }
        public List<string> Targets { get; set; } = new List<string>();
    }
}
