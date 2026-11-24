# Duall Engenharia · Gemini + Revit MEP

Perfil de consulta para elétrica, hidráulica, sprinklers e prevenção e combate a incêndio. O Gemini CLI inicia um servidor MCP local. O servidor consulta o documento aberto no Revit por meio do add-in deste fork.

## Instalação no Windows

1. Instale Node.js 22 LTS, Gemini CLI e o add-in correspondente ao ano do Revit. O projeto de origem possui configurações até o Revit 2026. Veja [instalação e compilação do add-in](README-upstream.md). Use uma versão deste add-in com o comando `send_code_to_revit` e suporte a `transactionMode: none`.
2. Baixe esta versão e compile o servidor em PowerShell:

```powershell
git clone --branch duall/gemini-mep https://github.com/renanem/mcp-servers-for-revit.git
cd mcp-servers-for-revit/server
npm ci
npm run build
gemini mcp add --scope user duall-revit node (Resolve-Path .\build\duall.js).Path
```

3. Abra um projeto no Revit. Nas configurações do add-in habilite `send_code_to_revit`, salve e ligue a conexão na faixa de opções. O servidor usa TCP local na porta 8080.
4. No mesmo Windows, execute `gemini` e confira as duas ferramentas com `/mcp`. A autenticação é a habitual do Gemini CLI.

## Pedidos para o Gemini

- “No documento ativo, use duall_quantitativos_mep e liste os metros de eletrodutos e eletrocalhas por nível e tipo.”
- “Separe as tubulações hidráulicas por sistema, pavimento e diâmetro em milímetros.”
- “Conte os bicos de sprinkler por pavimento. Mostre separadamente as redes classificadas como incêndio.”
- “Liste os itens da disciplina revisar, com ID e valor de E3-Disciplina, em páginas de 50.”

## Como interpretar

As consultas leem instâncias no documento ativo, excluindo modelos vinculados. Os metros correspondem ao comprimento do eixo de elementos lineares MEP. Conexões, acessórios e equipamentos entram na contagem de peças. O resumo separa nível, sistema de tubulação, categoria, família e tipo. A listagem aceita filtros e paginação.

O parâmetro textual `E3-Disciplina` na instância ou no tipo tem prioridade quando seu valor é reconhecido. Na ausência de marca reconhecida, a consulta usa categorias nativas e classificação de sistema de tubulação. Bicos de sprinkler entram em SPK. Tubos de sistemas de proteção entram em incêndio, salvo marca explícita SPK. Equipamentos mecânicos e acessórios ambíguos aparecem em `revisar`. Famílias modeladas como modelos genéricos ficam fora da coleta. A consulta não verifica dimensionamento, cobertura, circuitos ou conformidade normativa.

O perfil expõe duas ferramentas de consulta. Ambas executam um trecho C# fixo do repositório, passando filtros como dados. O servidor genérico original continua disponível em outro ponto de entrada. [Documentação original e créditos](README-upstream.md).
