#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPCommandSet.Duall;
internal static partial class ModelOps
{
    static object HostFaces(Document d,JObject p) {
        var host=ById<Element>(d,p,"hospedeiro");
        var link=host as RevitLinkInstance;
        var source=link==null? d : link.GetLinkDocument() ?? throw new ArgumentException("Vínculo descarregado.");
        var elem=link==null?host:ById<Element>(source,p,"elementoVinculado");
        var transform=link?.GetTotalTransform() ?? Transform.Identity;
        var pos=transform.Inverse.OfPoint(Point(p["perto"]));
        var items=new List<(double distancia,object item)>();
        void Walk(GeometryElement g, Transform local) {
            if(g==null)return;
            foreach(var obj in g) {
                if(obj is GeometryInstance inst) {Walk(inst.GetSymbolGeometry(),local.Multiply(inst.Transform));continue;}
                if(obj is not Solid solid)continue;
                foreach(Face f in solid.Faces) {
                    if(f.Reference==null) continue;
                    var hit=f.Project(local.Inverse.OfPoint(pos));if(hit==null)continue;
                    var q=local.OfPoint(hit.XYZPoint);
                    var actual=transform.OfPoint(q)*0.3048;
                    var dir=transform.OfVector(local.OfVector(f.ComputeDerivatives(hit.UVPoint).BasisX)).Normalize();
                    var reference=link==null?f.Reference:f.Reference.CreateLinkReference(link);
                    items.Add((q.DistanceTo(pos),new { face=reference.ConvertToStableRepresentation(d), xyz=new[]{actual.X,actual.Y,actual.Z},direcao=new[]{dir.X,dir.Y,dir.Z} }));
                }
            }
        }
        Walk(elem.get_Geometry(new Options { ComputeReferences=true,DetailLevel=ViewDetailLevel.Fine }),Transform.Identity);
        return items.OrderBy(i=>i.distancia).Take(10).Select(i=>i.item).ToArray();
    }
}
#endif
