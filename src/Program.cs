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
            string file = "../who.dat";

            if (!File.Exists(file))
            {
                file = "../../../" + file;

                if (!File.Exists(file))
                {
                    Console.WriteLine("who.dat file not found");
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
            foreach (string target in p.Targets)
            {
                if (!Directory.Exists(target))
                {
                    Console.WriteLine($"deploy/{target} directory does not exist");
                    return -1;
                }
            }

            Config config;
            text = File.ReadAllText($"../../who.yaml");

            foreach (string target in p.Targets)
            {
                string fn = $"{target}/{p.Name}.yaml";
                string cfg = File.ReadAllText($"{target}/config.dat");

                config = yaml.Deserialize<Config>(cfg);

                if (File.Exists(fn))
                {
                    File.Delete(fn);
                }

                string s = text.Replace("{{whodat.Config.Region}}", config.Region)
                    .Replace("{{whodat.Config.Zone}}", config.Zone);

                File.WriteAllText(fn, s);
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
        public string Owner { get; set; }
        public string Name { get; set; }
        public List<string> Targets { get; set; } = new List<string>();
    }
}
