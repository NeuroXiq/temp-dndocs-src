namespace DNDocs.Domain.Utils.Docfx
{
    public class TOCItem
    {
        public string Name { get; set; }
        public string Href { get; set; }
        public string Homepage { get; set; }
        public bool Expanded { get; set; }
        public IList<TOCItem> Items { get; set; }
    }
}
