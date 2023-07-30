using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace Grid.Logic
{
    public class LogicAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct LogicPortDefinition
        {
            public PortType Type;
            public LogicAccess Access;
        }

        public LogicPortDefinition[] Ports;
        
        class LogicBaker : Baker<LogicAuthoring>
        {
            public override void Bake(LogicAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);
                if (authoring.Ports.Length != 0)
                {
                    AddComponent<RegisterAsLogicAddressable>(e);
                    var buffer = AddBuffer<LogicPort>(e);
                    foreach (var port in authoring.Ports)
                        buffer.Add(new LogicPort
                        {
                            Type = port.Type, 
                            Access = port.Access
                        });
                }

                if (authoring.Ports.Any(x => (x.Access & LogicAccess.Read) != 0))
                    AddBuffer<LogicTarget>(e);
            }
        }
    }
}