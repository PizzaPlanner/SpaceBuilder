using UnityEngine.UIElements;

namespace Identifiables.UI
{
    public class DetailPage : ScrollView
    {
        public DetailPage()
        {
            var header = new VisualElement{name = "Header"};
            header.Add(new VisualElement {name = "Icon"});
            header.Add(new Label{name = "Title"});
            Add(header);
            var body = new VisualElement{name = "Body"};
            body.Add(new Label{name = "Description"});
            body.Add(new Label{name = "IdRef"});
            Add(body);
            var refs  = new VisualElement{name = "References"};
            Add(refs);
        }
        
        Identifieable _content;
        public Identifieable Content
        {
            set
            {
                this.Q<Label>("Title").text = $"Translate({value.DefaultName.Value})";
                this.Q<Label>("Description").text = "lorem ispum\nlorem ipsum\nHello World?";
                this.Q<Label>("IdRef").text = $"Id:   {value.ItemId.ToString()}\nName: {value.DefaultName.Value}";
                this.Q("Icon").style.backgroundImage = value.Icon;
                var refs = this.Q("References");
                foreach (var reference in value.References)
                {
                    var refParent = reference.Relation.ToString();
                    if(refs.Q<Foldout>(refParent) == null)
                        refs.Add(new Foldout(){name = refParent, value = true,  text = refParent});
                        
                    refs.Q<Foldout>(refParent).Add(new WikiIdLinkButton(reference, true));
                }
                    
                _content = value;
            }
            get => _content;
        }
        
        public new class UxmlFactory : UxmlFactory<DetailPage, UxmlTraits> { }
    }
}