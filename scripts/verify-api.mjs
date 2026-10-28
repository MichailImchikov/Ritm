import assert from 'node:assert/strict';
const base='http://127.0.0.1:5173';
const login=await fetch(base+'/signin-with-chatgpt?return_to=/',{redirect:'manual'});
const cookie=login.headers.getSetCookie().map(x=>x.split(';')[0]).join('; ');
assert.ok(cookie,'local preview sign-in cookie');
async function call(method,body,opts={}){
 const r=await fetch(base+'/api/tasks',{method,headers:{'Content-Type':'application/json',origin:base,...(opts.anon?{}:{cookie}),...opts.headers},...(body===undefined?{}:{body:JSON.stringify(body)})});
 const text=await r.text(); let data;try{data=JSON.parse(text);}catch{data={error:text};}return {status:r.status,data};
}
assert.equal((await call('GET',undefined,{anon:true})).status,401);
const fields={title:'API verification',description:'temporary local test',date:null,priority:2,category:'Работа',completed:false,subtasks:[{id:'step-1',title:'Verify sync',done:false}]};
assert.equal((await call('POST',fields,{headers:{origin:'https://other.test'}})).status,403);
assert.equal((await call('POST',{...fields,date:'2026-02-30'})).status,400);
assert.equal((await call('POST',{...fields,title:'  '})).status,400);
const made=await call('POST',fields);assert.equal(made.status,201);let task=made.data.task;
assert.equal(task.version,1);
assert.ok((await call('GET')).data.tasks.some(t=>t.id===task.id));
const first=task;
const changed=await call('PATCH',{...task,date:'2026-10-28',completed:true,subtasks:[{...fields.subtasks[0],done:true}]});
assert.equal(changed.status,200);task=changed.data.task;assert.equal(task.version,2);assert.equal(task.date,'2026-10-28');assert.equal(task.subtasks[0].done,true);
assert.equal((await call('PATCH',{...first,title:'stale write'})).status,409);
assert.equal((await call('DELETE',first)).status,409);
const other=await call('GET',undefined,{anon:true,headers:{'oai-authenticated-user-id':'other-local-test','oai-authenticated-user-email':'other@sites.test'}});
assert.equal(other.status,401,'preview must strip spoofed authentication headers');
assert.equal((await call('PATCH',task,{anon:true,headers:{'oai-authenticated-user-id':'other-local-test','oai-authenticated-user-email':'other@sites.test'}})).status,401);
assert.equal((await call('DELETE',task)).status,200);
assert.ok(!(await call('GET')).data.tasks.some(t=>t.id===task.id));
console.log('PASS: auth, origin, input validation, persistence, scheduling, subtasks, completion, version conflicts, spoofed-header rejection, deletion.');

