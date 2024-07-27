namespace DNDocs.Domain.Utils.Docfx
{
    public class ContentItem
    {
        public enum ContentType
        {
            Directory = 1,
            Yml = 2,
            Md = 3
        }

        public string Directory { get; set; }
        public string Name { get; set; }
        public ContentType Type { get; set; }
        public List<ContentItem> Children { get; set; }

        public ContentItem(string directory, string name, ContentType type, List<ContentItem> children)
        {
            Directory = directory;
            Name = name;
            Type = type;
            Children = children;
        }
    }
}
