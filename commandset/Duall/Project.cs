#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System.IO;

namespace RevitMCPCommandSet.Duall;
internal static partial class ModelOps
{
    static string FilePath(JToken p, string key, string extension, bool existing)
    {
        var path=S(p,key);
        if(!Path.IsPathFullyQualified(path) || !path.EndsWith(extension,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Caminho absoluto "+extension+" necessário em "+key);
        if(existing && !File.Exists(path)) throw new FileNotFoundException(path);
        if(!existing && (File.Exists(path) || !Directory.Exists(Path.GetDirectoryName(path))))
            throw new IOException("Destino existente ou pasta ausente: "+path);
        return path;
    }
    static object Project(UIApplication ui, JObject p)
    {
        var template=FilePath(p,"template",".rte",true);
        var output=FilePath(p,"destino",".rvt",false);
        var d=ui.Application.NewProjectDocument(template) ?? throw new InvalidOperationException("Falha ao abrir o template.");
        try {
            using(var t=new Transaction(d,"Aplicar BEP Duall")) {
                t.Start();
                foreach(var item in ((JArray)p["familias"] ?? new JArray()).Values<string>()) {
                    if(!Path.IsPathFullyQualified(item) || !File.Exists(item) || !item.EndsWith(".rfa",StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Arquivo RFA ausente: "+item);
                    if(!All<Family>(d).Any(x=>x.Name==Path.GetFileNameWithoutExtension(item)) && !d.LoadFamily(item,out var loadedFamily))
                        throw new ArgumentException("Falha ao carregar família "+item);
                }
                foreach(var item in (JArray)p["niveis"]) {
                    var name=S(item,"nome");
                    var height=Feet((double)item["elevacaoM"]);
                    var level=All<Level>(d).SingleOrDefault(x=>x.Name==name);
                    if(level==null) { level=Level.Create(d,height);level.Name=name; }
                    else if(Math.Abs(level.ProjectElevation-height)>1e-6) throw new ArgumentException("Nível do template com cota divergente: "+name);
                    if(item["planta"]!=null) {
                        var vt=All<ViewFamilyType>(d).Single(x=>x.Name==S(item,"planta") && (x.ViewFamily==ViewFamily.FloorPlan || x.ViewFamily==ViewFamily.CeilingPlan || x.ViewFamily==ViewFamily.StructuralPlan));
                        if(!All<ViewPlan>(d).Any(v=>v.GenLevel?.Id==level.Id && v.GetTypeId()==vt.Id)) ViewPlan.Create(d,vt.Id,level.Id);
                    }
                }
                if(p["coordenadas"] is JObject c) {
                    var pos=new ProjectPosition(Feet((double)c["lesteM"]),Feet((double)c["norteM"]),Feet((double)c["cotaM"]),(double)c["anguloGraus"]*Math.PI/180);
                    d.ActiveProjectLocation.SetProjectPosition(XYZ.Zero,pos);
                }
                foreach(var item in (JArray)p["vinculos"]) {
                    var path=FilePath(item,"arquivo",".rvt",true);
                    var loaded=RevitLinkType.Create(d,ModelPathUtils.ConvertUserVisiblePathToModelPath(path),new RevitLinkOptions(false));
                    if(!RevitLinkType.IsLoaded(d,loaded.ElementId)) throw new IOException("Falha ao carregar vínculo: "+path);
                    RevitLinkInstance.Create(d,loaded.ElementId,S(item,"posicao")=="compartilhada"?ImportPlacement.Shared:ImportPlacement.Origin);
                }
                BindParameters(ui.Application,d,p);
                foreach(var prop in ((JObject)p["informacoes"]).Properties()) {
                    var pars=d.ProjectInformation.GetParameters(prop.Name);
                    if(pars.Count!=1 || pars[0].IsReadOnly || pars[0].StorageType!=StorageType.String || !pars[0].Set((string)prop.Value))
                        throw new ArgumentException("Confira o parâmetro textual do template: "+prop.Name);
                }
                if(t.Commit()!=TransactionStatus.Committed) throw new InvalidOperationException("Configuração revertida.");
            }
            d.SaveAs(output,new SaveAsOptions { OverwriteExistingFile=false });
            return new { arquivo=output, fonte=S(p,"fonteBep"), niveis=All<Level>(d).Count(), vinculos=All<RevitLinkInstance>(d).Count() };
        } finally { d.Close(false); }
    }
}
#endif
