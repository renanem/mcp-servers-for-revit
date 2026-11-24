import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
const id=z.string().regex(/^[1-9][0-9]*$/);
const txt=z.string().min(1);
const xyz=z.tuple([z.number().finite(),z.number().finite(),z.number().finite()]).describe("XYZ em metros, origem interna do modelo");
const doc={documento:txt.describe("Título exato do documento ativo")};
const write={...doc,operacaoId:txt.describe("Chave estável deste lançamento. Reutilizar se repetir a mesma chamada."),simular:z.boolean().default(true).describe("true calcula e reverte; false grava a operação")};
async function call(acao:string,args:object){
  try {
    const r=await withRevitConnection(c=>c.sendCommand("duall_producao",{acao,...args}));
    if(!r?.success)throw new Error(r?.erro??"O comando duall_producao não retornou sucesso.");
    return {content:[{type:"text" as const,text:JSON.stringify(r.dados)}]};
  } catch(e){return {isError:true,content:[{type:"text" as const,text:String(e)}]};}
}
export function registerProduction(s:McpServer) {
  s.tool("duall_criar_projeto_bep","Leia o BEP anexado pelo usuário. Extraia os dados explícitos e use o RTE indicado. Cria e salva um novo RVT local. Confira requisitos fora do esquema com o projetista.",{
    fonteBep:txt,template:txt,destino:txt,
    niveis:z.array(z.object({nome:txt,elevacaoM:z.number(),planta:txt.optional().describe("Nome do tipo de planta do template")})),
    vinculos:z.array(z.object({arquivo:txt,posicao:z.enum(["origem","compartilhada"])})),
    coordenadas:z.object({lesteM:z.number(),norteM:z.number(),cotaM:z.number(),anguloGraus:z.number()}).optional(),
    informacoes:z.record(z.string()).describe("Parâmetros textuais existentes em Informações do projeto")
  },a=>call("projeto",a));
  s.tool("duall_catalogo","Consulte IDs, famílias, tipos, níveis, sistemas, vínculos e vistas disponíveis.",{
    grupo:z.enum(["familias","niveis","vinculos","tubos","sistemas","eletrodutos","eletrocalhas","vistas3d"]),busca:z.string().default(""),inicio:z.number().int().nonnegative().default(0)
  },a=>call("catalogo",a));
  s.tool("duall_faces","Localize a referência de face mais próxima para hospedar uma família, inclusive em vínculo.",{
    ...doc,hospedeiro:id,elementoVinculado:id.optional(),perto:xyz
  },a=>call("faces",a));
  s.tool("duall_lancar_pontos","Insira tomadas, luminárias, aparelhos e bicos SPK com a família da obra. Famílias de face usam referência e direção retornadas por duall_faces.",{
    ...write,pontos:z.array(z.object({tipo:id,nivel:id,xyz,hospedeiro:id.optional(),face:txt.optional(),direcao:xyz.optional(),giro:z.number().optional()})
      .refine(a=> !a.face || !!a.direcao,"Informe direção para a face")).min(1).max(200)
  },a=>call("pontos",a));
  s.tool("duall_lancar_rede","Lance percurso por vértices XYZ, incluindo cotas de caimento. Une trechos consecutivos. Para hidráulica ou SPK escolha o tipo de sistema da obra.",{
    ...write,classe:z.enum(["tubo","eletroduto","eletrocalha"]),nivel:id,tipo:id,sistema:id.optional(),vertices:z.array(xyz).min(2).max(200),
    diametroMm:z.number().positive().optional(),larguraMm:z.number().positive().optional(),alturaMm:z.number().positive().optional(),
    conectarInicio:id.optional(),conectarFim:id.optional()
  },a=>call("rede",a));
  s.tool("duall_marcar_furos","Cruze eixos dos trechos escolhidos com sólidos da estrutura vinculada. Insira família FURO no modelo de instalações e destaque as novas reservas em vista 3D para revisão pela estrutura.",{
    ...write,vinculo:id,tipoFuro:id,trechos:z.array(id).min(1).max(200),vista:txt,
    parametroDiametro:txt.optional(),parametroProfundidade:txt.optional(),folgaMm:z.number().nonnegative().describe("Folga radial definida para esta obra")
  },a=>call("furos",a));
  s.tool("duall_corrigir_pendencia","Aplique correções de cliente a instâncias identificadas. Mova por delta XYZ ou edite parâmetro de instância. Use a referência da issue na operação.",{
    ...write,pendencia:txt,alteracoes:z.array(z.object({id,deslocamento:xyz.optional(),parametro:txt.optional(),valor:z.string().optional()})
      .refine(a=>!!(a.deslocamento||a.parametro) && (a.parametro===undefined)===(a.valor===undefined),"Informe deslocamento ou parâmetro e valor")).min(1).max(200)
  },a=>call("corrigir",a));
  s.tool("duall_ler_bcf","Leia tópicos, comentários e componentes selecionados em BCF 2.1.",{arquivo:txt,topico:txt.optional()},a=>call("bcf",a));
  s.tool("duall_responder_bcf","Grave resposta e situação do tópico em uma nova cópia BCF 2.1.",{
    arquivo:txt,destino:txt,topico:z.string().uuid(),autor:txt,resposta:txt,status:txt.optional()
  },a=>call("bcf",a));
}
