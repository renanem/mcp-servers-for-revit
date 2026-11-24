#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
namespace RevitMCPCommandSet.Duall;
internal static partial class ModelOps
{
    static readonly Guid ReceiptSchemaId=new("B31BADA9-1172-4054-8A25-B377F6BCB370");
    static string Digest(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    static string ReceiptName(JObject p)=>"Duall.MCP."+Digest(S(p,"operacaoId"));
    static string Payload(JObject p)=>Digest(p.ToString(Formatting.None));
    static object Prior(Document d,JObject p) {
        var schema=Schema.Lookup(ReceiptSchemaId);
        if(schema==null)return null;
        var record=All<DataStorage>(d).FirstOrDefault(e=>e.Name==ReceiptName(p));
        if(record==null)return null;
        var entity=record.GetEntity(schema);
        if(!entity.IsValid() || entity.Get<string>("hash")!=Payload(p))throw new ArgumentException("operacaoId reutilizado com dados diferentes.");
        return JToken.Parse(entity.Get<string>("resposta"));
    }
    static void Record(Document d,JObject p,object result) {
        var schema=Schema.Lookup(ReceiptSchemaId);
        if(schema==null) {
            var b=new SchemaBuilder(ReceiptSchemaId);b.SetSchemaName("DuallOperationReceipt");
            b.AddSimpleField("hash",typeof(string));b.AddSimpleField("resposta",typeof(string));schema=b.Finish();
        }
        var entity=new Entity(schema);entity.Set("hash",Payload(p));entity.Set("resposta",JsonConvert.SerializeObject(result));
        var record=DataStorage.Create(d);record.Name=ReceiptName(p);record.SetEntity(entity);
    }
}
#endif
