$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    foreach ($project in @('plugin/RevitMCPPlugin.csproj', 'commandset/RevitMCPCommandSet.csproj')) {
        dotnet build $project -c 'Release R27'
        if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar $project" }
    }
    $source = 'plugin/bin/AddIn 2027 Release R27'
    $plugin = 'revit_mcp_plugin'
    $commands = "$plugin/Commands/RevitMCPCommandSet"
    foreach ($file in @('mcp-servers-for-revit.addin', "$plugin/RevitMCPPlugin.dll", "$commands/command.json", "$commands/2027/RevitMCPCommandSet.dll", "$commands/2027/Microsoft.CodeAnalysis.CSharp.dll")) {
        if (!(Test-Path "$source/$file" -PathType Leaf)) { throw "Ausente no pacote: $file" }
    }
    Compress-Archive -Path "$source/*" -DestinationPath 'Duall-Revit2027.zip' -Force
    Write-Host "Pacote criado: $PWD/Duall-Revit2027.zip"
} finally {
    Pop-Location
}
