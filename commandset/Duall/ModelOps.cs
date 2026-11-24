#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System.IO;

namespace RevitMCPCommandSet.Duall;

internal static partial class ModelOps
{
    static string S(JToken p, string key) => (string)p[key] ?? throw new ArgumentException("Falta " + key);
    static double Feet(double metres) => metres / 0.3048;
    static XYZ Point(JToken p) => new(Feet((double)p[0]), Feet((double)p[1]), Feet((double)p[2]));
    static ElementId Id(JToken p, string key) => new((long)p[key]);
    static T ById<T>(Document d, JToken p, string key) where T : Element =>
        d.GetElement(Id(p, key)) as T ?? throw new ArgumentException("ID incompatível: " + key);
    static IEnumerable<T> All<T>(Document d) where T : Element => new FilteredElementCollector(d).OfClass(typeof(T)).Cast<T>();
    static string Key(Element e) => e.Id.Value.ToString();

    public static object Run(UIApplication app, JObject p)
    {
        var op = S(p, "acao");
        if (op == "projeto") return Project(app, p);
        if (op == "bcf") return Bcf(p);
        var d = app.ActiveUIDocument?.Document ?? throw new InvalidOperationException("Abra o modelo.");
        if (op == "catalogo") return Catalog(d, p);
        if (S(p, "documento") != d.Title) throw new InvalidOperationException("O documento ativo mudou.");
        if(op=="resolver") return ResolveIssue(d,p);
        if(op=="markup") return Markup(d,p);
        if(op=="faces") return HostFaces(d,p);
        if ((bool?)p["simular"] != true && Prior(d,p) is object previous) return previous;
        using var t = new Transaction(d, "Duall · " + op);
        t.Start();
        object result = op switch {
            "pontos" => ((JArray)p["pontos"]).Select(x => Key(Place(d, x))).ToList(),
            "rede" => Network(d, p),
            "furos" => Openings(d, p),
            "corrigir" => Correct(d, p),
            _ => throw new ArgumentException("Operação desconhecida.")
        };
        var report = new { simulado = (bool?)p["simular"] == true, resultado = result };
        if (!report.simulado) Record(d,p,report);
        if (report.simulado) t.RollBack();
        else if (t.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("O Revit reverteu a operação.");
        return report;
    }

    static object Catalog(Document d, JObject p)
    {
        IEnumerable<Element> elems = S(p,"grupo") switch {
            "familias" => All<FamilySymbol>(d), "niveis" => All<Level>(d), "vinculos" => All<RevitLinkInstance>(d),
            "tubos" => All<PipeType>(d), "sistemas" => All<PipingSystemType>(d),
            "eletrodutos" => All<ConduitType>(d), "eletrocalhas" => All<CableTrayType>(d),
            "vistas3d" => All<View3D>(d).Where(v => !v.IsTemplate), _ => throw new ArgumentException("Grupo inválido.")
        };
        var lista = elems.Select(e => new { id = Key(e), nome = e.Name, familia = (e as FamilySymbol)?.FamilyName,
            hospedagem = (e as FamilySymbol)?.Family.FamilyPlacementType.ToString(), categoria = e.Category?.Name })
            .Where(e => (e.familia + " " + e.nome).Contains((string)p["busca"] ?? "", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.familia).ThenBy(e => e.nome).ThenBy(e=>e.id).ToList();
        return new { documento = d.Title, total = lista.Count, itens = lista.Skip((int)p["inicio"]).Take(100) };
    }

    static FamilyInstance Place(Document d, JToken p)
    {
        var symbol = ById<FamilySymbol>(d,p,"tipo");
        var level = ById<Level>(d,p,"nivel");
        var pos = Point(p["xyz"]);
        if (!symbol.IsActive) { symbol.Activate(); d.Regenerate(); }
        var host = p["hospedeiro"] == null ? null : ById<Element>(d,p,"hospedeiro");
        FamilyInstance item;
        if (p["face"] != null) {
            var face = Reference.ParseFromStableRepresentation(d, S(p,"face"));
            item = d.Create.NewFamilyInstance(face, pos, Point(p["direcao"]).Normalize(), symbol);
        } else if (host != null) item = d.Create.NewFamilyInstance(pos,symbol,host,level,StructuralType.NonStructural);
        else item = d.Create.NewFamilyInstance(pos,symbol,level,StructuralType.NonStructural);
        if (item == null) throw new InvalidOperationException("Confira a hospedagem da família.");
        if ((double?)p["giro"] is double giro && giro != 0)
            ElementTransformUtils.RotateElement(d,item.Id,Line.CreateBound(pos,pos+XYZ.BasisZ),giro*Math.PI/180);
        return item;
    }

