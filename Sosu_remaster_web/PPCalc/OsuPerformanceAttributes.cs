﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sosu.PPCalc
{
    public class OsuPerformanceAttributes
    {
        [JsonProperty("aim")]
        public double Aim { get; set; }

        [JsonProperty("speed")]
        public double Speed { get; set; }

        [JsonProperty("accuracy")]
        public double Accuracy { get; set; }

        [JsonProperty("flashlight")]
        public double Flashlight { get; set; }

        [JsonProperty("effective_miss_count")]
        public double EffectiveMissCount { get; set; }

        public double Total { get; set; }
    }
}
