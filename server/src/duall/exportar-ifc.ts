export const exportarIFC = String.raw`
if (document.Title != Convert.ToString(parameters[0]))
    throw new InvalidOperationException("O documento ativo mudou.");
var pasta = Convert.ToString(parameters[1]);
var nome = Convert.ToString(parameters[2]);
if (!System.IO.Path.IsPathFullyQualified(pasta) || !System.IO.Directory.Exists(pasta))
    throw new ArgumentException("Informe uma pasta de destino absoluta existente.");
if (System.IO.Path.GetFileName(nome) != nome || !nome.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Nome IFC inválido.");
var destino = System.IO.Path.Combine(pasta, nome);
if (System.IO.File.Exists(destino)) throw new System.IO.IOException("O arquivo já existe.");
var vistaId = Convert.ToString(parameters[4]);
using (var opcoes = new IFCExportOptions()) {
    opcoes.FileVersion = Convert.ToString(parameters[3]) == "IFC4" ? IFCVersion.IFC4 : IFCVersion.IFC2x3;
    if (vistaId.Length > 0) {
        var vista = document.GetElement(new ElementId(Int64.Parse(vistaId))) as View3D;
        if (vista == null || vista.IsTemplate) throw new ArgumentException("Informe uma vista 3D válida.");
        opcoes.FilterViewId = vista.Id;
    }
    var temporaria = System.IO.Path.Combine(pasta, ".duall-" + Guid.NewGuid().ToString("N"));
    System.IO.Directory.CreateDirectory(temporaria);
    try {
        using (var tx = new Transaction(document, "Exportação IFC Duall")) {
            tx.Start();
            if (!document.Export(temporaria, nome, opcoes)) throw new InvalidOperationException("O Revit recusou a exportação.");
            tx.RollBack();
        }
        var arquivo = new System.IO.FileInfo(System.IO.Path.Combine(temporaria, nome));
        if (!arquivo.Exists || arquivo.Length == 0) throw new InvalidOperationException("Exportação sem arquivo.");
        arquivo.MoveTo(destino);
        return new { documento = document.Title, caminho = destino, bytes = arquivo.Length, versao = opcoes.FileVersion.ToString(), vistaId };
    } finally {
        System.IO.Directory.Delete(temporaria, true);
    }
}
`;
