#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { withRevitConnection } from "./utils/ConnectionManager.js";
import { consultaMEP } from "./duall/consulta.js";

const server = new McpServer({ name: "duall-revit-mep", version: "1.0.0" });
const filtros = {
  disciplina: z.enum(["todas", "eletrica", "hidraulica", "spk", "incendio", "revisar"]).default("todas"),
  nivel: z.string().default("").describe("Nome exato do nível. Vazio inclui todos."),
  sistema: z.string().default("").describe("Nome exato do tipo de sistema de tubulação. Vazio inclui todos."),
};
type Filtros = { disciplina: string; nivel: string; sistema: string };

async function consultar(modo: string, filtros: Filtros, pagina = 1, tamanho = 50) {
  try {
    const resposta = await withRevitConnection(client => client.sendCommand("send_code_to_revit", {
      code: consultaMEP,
      parameters: [modo, filtros.disciplina, filtros.nivel, filtros.sistema, pagina, tamanho],
      transactionMode: "none",
    }));
    if (resposta?.success !== true || typeof resposta.result !== "string") {
      throw new Error(resposta?.errorMessage || "Resposta inesperada do add-in.");
    }
    return { content: [{ type: "text" as const, text: resposta.result }] };
  } catch (error) {
    return { isError: true, content: [{ type: "text" as const, text: String(error) }] };
  }
}

server.tool("duall_quantitativos_mep", "Conte instâncias e some metros de trechos retos ou curvos modelados por disciplina, nível, sistema, categoria e tipo no documento ativo. Use revisar para itens sem classificação confiável.", filtros,
  args => consultar("resumo", args));

server.tool("duall_listar_mep", "Localize elementos MEP no documento ativo por ID, nível, sistema e classificação. A disciplina spk identifica bicos e itens marcados SPK. Redes de combate compartilhadas entram em incendio.", {
  ...filtros,
  pagina: z.number().int().min(1).max(1000000).default(1),
  tamanho: z.number().int().min(1).max(200).default(50),
}, args => consultar("lista", args, args.pagina, args.tamanho));

await server.connect(new StdioServerTransport());
