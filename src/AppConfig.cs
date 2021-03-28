// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace LogApp
{
    public class AppConfig
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Imagename { get; set; }
        public string Imagetag { get; set; }
        public List<string> Targets { get; set; } = new List<string>();
    }
}
