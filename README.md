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

3. Abra um projeto no Revit. Nas configurações do add-in habilite `duall_producao` e `send_code_to_revit`, salve e ligue a conexão na faixa de opções. O servidor usa TCP local na porta 8080.
4. No mesmo Windows, execute `gemini` e confira as ferramentas Duall com `/mcp`. A autenticação é a habitual do Gemini CLI.

## Pedidos para o Gemini

- “No documento ativo, use duall_quantitativos_mep e liste os metros de eletrodutos e eletrocalhas por nível e tipo.”
- “Separe as tubulações hidráulicas por sistema, pavimento e diâmetro em milímetros.”
- “Conte os bicos de sprinkler por pavimento. Mostre separadamente as redes classificadas como incêndio.”
- “Liste os itens da disciplina revisar, com ID e valor de E3-Disciplina, em páginas de 50.”

## Como interpretar

As consultas leem instâncias no documento ativo, excluindo modelos vinculados. Os metros correspondem ao comprimento do eixo de elementos lineares MEP. Conexões, acessórios e equipamentos entram na contagem de peças. O resumo separa nível, sistema de tubulação, categoria, família e tipo. A listagem aceita filtros e paginação.

O parâmetro textual `E3-Disciplina` na instância ou no tipo tem prioridade quando seu valor é reconhecido. Na ausência de marca reconhecida, a consulta usa categorias nativas e classificação de sistema de tubulação. Bicos de sprinkler entram em SPK. Tubos de sistemas de proteção entram em incêndio, salvo marca explícita SPK. Equipamentos mecânicos e acessórios ambíguos aparecem em `revisar`. Famílias modeladas como modelos genéricos ficam fora da coleta. A consulta não verifica dimensionamento, cobertura, circuitos ou conformidade normativa.

## Produção no Revit 2027

Instale o pacote desta revisão e habilite ambos os comandos nas configurações do add-in. No Gemini CLI indique o caminho de um BEP PDF, TXT ou MD de até 12 MiB para duall_ler_bep e informe os caminhos Windows do RTE, dos vínculos e do RVT de saída. O Gemini traduz o BEP em campos estruturados. Para regras de uma obra que o esquema não represente, peça ao Gemini uma lista de requisitos pendentes.

| Pedido no Gemini | Resultado e entrada |
|---|---|
| “Use este BEP para iniciar o RVT pelo nosso RTE.” | Cria novo RVT com níveis, plantas opcionais por tipo do template, vínculos RVT locais por origem ou coordenadas compartilhadas, posição do projeto e informações textuais. Aceita definições de parâmetros compartilhados com grupo, categorias e escopo de instância ou tipo. O destino precisa estar livre. |
| “Lance esses pontos com as famílias do template.” | Consulte o catálogo de famílias, níveis e vínculos. Coordenadas XYZ são metros a partir da origem interna. Para hospedagem em face, consulte as faces próximas e use a referência e direção retornadas. |
| “Modele esse caminho como rede SPK.” | Selecione tipo de tubo, sistema, nível, diâmetro e vértices. Suporta eletrodutos e eletrocalhas, cotas de caimento, conexões entre trechos e conexão a terminais coincidentes. O traçado e as dimensões vêm do projeto. |
| “Marque as travessias desses trechos no vínculo estrutural.” | Cruza os eixos com sólidos de vigas, pilares, paredes e pisos. Insere família FURO não hospedada no modelo de instalações, registra IDs e cria ou atualiza vista 3D para markup. A família pode receber profundidade e diâmetro com folga radial. A vista pode ser exportada em PDF de markup. A estrutura valida as reservas. |
| “Atenda esta pendência do cliente.” | Lê BCF 2.1 ou issues ACC. Mova instâncias ou edite seus parâmetros por ID. A resposta ao BCF gera outro arquivo. No ACC, responder e alterar o status são ações explícitas e separadas. |
| “Exporte a vista de entrega em IFC4.” | Indique documento, pasta, nome e ID da vista 3D. Revise o IFC produzido conforme o BEP. |

Para alterar o modelo ativo, as ferramentas começam em simulação. A resposta indica se o Revit reverteu o ensaio. Aplique com `simular=false` e uma `operacaoId` única por lançamento. Repetir a mesma operação aplicada devolve o resultado registrado no RVT. Os IDs criados na simulação são provisórios.

A integração ACC usa `APS_ACCESS_TOKEN` no ambiente do servidor MCP, com acesso ao projeto, permissões de leitura/escrita para a operação e token válido. O servidor faz chamadas ao projeto indicado. Renovação do token fica a cargo da integração APS da empresa.

**Aceite na obra piloto.** Use uma cópia do template e o vínculo estrutural real. Confira níveis e coordenadas, experimente uma família hospedada, conecte uma rede com curva, revise o marcador FURO em corte e reabra o IFC no visualizador da coordenação.
