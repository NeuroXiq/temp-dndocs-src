using System.Text.Json;

namespace DNDocs.Domain.Utils.Docfx
{
    public class DocfxJson
    {
        public class NMetadata
        {
            public class NSrc
            {
                
                public List<string> Files { get; set; }
            }

            public List<NSrc> Src { get; set; }
            
            public string Dest { get; set; }

            public bool IncludePrivateMembers { get; set; }

            public bool DisableGitFeatures { get; set; }
            
            public bool DisableDefaultFilter { get; set; }
            
            public bool NoRestore { get; set; }
            
            public string NamespaceLayout { get; set; }
            
            public string MemberLayout { get; set; }
            
            public bool AllowCompilationErrors { get; set; }
        }

        public class NBuild
        {
            public class NGlobalMetadata
            {
                
                public string _appTitle { get; set; }
                
                public string _appFooter { get; set; }
                
                public string _appLogoPath { get; set; }
                
                public string _appFaviconPath { get; set; }
                
                public bool _enableSearch { get; set; }
                
                public string _enableNewTab { get; set; }
                
                public string _disableNavbar { get; set; }
                
                public string _disableBreadcrumb { get; set; }
                
                public string _disableToc { get; set; }
                
                public string _disableAffix { get; set; }
                
                public string _disableContribution { get; set; }
                
                public string _gitContribute { get; set; }
                
                public string _gitUrlPattern { get; set; }
                
                public string _noindex { get; set; }
            }

            public class NContent
            {
                public List<string> Files { get; set; }

                public NContent() { }

                public NContent(List<string> files)
                {
                    Files = files;
                }
            }

            public class NResource
            {
                
                public List<string> Files { get; set; }
            }

            public List<NContent> Content { get; set; }
            
            public List<NResource> Resource { get; set; }
            
            public string Dest { get; set; }

            public string Output { get; set; }

            public List<string> GlobalMetadataFiles { get; set; }
            
            public List<string> FileMetadataFiles { get; set; }
            
            public List<string> Template { get; set; }
            
            public List<string> PostProcessors { get; set; }
            
            public bool KeepFileLink { get; set; }
            
            public bool DisableGitFeatures { get; set; }
            
            public NGlobalMetadata GlobalMetadata { get; set; }
        }

        //
        //
        //
        // [JsonPropertyName("build")]
        public NBuild Build { get; set; }
        // [JsonPropertyName("metadata")]
        public List<NMetadata> Metadata { get; set; }

        public static DocfxJson FromJson(string json)
        {
            JsonSerializerOptions opt = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var x = JsonSerializer.Deserialize<DocfxJson>(json, opt);

            return x;
        }
    }
}
