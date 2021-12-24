// Copied from https://github.com/cloudbase/powershell-yaml/blob/master/powershell-yaml.psm1

// Copyright 2016 Cloudbase Solutions Srl
//
//    Licensed under the Apache License, Version 2.0 (the "License"); you may
//    not use this file except in compliance with the License. You may obtain
//    a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
//    WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
//    License for the specific language governing permissions and limitations
//    under the License.

namespace Tandoku.Yaml;

using System.Text.RegularExpressions;
using YamlDotNet;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

public class StringQuotingEmitter : ChainedEventEmitter
{
    // Patterns from https://yaml.org/spec/1.2/spec.html#id2804356
    private static Regex _quotedRegex = new Regex(
        @"^(\~|null|true|false|-?(0|[0-9][0-9]*)(\.[0-9]*)?([eE][-+]?[0-9]+)?)?$",
        RegexOptions.Compiled);

    public StringQuotingEmitter(IEventEmitter next) : base(next) { }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        var typeCode = eventInfo.Source.Value != null ?
            Type.GetTypeCode(eventInfo.Source.Type) :
            TypeCode.Empty;

        switch (typeCode)
        {
            case TypeCode.Char:
                if (char.IsDigit((char)eventInfo.Source.Value))
                {
                    eventInfo.Style = ScalarStyle.DoubleQuoted;
                }
                break;

            case TypeCode.String:
                var val = eventInfo.Source.Value.ToString();
                if (_quotedRegex.IsMatch(val))
                {
                    eventInfo.Style = ScalarStyle.DoubleQuoted;
                }
                else if (val.IndexOf('\n') > -1)
                {
                    eventInfo.Style = ScalarStyle.Literal;
                }
                break;
        }

        base.Emit(eventInfo, emitter);
    }
}
