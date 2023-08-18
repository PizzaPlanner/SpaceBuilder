using Grid.Essentials;
using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

public sealed class CellConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(Cell);

    public object ReadYaml(IParser parser, Type type)
    {
        parser.Consume<SequenceStart>();
        var cell = new Cell()
        {
            x = (short)int.Parse(parser.Consume<Scalar>().Value),
            y = (short)int.Parse(parser.Consume<Scalar>().Value),
            z = (short)int.Parse(parser.Consume<Scalar>().Value),
        };
        parser.Consume<SequenceEnd>();
        
        return cell;
    }
    
    public void WriteYaml(IEmitter emitter, object value, Type type)
    {
        emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));

        var cell = (Cell)value;
        emitter.Emit(new Scalar(cell.x.ToString()));
        emitter.Emit(new Scalar(cell.y.ToString()));
        emitter.Emit(new Scalar(cell.z.ToString()));

        emitter.Emit(new SequenceEnd());
    }
}