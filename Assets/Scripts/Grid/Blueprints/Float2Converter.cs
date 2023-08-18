using Grid.Essentials;
using System;
using Unity.Mathematics;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

public sealed class Float2Converter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(float2);

    public object ReadYaml(IParser parser, Type type)
    {
        parser.Consume<SequenceStart>();
        var result = new float2()
        {
            x = float.Parse(parser.Consume<Scalar>().Value),
            y = float.Parse(parser.Consume<Scalar>().Value)
        };
        parser.Consume<SequenceEnd>();
        
        return result;
    }
    
    public void WriteYaml(IEmitter emitter, object value, Type type)
    {
        emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));

        var v = (float2)value;
        emitter.Emit(new Scalar(v.x.ToString()));
        emitter.Emit(new Scalar(v.y.ToString()));

        emitter.Emit(new SequenceEnd());
    }
}