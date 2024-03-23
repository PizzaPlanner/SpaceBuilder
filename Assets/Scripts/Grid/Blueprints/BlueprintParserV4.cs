using Grid.Blueprints;
using Grid.Essentials;
using Grid.Logic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.ObjectGraphVisitors;
using YamlDotNet.Serialization.Utilities;

namespace Grid.Blueprints
{
    internal class BlueprintV4DataStructure
    {
        public string Name;
        public IList<string> Tags = new List<string>();
        public IDictionary<ulong, IList<PartData>> Parts = new Dictionary<ulong, IList<PartData>>();
        public IList<PortData> LogicConnections = new List<PortData>();
        public IList<PendingLogicUIData> LogicUIData = new List<PendingLogicUIData>();

        internal class TRS
        {
            public Cell T = math.int3(0, 0, 0);
            public string R = RightAngleRotation.Identity.ToString();
            public uint[] S = { 1u, 1u, 1u };
        }

        internal class PartData
        {
            public TRS At;
            public byte[] Color = new[] { byte.MinValue, byte.MinValue, byte.MinValue };
            public IList<ItemData> Inventory = new List<ItemData>();
            public string Name;
        } 
        
        internal class ItemData
        {
            public uint Id;
            public ushort Stack;
        }

        internal class PortData
        {
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public byte From;
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public byte To;
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public Cell FromPart;
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public Cell ToPart;
        }
    }

    public struct BlueprintParserV4 : IBlueprintParser
    {
        public string Save(Entity grid, EntityManager em)
        {
            var data = new BlueprintV4DataStructure();
            data.Parts = new Dictionary<ulong, IList<BlueprintV4DataStructure.PartData>>();
            data.Name = em.GetComponentData<Name>(grid).Value.Value;
            data.LogicConnections = new List<BlueprintV4DataStructure.PortData>();
            
            var children = em.GetBuffer<Child>(grid, true);
            foreach (var child in children)
            {
                HandleChild(child, em, data);
            }
            
            var builder = new SerializerBuilder()
                          .WithNamingConvention(PascalCaseNamingConvention.Instance)
                          .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults |
                                                          DefaultValuesHandling.OmitEmptyCollections)
                          .WithEventEmitter(x => new FlowArraysEmitter(x))
                          .WithTypeConverter(new CellConverter())
                          .WithTypeConverter(new Float2Converter())
                          .WithEmissionPhaseObjectGraphVisitor(args => new CustomObjectGraphVisitor(args));
            return builder.Build().Serialize(data);
        }

        void HandleChild(Child child, EntityManager em, BlueprintV4DataStructure data)
        {
            if (!em.HasComponent<GridItem>(child.Value) || em.HasComponent<Ghost>(child.Value))
                return;
            var gridItem = em.GetComponentData<GridItem>(child.Value);
            var name = em.GetComponentData<Name>(child.Value);
            var identifieable = em.GetComponentData<Identifieable>(child.Value);
            var rotation = em.GetComponentData<LocalTransform>(child.Value).Rotation;
            var scale = gridItem.Scalable ? gridItem.Area.Size : 1;

            // Inventory
            BlueprintV4DataStructure.ItemData[] inventory = null;
            if (em.HasComponent<Inventory>(child.Value))
            {
                var buffer = em.GetBuffer<Inventory>(child.Value);
                inventory = new BlueprintV4DataStructure.ItemData[buffer.Length];
                for (int i = 0; i < inventory.Length; i++)
                {
                    var item = buffer[i].Item;
                    if (item == Entity.Null)
                        continue;

                    var id = em.GetComponentData<Identifieable>(item);
                    var stack = em.HasComponent<Stackable>(item)
                        ? em.GetComponentData<Stackable>(item).Size
                        : (ushort)1u;
                    inventory[i] = new BlueprintV4DataStructure.ItemData { Stack = stack, Id = id.ItemId };
                }
            }

            // Part
            if (!data.Parts.ContainsKey(identifieable.ItemId))
                data.Parts[identifieable.ItemId] = new List<BlueprintV4DataStructure.PartData>();
            var color = em.HasComponent<URPMaterialPropertyBaseColor>(child.Value)
                ? (int4)(em.GetComponentData<URPMaterialPropertyBaseColor>(child.Value).Value * 255)
                : int4.zero;
            
            var partToAdd = new BlueprintV4DataStructure.PartData
            {
                At = new BlueprintV4DataStructure.TRS
                {
                    T = gridItem.Area.Origin,
                    R = new RightAngleRotation(rotation).ToString(),
                    S = new[] { scale.x, scale.y, scale.z }
                },
                Inventory = inventory?.ToList(),
                Color = new[] { (byte)color.x, (byte)color.y, (byte)color.z },
                Name = name.Value.IsEmpty ? null : name.Value.Value
            };
            data.Parts[identifieable.ItemId].Add(partToAdd);

            // Logic
            if (em.HasBuffer<LogicPort>(child.Value))
            {
                var portsBuffer = em.GetBuffer<LogicPort>(child.Value, true);
                var myCell = gridItem.Area.Origin;
                for (var i = 0; i < portsBuffer.Length; i++)
                {
                    var connection = portsBuffer[i];
                    if (connection.Connection.FromEntity == Entity.Null)
                        continue;

                    var theirCell = em.GetComponentData<GridItem>(connection.Connection.FromEntity).Area.Origin;
                    data.LogicConnections.Add(new BlueprintV4DataStructure.PortData
                    {
                        From = connection.Connection.FromPort,
                        FromPart = theirCell,
                        ToPart = myCell,
                        To = (byte)i
                    });
                }
            }

            if (em.HasComponent<LogicItem>(child.Value))
            {
                data.LogicUIData.Add(new PendingLogicUIData
                {
                    Position = em.GetComponentData<LogicItem>(child.Value).SavedUIPosition,
                    Target = gridItem.Area.Origin
                });
            }
        }

