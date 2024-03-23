using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Identifiables.UI
{
    public partial class WikiSystem : SystemBase
    {
        Wiki _wiki;
        protected override void OnCreate()
        {
            RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        }

        protected override void OnStartRunning()
        {
            _wiki ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<Wiki>();
            _wiki.Init(World);
            _wiki.style.display = DisplayStyle.None;
        }

        protected override void OnUpdate()
        {
            if(Input.GetKeyDown(KeyCode.F1))
                _wiki.style.display = _wiki.style.display == DisplayStyle.Flex ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}