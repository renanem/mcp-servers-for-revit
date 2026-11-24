#if REVIT2027_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Duall;

public class ProductionCommand : ExternalEventCommandBase
{
    public ProductionCommand(UIApplication app) : base(new ProductionHandler(), app) { }
    public override string CommandName => "duall_producao";
    public override object Execute(JObject parameters, string requestId)
    {
        var handler = (ProductionHandler)Handler;
        lock (handler)
        {
            handler.Prepare(parameters);
            if (!RaiseAndWaitForCompletion(110000))
                throw new TimeoutException("Confira o modelo antes de repetir. O Revit ainda pode estar processando.");
            return handler.Result;
        }
    }
}

public class ProductionHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent completion = new(true);
    private JObject input;
    public bool TaskCompleted => completion.WaitOne(0);
    public object Result { get; private set; }
    public void Prepare(JObject args)
    {
        if (!TaskCompleted) throw new InvalidOperationException("Existe uma operação em andamento.");
        input = args;
        Result = null;
        completion.Reset();
    }
    public bool WaitForCompletion(int timeoutMilliseconds = 10000) => completion.WaitOne(timeoutMilliseconds);
    public string GetName() => "Duall MEP";
    public void Execute(UIApplication app)
    {
        try { Result = new { success = true, dados = ModelOps.Run(app, input) }; }
        catch (Exception e) { Result = new { success = false, erro = e.GetBaseException().Message }; }
        finally { completion.Set(); }
    }
}
#endif
