using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace BlueMarsh.Tandoku.Yaml
{
    internal sealed class FlowStyleEventEmitter : ChainedEventEmitter
    {
        internal FlowStyleEventEmitter(IEventEmitter next)
            : base(next)
        {
        }

        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            if (UseFlowStyle(eventInfo.Source.Value))
                eventInfo.Style = SequenceStyle.Flow;

            base.Emit(eventInfo, emitter);
        }

        public override void Emit(MappingStartEventInfo eventInfo, IEmitter emitter)
        {
            if (UseFlowStyle(eventInfo.Source.Value))
                eventInfo.Style = MappingStyle.Flow;

            base.Emit(eventInfo, emitter);
        }

        private static bool UseFlowStyle(object? o)
        {
            // TODO: generalize this
            return o is int[];
        }
    }
}
