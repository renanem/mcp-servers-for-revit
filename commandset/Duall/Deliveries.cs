#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;
using Newtonsoft.Json.Linq;
using System.IO;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace RevitMCPCommandSet.Duall;
internal static partial class ModelOps
{
    static object Markup(Document d,JObject p) {
        var path=FilePath(p,"destino",".pdf",false);
        var view=ById<View3D>(d,p,"vista");
        if(view.IsTemplate || !view.CanBePrinted) throw new ArgumentException("Selecione uma vista 3D imprimível.");
        var tmp=Path.Combine(Path.GetDirectoryName(path),Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try {
            var name=Path.GetFileNameWithoutExtension(path);
            var options=new PDFExportOptions {Combine=true,FileName=name};
            if(!d.Export(tmp,new List<ElementId>{view.Id},options))throw new IOException("O Revit recusou o PDF.");
            var result=Directory.GetFiles(tmp,"*.pdf").Single();
            if(new FileInfo(result).Length==0)throw new IOException("PDF vazio.");
            File.Move(result,path);
            return new {arquivo=path,vista=view.Name};
        } finally {Directory.Delete(tmp,true);}
    }

    static void BindParameters(Application app, Document d, JObject p) {
        if(p["parametrosCompartilhados"] is not JObject setup)return;
        string file=FilePath(setup,"arquivo",".txt",true);
        var original=app.SharedParametersFilename;
        try {
            app.SharedParametersFilename=file;
            var defs=app.OpenSharedParameterFile() ?? throw new ArgumentException("Arquivo de parâmetros compartilhados inválido.");
            foreach(var item in (JArray)setup["itens"]) {
                var group=defs.Groups.get_Item(S(item,"grupo"));
                var def=group?.Definitions.get_Item(S(item,"nome")) as ExternalDefinition ?? throw new ArgumentException("Definição compartilhada ausente.");
                var cats=app.Create.NewCategorySet();
                foreach(string name in ((JArray)item["categorias"]).Values<string>()) {
                    var cat=Category.GetCategory(d,Enum.Parse<BuiltInCategory>(name));
                    if(cat==null || !cat.AllowsBoundParameters)throw new ArgumentException("Categoria inválida: "+name);
                    cats.Insert(cat);
                }
                var existing=SharedParameterElement.Lookup(d,def.GUID);
                var key=(Definition)existing?.GetDefinition() ?? def;
                bool instance=S(item,"vincularEm")=="instancia";
                var old=d.ParameterBindings.get_Item(key) as ElementBinding;
                if(old!=null) {
                    if((old is InstanceBinding)!=instance)throw new ArgumentException("Vínculo existente com escopo divergente: "+def.Name);
                    foreach(Category cat in old.Categories)cats.Insert(cat);
                }
                ElementBinding binding=instance?app.Create.NewInstanceBinding(cats):app.Create.NewTypeBinding(cats);
                bool success=old==null?d.ParameterBindings.Insert(key,binding,GroupTypeId.Data):d.ParameterBindings.ReInsert(key,binding,((InternalDefinition)key).GetGroupTypeId());
                if(!success)throw new InvalidOperationException("Falha ao vincular parâmetro: "+def.Name);
            }
        } finally {app.SharedParametersFilename=original;}
    }
}
#endif
