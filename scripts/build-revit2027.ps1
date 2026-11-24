$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    foreach ($project in @('plugin/RevitMCPPlugin.csproj', 'commandset/RevitMCPCommandSet.csproj')) {
        dotnet build $project -c 'Release R27'
        if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar $project" }
    }
    # Compile the exact C# bodies sent by Gemini against the 2027 API.
    $probe = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
    New-Item -ItemType Directory $probe | Out-Null
    try {
        @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0-windows10.0.19041.0</TargetFramework><UseWPF>true</UseWPF></PropertyGroup>
  <ItemGroup><PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2027.*" /></ItemGroup>
</Project>
'@ | Set-Content "$probe/ApiProbe.csproj"
        foreach ($name in @('consulta', 'exportar-ifc')) {
            $snippet = Get-Content -Raw "server/src/duall/$name.ts"
            if ($snippet -notmatch '(?s)String\.raw`(.+)`;\s*$') { throw "Corpo C# ausente em $name" }
            $class = $name.Replace('-', '_')
            "using System; using System.Linq; using System.Collections.Generic; using Autodesk.Revit.DB; public static class $class { public static object Execute(Document document, object[] parameters) { $($Matches[1]) } }" | Set-Content "$probe/$class.cs"
        }
        dotnet build "$probe/ApiProbe.csproj" -c Release
        if ($LASTEXITCODE -ne 0) { throw 'A API 2027 recusou uma operação do Gemini.' }
    } finally {
        Remove-Item -Recurse -Force $probe
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
