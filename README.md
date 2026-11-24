# Duall Engenharia · Gemini + Revit MEP

Perfil de consulta para elétrica, hidráulica, sprinklers e prevenção e combate a incêndio. O Gemini CLI inicia um servidor MCP local. O servidor consulta o documento aberto no Revit por meio do add-in deste fork.

## Instalação no Windows

1. Para Revit 2027, instale o SDK .NET 10 x64, Node.js 22 e o Gemini CLI no Windows. Use Revit 2027 atualizado (o SDK MCP referencia a API 2027.1 ou superior).
2. Compile o add-in e o servidor desta branch em PowerShell:

```powershell
git clone --branch duall/gemini-mep https://github.com/renanem/mcp-servers-for-revit.git
cd mcp-servers-for-revit
.\scripts\build-revit2027.ps1
# Com o Revit fechado, extraia Duall-Revit2027.zip em %APPDATA%\Autodesk\Revit\Addins\2027
cd server
npm ci
npm run build
gemini mcp add --scope user duall-revit node (Resolve-Path .\build\duall.js).Path
```

3. Abra um projeto no Revit. Nas configurações do add-in habilite `send_code_to_revit`, salve e ligue a conexão na faixa de opções. O servidor usa TCP local na porta 8080.
4. No mesmo Windows, execute `gemini` e confira as três ferramentas com `/mcp`. A autenticação é a habitual do Gemini CLI.

## Pedidos para o Gemini

- “No documento ativo, use duall_quantitativos_mep e liste os metros de eletrodutos e eletrocalhas por nível e tipo.”
- “Separe as tubulações hidráulicas por sistema, pavimento e diâmetro em milímetros.”
- “Conte os bicos de sprinkler por pavimento. Mostre separadamente as redes classificadas como incêndio.”
- “Liste os itens da disciplina revisar, com ID e valor de E3-Disciplina, em páginas de 50.”

## Como interpretar

As consultas leem instâncias no documento ativo, excluindo modelos vinculados. Os metros correspondem ao comprimento do eixo de elementos lineares MEP. Conexões, acessórios e equipamentos entram na contagem de peças. O resumo separa nível, sistema de tubulação, categoria, família e tipo. A listagem aceita filtros e paginação.

O parâmetro textual `E3-Disciplina` na instância ou no tipo tem prioridade quando seu valor é reconhecido. Na ausência de marca reconhecida, a consulta usa categorias nativas e classificação de sistema de tubulação. Bicos de sprinkler entram em SPK. Tubos de sistemas de proteção entram em incêndio, salvo marca explícita SPK. Equipamentos mecânicos e acessórios ambíguos aparecem em `revisar`. Famílias modeladas como modelos genéricos ficam fora da coleta. A consulta não verifica dimensionamento, cobertura, circuitos ou conformidade normativa.

O perfil expõe duas consultas e uma ferramenta de exportação IFC. As três executam um trecho C# fixo do repositório, passando filtros como dados. O servidor genérico original continua disponível em outro ponto de entrada. [Documentação original e créditos](README-upstream.md).

## Fluxos do perfil Duall

| Fluxo | Escopo implementado |
|---|---|
| Inventário e quantitativos | Lista e agrupa os elementos das categorias MEP atendidas no documento ativo. |
| IFC de envio | `duall_exportar_ifc` grava IFC4 ou IFC2x3 em pasta existente, com vista 3D opcional. Revisão de coordenadas, Psets, nomenclatura e validação do BEP continuam com a coordenação. |
| BEP para RVT | Sem importação de BEP, criação de RVT ou configuração de vínculos. |
| Comentários de cliente | É possível consultar elementos para investigar pendências. Sem sincronização de issues do ACC/BCF ou aplicação automática de correções. |
| Furação para markup | Sem detecção de interferências com estrutura vinculada, inserção de família FURO ou emissão de markup. |
| Lançamento MEP | As ferramentas do perfil consultam o que foi modelado. O perfil não posiciona pontos nem roteia redes. |

**Teste de aceitação no Revit 2027.** Abra uma cópia de projeto, ligue o serviço, consulte um nível conhecido e compare o resultado a uma tabela do Revit com os mesmos filtros. Exporte uma vista 3D conhecida e abra o IFC em um visualizador para conferir escopo, níveis e coordenadas. O build do add-in e a homologação no Revit são verificações distintas.
