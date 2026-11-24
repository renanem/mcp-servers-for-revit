#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json.Linq;

namespace RevitMCPCommandSet.Duall;
internal static partial class ModelOps
{
    static IEnumerable<Solid> Solids(GeometryElement geometry)
    {
        if(geometry==null) yield break;
        foreach(var obj in geometry) {
            if(obj is Solid s && s.Volume>0) yield return s;
            if(obj is GeometryInstance gi)
                foreach(var solid in Solids(gi.GetInstanceGeometry())) yield return solid;
        }
    }

    static object Openings(Document d,JObject p)
    {
        var link=ById<RevitLinkInstance>(d,p,"vinculo");
        var str=link.GetLinkDocument() ?? throw new ArgumentException("Vínculo descarregado.");
        var transform=link.GetTotalTransform();
        var family=ById<FamilySymbol>(d,p,"tipoFuro");
        if(family.Family.FamilyPlacementType != FamilyPlacementType.OneLevelBased)
            throw new ArgumentException("Selecione uma família FURO de um nível, sem hospedeiro.");
        if(!family.IsActive) {family.Activate();d.Regenerate();}
        var category=new ElementMulticategoryFilter(new List<BuiltInCategory>{BuiltInCategory.OST_StructuralFraming,BuiltInCategory.OST_Floors,BuiltInCategory.OST_StructuralColumns,BuiltInCategory.OST_Walls});
        string name=S(p,"vista");
        var view=All<View3D>(d).SingleOrDefault(v=>v.Name==name && !v.IsTemplate);
        if(view==null) { view=View3D.CreateIsometric(d,All<ViewFamilyType>(d).First(x=>x.ViewFamily==ViewFamily.ThreeDimensional).Id);view.Name=name; }
        var red=new OverrideGraphicSettings().SetProjectionLineColor(new Color(255,0,0)).SetCutLineColor(new Color(255,0,0));
        var log=new List<object>();
        var seen=All<FamilyInstance>(d).Where(x=>x.Symbol.Id==family.Id)
            .Select(x=>x.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()??"").ToHashSet();
        foreach(var id in ((JArray)p["trechos"]).Values<long>().Distinct()) {
            var mep=d.GetElement(new ElementId(id)) as MEPCurve ?? throw new ArgumentException("Trecho inválido: "+id);
            var axis=((LocationCurve)mep.Location).Curve.CreateTransformed(transform.Inverse);
            var pts=axis.Tessellate(); double pad=0.01;
            var box=new Outline(new XYZ(pts.Min(v=>v.X)-pad,pts.Min(v=>v.Y)-pad,pts.Min(v=>v.Z)-pad),new XYZ(pts.Max(v=>v.X)+pad,pts.Max(v=>v.Y)+pad,pts.Max(v=>v.Z)+pad));
            var candidates=new FilteredElementCollector(str).WherePasses(category).WherePasses(new BoundingBoxIntersectsFilter(box)).WhereElementIsNotElementType();
            foreach(var structure in candidates) {
                foreach(var solid in Solids(structure.get_Geometry(new Options {DetailLevel=ViewDetailLevel.Fine}))) {
                    using var hits=solid.IntersectWithCurve(axis,new SolidCurveIntersectionOptions());
                    for(int i=0;i<hits.SegmentCount;i++) {
                        var segment=hits.GetCurveSegment(i);var pos=transform.OfPoint(segment.Evaluate(0.5,true));
                        var tag=FormattableString.Invariant($"FURO|{mep.UniqueId}|{link.UniqueId}|{structure.UniqueId}|{Math.Round(pos.X,3)},{Math.Round(pos.Y,3)},{Math.Round(pos.Z,3)}");
                        if(!seen.Add(tag)) continue;
                        var level=mep.ReferenceLevel ?? All<Level>(d).OrderBy(l=>Math.Abs(l.ProjectElevation-pos.Z)).First();
                        var marker=d.Create.NewFamilyInstance(pos,family,level,StructuralType.NonStructural);
                        var comment=marker.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if(comment==null || !comment.Set(tag)) throw new ArgumentException("A família FURO precisa de Comentários editáveis.");
                        if(p["parametroProfundidade"]!=null) {
                            var par=marker.LookupParameter(S(p,"parametroProfundidade"));
                            if(par?.Definition.GetDataType()!=SpecTypeId.Length || !par.Set(segment.Length)) throw new ArgumentException("Profundidade incompatível.");
                        }
                        if(p["parametroDiametro"]!=null) {
                            var par=marker.LookupParameter(S(p,"parametroDiametro"));
                            var diameter=mep.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM) ?? mep.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                            if(diameter==null || par?.Definition.GetDataType()!=SpecTypeId.Length || !par.Set(diameter.AsDouble()+2*Feet((double)p["folgaMm"]/1000)))
                                throw new ArgumentException("Confira diâmetro e folga da reserva.");
                        }
                        var dir=transform.OfVector(segment.ComputeDerivatives(0.5,true).BasisX);
                        if(Math.Abs(dir.X)+Math.Abs(dir.Y)>1e-7)
                            ElementTransformUtils.RotateElement(d,marker.Id,Line.CreateBound(pos,pos+XYZ.BasisZ),Math.Atan2(dir.Y,dir.X));
                        view.SetElementOverrides(marker.Id,red);
                        log.Add(new { marcador=Key(marker),trecho=Key(mep),estrutura=Key(structure),vinculo=Key(link) });
                    }
                }
            }
        }
        return new { vista=Key(view),novos=log };
    }
}
#endif
