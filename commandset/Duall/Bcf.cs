#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace RevitMCPCommandSet.Duall;
internal static partial class ModelOps
{
    static string IfcGuid(Guid guid) {
        const string chars="0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";
        var value=new System.Numerics.BigInteger(guid.ToByteArray(true),true,true);
        char[] result=new char[22];
        for(int i=21;i>=0;i--) { result[i]=chars[(int)(value%64)];value/=64; }
        return new string(result);
    }
    static object ResolveIssue(Document d,JObject p) {
        var ids=((JArray)p["ifcGuids"]).Values<string>().ToHashSet(StringComparer.Ordinal);
        var found=new List<object>();
        foreach(var e in new FilteredElementCollector(d).WhereElementIsNotElementType()) {
            var saved=e.get_Parameter(BuiltInParameter.IFC_GUID)?.AsString();
            if(string.IsNullOrEmpty(saved))saved=IfcGuid(ExportUtils.GetExportId(d,e.Id));
            if(ids.Contains(saved))found.Add(new { id=Key(e),ifcGuid=saved,categoria=e.Category?.Name });
        }
        return found;
    }

    static XDocument Xml(ZipArchiveEntry entry) {
        using var stream=entry.Open();
        using var reader=XmlReader.Create(stream,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8000000 });
        return XDocument.Load(reader,LoadOptions.PreserveWhitespace);
    }
    static object Bcf(JObject p) {
        var src=FilePath(p,"arquivo",".bcfzip",true);
        using var zip=ZipFile.OpenRead(src);
        var ver=zip.GetEntry("bcf.version");
        if(ver==null || (string)Xml(ver).Root.Attribute("VersionId")!="2.1") throw new ArgumentException("Informe um BCF 2.1.");
        var topics=new List<object>();
        XDocument changed=null; string changedName=null;
        foreach(var entry in zip.Entries.Where(e=>e.FullName.EndsWith("/markup.bcf",StringComparison.OrdinalIgnoreCase))) {
            var xml=Xml(entry); var root=xml.Root;
            var topic=root.Element("Topic"); var guid=(string)topic.Attribute("Guid");
            if(p["topico"]!=null && !string.Equals(S(p,"topico"),guid,StringComparison.OrdinalIgnoreCase)) continue;
            var comps=new List<object>();
            foreach(var vp in root.Elements("Viewpoints")) {
                var file=(string)vp.Element("Viewpoint");if(file==null)continue;
                var found=zip.GetEntry(entry.FullName[..entry.FullName.LastIndexOf('/')]+"/"+file);
                if(found==null)continue;
                comps.AddRange(Xml(found).Descendants("Selection").Elements("Component").Select(c=>(object)new { ifcGuid=(string)c.Attribute("IfcGuid"),idAutoria=(string)c.Element("AuthoringToolId") }));
            }
            topics.Add(new { guid,titulo=(string)topic.Element("Title"),descricao=(string)topic.Element("Description"),status=(string)topic.Attribute("TopicStatus"),componentes=comps,
                comentarios=root.Elements("Comment").Select(c=>(string)c.Element("Comment")).ToArray() });
            if(p["resposta"]==null) continue;
            if(changed!=null) throw new InvalidDataException("GUID de tópico duplicado.");
            var msg=new XElement("Comment",new XAttribute("Guid",Guid.NewGuid()),new XElement("Date",DateTime.UtcNow),new XElement("Author",S(p,"autor")),new XElement("Comment",S(p,"resposta")));
            var firstView=root.Elements("Viewpoints").FirstOrDefault();
            if(firstView!=null) firstView.AddBeforeSelf(msg);else root.Add(msg);
            if(p["status"]!=null) topic.SetAttributeValue("TopicStatus",S(p,"status"));
            changed=xml;changedName=entry.FullName;
        }
        if(p["resposta"]==null) return topics;
        if(changed==null) throw new ArgumentException("Tópico ausente.");
        var output=FilePath(p,"destino",".bcfzip",false);var temp=Path.Combine(Path.GetDirectoryName(output),Guid.NewGuid()+".tmp");
        try {
            using(var packed=ZipFile.Open(temp,ZipArchiveMode.Create)) foreach(var e in zip.Entries) {
                var dest=packed.CreateEntry(e.FullName);
                using var to=dest.Open();
                if(e.FullName==changedName) changed.Save(to);
                else {using var from=e.Open();from.CopyTo(to);}
            }
            File.Move(temp,output);
        } finally {if(File.Exists(temp)) File.Delete(temp);}
        return new {arquivo=output,topico=S(p,"topico")};
    }
}
#endif
