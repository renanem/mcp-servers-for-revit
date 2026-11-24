// O add-in compila este corpo fixo em Execute(Document document, object[] parameters).
// Valores recebidos do Gemini são passados separadamente como dados.
export const consultaMEP = String.raw`
var eletrica = new[] {
    BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting,
    BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting,
    BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_ElectricalFixtures,
    BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices
};
var categorias = eletrica.Concat(new[] {
    BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_FlexPipeCurves,
    BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_PipeAccessory,
    BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_Sprinklers,
    BuiltInCategory.OST_FireAlarmDevices, BuiltInCategory.OST_MechanicalEquipment
}).ToList();
var hidraulicos = new HashSet<string> {
    "DomesticHotWater", "DomesticColdWater", "Sanitary", "Storm",
    "Vent", "DomesticHotWaterReturn"
};
var disciplinaFiltro = Convert.ToString(parameters[1]);
var nivelFiltro = Convert.ToString(parameters[2]);
var sistemaFiltro = Convert.ToString(parameters[3]);
int pagina = Convert.ToInt32(parameters[4]);
int tamanho = Convert.ToInt32(parameters[5]);
var coletor = new FilteredElementCollector(document)
    .WherePasses(new ElementMulticategoryFilter(categorias)).WhereElementIsNotElementType();

var linhas = coletor.Select(e => {
    var cat = (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), e.Category.Id.ToString());
    var tipo = document.GetElement(e.GetTypeId()) as ElementType;
    var idSistema = e.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
    var sistemaTipo = idSistema != null && idSistema.StorageType == StorageType.ElementId
        ? document.GetElement(idSistema.AsElementId()) as Autodesk.Revit.DB.Plumbing.PipingSystemType : null;
    var classe = sistemaTipo == null ? "" : sistemaTipo.SystemClassification.ToString();
    var sistema = sistemaTipo == null ? "" : sistemaTipo.Name;
    var nivel = document.GetElement(e.LevelId) as Level;
    if (nivel == null && e is MEPCurve) nivel = ((MEPCurve)e).ReferenceLevel;
    var marca = (e.LookupParameter("E3-Disciplina")?.AsString() ?? "").Trim();
    if (marca.Length == 0) marca = (tipo?.LookupParameter("E3-Disciplina")?.AsString() ?? "").Trim();
    var disciplina = "revisar";
    switch (marca.ToUpperInvariant()) {
        case "SPK": case "SPRINKLER": disciplina = "spk"; break;
        case "PCI": case "INCÊNDIO": case "INCENDIO": disciplina = "incendio"; break;
        case "ELÉTRICA": case "ELETRICA": disciplina = "eletrica"; break;
        case "HIDRÁULICA": case "HIDRAULICA": disciplina = "hidraulica"; break;
        default:
            if (eletrica.Contains(cat)) disciplina = "eletrica";
            else if (cat == BuiltInCategory.OST_Sprinklers) disciplina = "spk";
            else if (cat == BuiltInCategory.OST_FireAlarmDevices || classe.StartsWith("FireProtect")) disciplina = "incendio";
            else if (cat == BuiltInCategory.OST_PlumbingFixtures || hidraulicos.Contains(classe)) disciplina = "hidraulica";
            break;
    }
    var curva = e is MEPCurve ? e.Location as LocationCurve : null;
    var diametro = e.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
        ?? e.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
    double? diametroMm = diametro != null && diametro.HasValue && diametro.StorageType == StorageType.Double
        ? (double?)Math.Round(diametro.AsDouble() * 304.8, 2) : null;
    return new {
        diametroMm, id = e.Id.ToString(), disciplina, marca, nivel = nivel?.Name ?? "",
        sistema, classe, categoria = e.Category.Name, familia = tipo?.FamilyName ?? "",
        tipo = tipo?.Name ?? "", metros = curva == null ? (double?)null : curva.Curve.Length * 0.3048
    };
}).Where(x => (disciplinaFiltro == "todas" || x.disciplina == disciplinaFiltro)
    && (nivelFiltro == "" || String.Equals(x.nivel, nivelFiltro, StringComparison.OrdinalIgnoreCase))
    && (sistemaFiltro == "" || String.Equals(x.sistema, sistemaFiltro, StringComparison.OrdinalIgnoreCase)))
.ToList();
if (Convert.ToString(parameters[0]) == "lista") {
    var itens = linhas.OrderBy(x => Convert.ToInt64(x.id)).Skip((pagina - 1) * tamanho).Take(tamanho).ToList();
    return new { documento = document.Title, total = linhas.Count, pagina, tamanho, itens };
}
var grupos = linhas.GroupBy(x => new { x.disciplina, x.nivel, x.sistema, x.categoria, x.familia, x.tipo, x.diametroMm })
    .OrderBy(g => g.Key.disciplina).ThenBy(g => g.Key.nivel).ThenBy(g => g.Key.sistema)
    .ThenBy(g => g.Key.categoria).ThenBy(g => g.Key.familia).ThenBy(g => g.Key.tipo).ThenBy(g => g.Key.diametroMm)
    .Select(g => new { grupo = g.Key, quantidade = g.Count(), trechosMedidos = g.Count(x => x.metros.HasValue),
        metros = g.Any(x => x.metros.HasValue) ? (double?)Math.Round(g.Sum(x => x.metros ?? 0), 3) : null }).ToList();
return new { documento = document.Title, total = linhas.Count, grupos };
`;
