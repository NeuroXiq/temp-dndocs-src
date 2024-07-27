namespace DNDocs.Docs.Web.Model
{
    public class Sitemap
    {
        public int Id { get; set; }
        public string SitemapName { get; set; }
        public byte[] ByteData { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
