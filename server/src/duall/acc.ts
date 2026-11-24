import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
const project=z.string().uuid().describe("UUID do projeto ACC sem prefixo b.");
const issue=z.string().uuid();
async function request(path:string,method="GET",body?:object) {
  try {
    const token=process.env.APS_ACCESS_TOKEN;
    if(!token)throw new Error("Configure APS_ACCESS_TOKEN no ambiente do Gemini com acesso ao projeto.");
    const res=await fetch("https://developer.api.autodesk.com/construction/issues/v1/projects/"+path,{
      method,headers:{Authorization:"Bearer "+token,"Content-Type":"application/json"},body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(30000)
    });
    const text=await res.text();
    if(!res.ok)throw new Error("ACC HTTP "+res.status+" "+text.slice(0,500));
    return {content:[{type:"text" as const,text:text||"Concluído"}]};
  }catch(e){return {isError:true,content:[{type:"text" as const,text:String(e)}]};}
}
export function registerAcc(s:McpServer){
  s.tool("duall_acc_issues","Liste issues paginadas do projeto ou leia os comentários da issue indicada.",{
    projeto:project,issue:issue.optional(),inicio:z.number().int().nonnegative().default(0)
  },a=>request(a.projeto+"/issues"+(a.issue?"/"+a.issue+"/comments":"")+"?limit=50&offset="+a.inicio));
  s.tool("duall_acc_responder","Publique a resposta da correção na issue indicada do cliente.",{projeto:project,issue,comentario:z.string().min(1)},
    a=>request(a.projeto+"/issues/"+a.issue+"/comments","POST",{body:a.comentario}));
  s.tool("duall_acc_atualizar_status","Atualize o status da issue conforme o fluxo do projeto ACC.",{projeto:project,issue,status:z.enum(["open","pending","in_review","closed"])},
    a=>request(a.projeto+"/issues/"+a.issue,"PATCH",{status:a.status}));
}
