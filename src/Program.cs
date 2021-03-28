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
        private static string containerVersion = Now.ToString("MMdd-HHmm");
        private static List<string> targets = null;

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

            // read config
            appConfig = ReadAppConfig();
            if (appConfig == null)
            {
                return -1;
            }

            // set working directory to /deploy
            if (!SetDeployDir())
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

        // read and validate App Config from gitops.json
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
                var cfg = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(file), JsonOptions);

                bool err = false;

                // check for required fields
                if (!cfg.ContainsKey("name") || string.IsNullOrWhiteSpace(cfg["name"].ToString()))
                {
                    Console.WriteLine("Invalid gitops.json - name is a required field");
                    err = true;
                }

                if (!cfg.ContainsKey("namespace") || string.IsNullOrWhiteSpace(cfg["namespace"].ToString()))
                {
                    Console.WriteLine("Invalid gitops.json - namespace is a required field");
                    err = true;
                }

                if (!cfg.ContainsKey("targets"))
                {
                    Console.WriteLine("Invalid gitops.json - targets is required array");
                    err = true;
                }

                if (err)
                {
                    return null;
                }

                string t = cfg["targets"].ToString().Trim();

                if (string.IsNullOrWhiteSpace(t) ||
                    !t.StartsWith('[') ||
                    !t.EndsWith(']'))
                {
                    Console.WriteLine("Invalid gitops.json - targets is required array");
                    return null;
                }

                // extract and remove the targets
                targets = JsonSerializer.Deserialize<List<string>>(t);
                cfg.Remove("targets");

                // add version and deploy if missing
                if (!cfg.ContainsKey("version"))
                {
                    cfg.Add("version", containerVersion);
                }

                if (!cfg.ContainsKey("deploy"))
                {
                    cfg.Add("deploy", Now.ToString("yy-MM-dd-HH-mm-ss"));
                }

                return cfg;
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
            Dictionary<string, object> config;
            string fileName;
            string yaml;

            try
            {
                if (targets.Count > 0)
                {
                    // read the deployment template
                    string text = File.ReadAllText($"../../gitops.yaml");

                    foreach (string target in targets)
                    {
                        // create the namespace directory
                        fileName = $"{target}/{appConfig["namespace"]}";
                        if (!Directory.Exists(fileName))
                        {
                            Directory.CreateDirectory(fileName);
                        }

                        // create namespace.yaml
                        fileName = $"{fileName}/namespace.yaml";
                        if (!File.Exists(fileName))
                        {
                            File.WriteAllText(fileName, $"apiVersion: v1\nkind: Namespace\nmetadata:\n  labels:\n    name: {appConfig["namespace"]}\n  name: {appConfig["namespace"]}\n");
                        }

                        // load config.json for target cluster
                        config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText($"{target}/config.json"), JsonOptions);

                        // set full path
                        fileName = $"{target}/{appConfig["namespace"]}/{appConfig["name"]}.yaml";

                        // do NOT overwrite text!
                        yaml = text;

                        // replace each app config value
                        foreach (var kv in appConfig)
                        {
                            yaml = yaml.Replace("{{gitops." + kv.Key + "}}", kv.Value.ToString())
                                .Replace("{{ gitops." + kv.Key + " }}", kv.Value.ToString());
                        }

                        // replace each cluster config value
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

                        File.WriteAllText(fileName, yaml);
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