        public BlueprintData Load(string data)
        {
            var builder = new DeserializerBuilder()
                          .WithNamingConvention(PascalCaseNamingConvention.Instance)
                          .WithTypeConverter(new CellConverter())
                          .WithTypeConverter(new Float2Converter());

            var blueprint = builder.Build().Deserialize<BlueprintV4DataStructure>(data);
            
            return new BlueprintData
            {
                Name = blueprint.Name,
                Tags = blueprint.Tags,
                Parts = blueprint.Parts.SelectMany(key => key.Value.Select(value => new BuildingPartInfo
                {
                    Id = new Identifieable(key.Key),
                    Color = value.Color.All(x => x == 0) ? 
                        default : 
                        new Color(value.Color[0] / 255f, value.Color[1] / 255f, value.Color[2] / 255f),
                    Origin = value.At.T,
                    Orientation = RightAngleRotation.FromString(value.At.R),
                    Scale = new uint3(value.At.S[0], value.At.S[1], value.At.S[2]),
                    Name = value.Name ?? default(FixedString64Bytes)
                })),
                LogicConnections = blueprint.LogicConnections.Select(c => new PendingLogicConnection
                {
                   FromPort = c.From,
                   FromCell = c.FromPart,
                   ToCell = c.ToPart,
                   ToPort = c.To
                }),
                LogicUIData = blueprint.LogicUIData
            };
        }

        public string VersionIdentifier => "# Blueprint Version 4";
    }
}

public class FlowArraysEmitter : ChainedEventEmitter
{
    public FlowArraysEmitter(IEventEmitter nextEmitter) : base(nextEmitter) { }

    bool ShouldFlow(Type type) => type == typeof(int[]) 
                                  || type == typeof(uint[]) 
                                  || type == typeof(byte[])
                                  || type == typeof(BlueprintV4DataStructure.PortData)
                                  || type == typeof(BlueprintV4DataStructure.TRS);

    bool ShouldBeImplicit(Type type) => type == typeof(IDictionary<Cell, IList<BlueprintV4DataStructure.PortData>>)
                                        || type == typeof(Cell);

    public override void Emit(MappingStartEventInfo eventInfo, IEmitter emitter)
    {
        if(ShouldFlow(eventInfo.Source.Type))
            eventInfo.Style = MappingStyle.Flow;
        if (ShouldBeImplicit(eventInfo.Source.Type))
            eventInfo.IsImplicit = true;
        base.Emit(eventInfo, emitter);
    }

    public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
    {
        if(ShouldFlow(eventInfo.Source.Type))
            eventInfo.Style = SequenceStyle.Flow;
        if (ShouldBeImplicit(eventInfo.Source.Type))
            eventInfo.IsImplicit = true;
        nextEmitter.Emit(eventInfo, emitter);
    }
}

public class CustomObjectGraphVisitor : ChainedObjectGraphVisitor
{
    public CustomObjectGraphVisitor(EmissionPhaseObjectGraphVisitorArgs nextVisitor) : base(nextVisitor.InnerVisitor) { }

    public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context)
    {
        if (key is { Name: "Color" })
        {
            if (value.Value is byte[] colorArray && colorArray.All(c => c == 0))
                return false;
        }
        else if (key is { Name: "S" })
        {
            if (value.Value is uint[] scaleArray && scaleArray.All(s => s == 1))
                return false;
        }
        else if (key is { Name: "R" })
        {
            if (value.Value is string s && s == RightAngleRotation.Identity.ToString())
                return false;
        }

        // Include all other properties in serialization
        return base.EnterMapping(key, value, context);
    }
}