    static object Network(Document d, JObject p)
    {
        var pts = ((JArray)p["vertices"]).Select(Point).ToList();
        var curves = new List<MEPCurve>();
        var fittings = new List<string>();
        var level = ById<Level>(d,p,"nivel").Id;
        var type = Id(p,"tipo");
        void Size(MEPCurve c, BuiltInParameter key, string field) {
            var par = c.get_Parameter(key);
            if (par == null || !par.Set(Feet((double)p[field]/1000))) throw new InvalidOperationException("Dimensão recusada: "+field);
        }
        Connector End(MEPCurve c, XYZ pt) => c.ConnectorManager.Connectors.Cast<Connector>().OrderBy(x=>x.Origin.DistanceTo(pt)).First();
        for(int i=1;i<pts.Count;i++) {
            MEPCurve c;
            switch(S(p,"classe")) {
                case "tubo":
                    c=Pipe.Create(d,ById<PipingSystemType>(d,p,"sistema").Id,type,level,pts[i-1],pts[i]);
                    Size(c,BuiltInParameter.RBS_PIPE_DIAMETER_PARAM,"diametroMm"); break;
                case "eletroduto":
                    c=Conduit.Create(d,type,pts[i-1],pts[i],level);
                    Size(c,BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM,"diametroMm"); break;
                case "eletrocalha":
                    c=CableTray.Create(d,type,pts[i-1],pts[i],level);
                    Size(c,BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM,"larguraMm");
                    Size(c,BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM,"alturaMm"); break;
                default: throw new ArgumentException("Classe inválida.");
            }
            if(curves.Count>0) {
                d.Regenerate();
                var a=End(curves.Last(),pts[i-1]); var b=End(c,pts[i-1]);
                var fitting = a.CoordinateSystem.BasisZ.IsAlmostEqualTo(-b.CoordinateSystem.BasisZ)
                    ? d.Create.NewUnionFitting(a,b) : d.Create.NewElbowFitting(a,b);
                fittings.Add(Key(fitting));
            }
            curves.Add(c);
        }
        foreach(var pair in new[]{("conectarInicio",curves.First(),pts.First()),("conectarFim",curves.Last(),pts.Last())}) {
            if(p[pair.Item1] == null) continue;
            var target=ById<Element>(d,p,pair.Item1);
            var cm=(target as MEPCurve)?.ConnectorManager ?? (target as FamilyInstance)?.MEPModel?.ConnectorManager;
            d.Regenerate();
            var a=End(pair.Item2,pair.Item3);
            var matches=cm?.Connectors.Cast<Connector>().Where(b=>b.ConnectorType==ConnectorType.End && !b.IsConnected &&
                a.Domain==b.Domain && a.Origin.DistanceTo(b.Origin)<Feet(0.001)).ToList();
            if(matches?.Count!=1) throw new ArgumentException("Informe um terminal com um conector livre coincidente.");
            a.ConnectTo(matches[0]);
        }
        return new { trechos=curves.Select(Key).ToList(), conexoes=fittings };
    }

    static object Correct(Document d, JObject p)
    {
        var changes = new List<object>();
        foreach(var x in (JArray)p["alteracoes"]) {
            var e=ById<Element>(d,x,"id");
            if(e is ElementType) throw new ArgumentException("Selecione uma instância.");
            if(x["deslocamento"]!=null) ElementTransformUtils.MoveElement(d,e.Id,Point(x["deslocamento"]));
            if(x["parametro"]!=null) {
                var pars=e.GetParameters(S(x,"parametro"));
                if(pars.Count!=1 || pars[0].IsReadOnly) throw new ArgumentException("Parâmetro ambíguo ou protegido.");
                var par=pars[0]; bool ok=par.StorageType==StorageType.String ? par.Set(S(x,"valor")) : par.SetValueString(S(x,"valor"));
                if(!ok) throw new ArgumentException("Valor recusado pelo Revit.");
            }
            changes.Add(new { id=Key(e), uid=e.UniqueId });
        }
        return new { pendencia=S(p,"pendencia"), elementos=changes };
    }
}
#endif
