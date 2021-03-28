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

namespace LogApp
{
    /// <summary>
    /// Main application class
    /// </summary>
    public sealed partial class App
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            IgnoreNullValues = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        };
        private static readonly DateTime Now = DateTime.UtcNow;
        private static Dictionary<string, object> appConfig = null;
        private static string containerVersion = DateTime.UtcNow.ToString("MMdd-HHmm");

        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">Command Line Parameters</param>
        /// <returns>0 on success</returns>
        public static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                containerVersion = args[0].Trim();
            }

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

        // read AppConfig from gitops.json
        private static Dictionary<string, object> ReadAppConfig()
        {
            string file = "../gitops.json";

            if (!File.Exists(file))
            {
                // handle running in debugger
                Directory.SetCurrentDirectory("../../../../");

                if (!File.Exists(file))
                {
                    Console.WriteLine("gitops.json file not found");
                    return null;
                }
            }

            try
            {
                // deserialze the json
                return JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(file), JsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception reading gitops.json: {ex.Message}");
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
                string fn = $"{target}/{appConfig["namespace"]}";

                if (Directory.Exists(fn))
                {
                    fn = $"{target}/{appConfig["namespace"]}/{appConfig["name"]}.yaml";

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
                if (appConfig.ContainsKey("targets"))
                {
                    // extract and remove the targets
                    List<string> targets = JsonSerializer.Deserialize<List<string>>(((JsonElement)appConfig["targets"]).GetRawText());
                    appConfig.Remove("targets");

                    if (targets.Count > 0)
                    {
                        // todo - get this from docker build
                        appConfig.Add("version", containerVersion);
                        appConfig.Add("deploy", Now.ToString("yy-MM-dd-HH-mm-ss"));

                        Dictionary<string, object> config;
                        string text = File.ReadAllText($"../../gitops.yaml");

                        foreach (string target in targets)
                        {
                            // create the directory (by namespace)
                            string fn = $"{target}/{appConfig["namespace"]}";
                            if (!Directory.Exists(fn))
                            {
                                Directory.CreateDirectory(fn);
                            }

                            // create namespace.yaml
                            fn = $"{fn}/namespace.yaml";
                            if (!File.Exists(fn))
                            {
                                File.WriteAllText(fn, $"apiVersion: v1\nkind: Namespace\nmetadata:\n  labels:\n    name: {appConfig["namespace"]}\n  name: {appConfig["namespace"]}\n");
                            }

                            string cfg = File.ReadAllText($"{target}/config.json");
                            config = JsonSerializer.Deserialize<Dictionary<string, object>>(cfg, JsonOptions);

                            fn = $"{target}/{appConfig["namespace"]}/{appConfig["name"]}.yaml";
                            if (File.Exists(fn))
                            {
                                File.Delete(fn);
                            }

                            string yaml = text;

                            foreach (var kv in appConfig)
                            {
                                yaml = yaml.Replace("{{gitops." + kv.Key + "}}", kv.Value.ToString())
                                    .Replace("{{ gitops." + kv.Key + " }}", kv.Value.ToString());
                            }

                            foreach (var kv in config)
                            {
                                yaml = yaml.Replace("{{gitops.config." + kv.Key + "}}", kv.Value.ToString())
                                    .Replace("{{ gitops.config." + kv.Key + " }}", kv.Value.ToString());
                            }

                            // check the yaml
                            string[] lines = yaml.Split('\n');
                            bool err = false;

                            foreach (string line in lines)
                            {
                                if (line.Contains("{{gitops.") || line.Contains("{{ gitops."))
                                {
                                    if (!err)
                                    {
                                        Console.WriteLine("Error in gitops.yaml");
                                    }

                                    err = true;

                                    Console.WriteLine(line);
                                }
                            }

                            if (err)
                            {
                                return false;
                            }

                            File.WriteAllText(fn, yaml);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception creating deployments: {ex.Message}");
            }

            return false;
        }
    }
}
