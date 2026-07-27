(() => {
  'use strict';
  const $ = (id) => document.getElementById(id);
  const state = { csrf: null, status: null, snapshot: emptySnapshot(), backups: [], activity: [], timer: null };
  const pageTitles = {
    home:['FAMILY WORLD','Home'],players:['ACCESS','Players'],characters:['LIVES','Characters'],stewardship:['PRIVATE JOURNAL','Stewardship'],
    rewards:['ECONOMY','Rewards'],progress:['READ-ONLY','Progress'],world:['EXPLORATION','World'],minejammer:['STAGING','MineJammer'],
    settlements:['CIVIC WARDS','Settlements'],remote:['PRIVATE NETWORK','Remote access'],backups:['RECOVERY','Backups'],updates:['FROZEN WORLD','Updates'],
    activity:['AUDIT','Activity'],steward:['EXCEPTIONAL ACCESS','Steward']
  };

  async function request(path, options = {}) {
    const init = { credentials:'same-origin', headers:{Accept:'application/json'}, ...options };
    if (init.body && typeof init.body !== 'string') { init.headers['Content-Type']='application/json'; init.body=JSON.stringify(init.body); }
    if (init.method && init.method !== 'GET' && state.csrf) init.headers['X-MineDeck-CSRF']=state.csrf;
    const response = await fetch(path, init);
    if (response.status === 401) { lock(); throw new Error('MineDeck is locked.'); }
    const payload = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(payload.error || `Request failed (${response.status}).`);
    return payload;
  }

  async function authenticate() {
    try {
      const session = await request('/api/auth/session');
      state.csrf = session.csrfToken;
      unlock();
      await refreshAll();
    } catch (_) { lock(); }
  }

  function unlock() {
    $('loginLayer').hidden=true; $('appShell').hidden=false;
    clearInterval(state.timer); state.timer=setInterval(refreshStatus, 3000);
  }
  function lock() { $('loginLayer').hidden=false; $('appShell').hidden=true; state.csrf=null; clearInterval(state.timer); }

  $('loginForm').addEventListener('submit', async event => {
    event.preventDefault(); $('loginMessage').textContent='Checking…';
    try {
      const session=await request('/api/auth/login',{method:'POST',body:{password:$('password').value}});
      state.csrf=session.csrfToken; $('password').value=''; $('loginMessage').textContent=''; unlock(); await refreshAll();
    } catch (error) { $('loginMessage').textContent=error.message==='MineDeck is locked.'?'Password was not accepted.':error.message; }
  });

  $('logoutButton').addEventListener('click', async () => { try { await request('/api/auth/logout',{method:'POST'}); } finally { lock(); } });
  $('refreshButton').addEventListener('click', refreshAll);
  $('nav').addEventListener('click', event => {
    const button=event.target.closest('[data-page]'); if(!button)return;
    document.querySelectorAll('#nav button').forEach(x=>x.classList.toggle('active',x===button));
    document.querySelectorAll('.page').forEach(x=>x.classList.toggle('active',x.id===`page-${button.dataset.page}`));
    [$('pageEyebrow').textContent,$('pageTitle').textContent]=pageTitles[button.dataset.page];
  });

  document.body.addEventListener('click', async event => {
    const action=event.target.closest('[data-action]')?.dataset.action; if(!action)return;
    const map={
      'server-start':['/api/server/start','POST',null],
      'server-stop':['/api/server/stop','POST','Stop MineScape cleanly? Players will be disconnected after Bridge saves.'],
      'jammer-start':['/api/minejammer/start','POST',null],
      'jammer-stop':['/api/minejammer/stop','POST','Stop MineJammer cleanly?']
    };
    const [path,method,warning]=map[action]; if(warning&&!confirm(warning))return;
    await runMutation(event.target,path,method,{});
  });

  async function refreshAll() {
    const results=await Promise.allSettled([
      request('/api/status'),request('/api/snapshot'),request('/api/backups'),request('/api/local-activity'),request('/api/updates'),request('/api/remote')
    ]);
    if(results[0].status==='fulfilled') state.status=results[0].value.data;
    if(results[1].status==='fulfilled'&&results[1].value.ok) state.snapshot=results[1].value.data;
    else state.snapshot=emptySnapshot();
    if(results[2].status==='fulfilled') state.backups=results[2].value.data||[];
    if(results[3].status==='fulfilled') state.activity=results[3].value.data||[];
    renderStatus(); renderSnapshot(); renderBackups(); renderActivity();
    if(results[4].status==='fulfilled') renderUpdates(results[4].value.data);
    if(results[5].status==='fulfilled') renderRemote(results[5].value.data);
    const bridgeError=results[1].status==='fulfilled' ? results[1].value.error : results[1].reason?.message;
    const gaps=state.snapshot.unsupportedCapabilities||[];
    showNotice(bridgeError ? `Bridge adapter unavailable: ${bridgeError} Read-only process health still works; world mutations remain disabled.` : gaps.length?`Bridge connected. Current Bridge does not yet expose: ${gaps.join(', ')}. Those controls fail closed.`:null);
  }

  async function refreshStatus(){ try{const result=await request('/api/status');state.status=result.data;renderStatus();}catch(_){} }

  function renderStatus(){
    if(!state.status)return; const s=state.status, lifecycle=String(s.state).toLowerCase();
    const names={0:'offline',1:'starting',2:'online',3:'stopping',4:'maintenance',5:'fault',offline:'offline',starting:'starting',online:'online',stopping:'stopping',maintenance:'maintenance',fault:'fault'};
    const name=names[lifecycle]||'offline', pill=$('statusPill'); pill.className=`status-pill ${name==='online'?'online':(['starting','stopping','maintenance'].includes(name)?'busy':name==='fault'?'fault':'offline')}`;
    pill.innerHTML=`<i></i>${esc(title(name))}${s.mineJammer?.online?' · J':''}`;
    $('heroState').textContent=name==='online'?'MineScape is online':name==='fault'?'MineScape needs attention':title(name);
    $('heroMessage').textContent=s.message;
    $('jammerState').textContent=s.mineJammer?.online?`Online · ${s.mineJammer.onlinePlayers}/${s.mineJammer.maxPlayers} · ${s.mineJammer.latencyMilliseconds} ms`:'Offline';
    $('homeMetrics').innerHTML=[
      metric('Minecraft ping',s.production?.online?`${s.production.latencyMilliseconds} ms`:'No response',s.production?.version||'Real Server List Ping'),
      metric('Players',s.production?.online?`${s.production.onlinePlayers} / ${s.production.maxPlayers}`:'—','Online now'),
      metric('Last backup',s.lastBackupAt?relative(s.lastBackupAt):'None yet','Verified archive'),
      metric('Free disk',bytes(s.freeDiskBytes),'Host data volume')].join('');
    $('healthList').innerHTML=[
      health('Minecraft protocol',s.production?.online?'Responding':'Offline',s.production?.online?'good':''),
      health('MineScape Bridge',s.bridgeAvailable?'Connected':s.bridgeMessage||'Unavailable',s.bridgeAvailable?'good':'warn'),
      health('Manifest',s.manifestState||'Unavailable',s.manifestState?.startsWith('Verified')?'good':'warn'),
      health('MineJammer',s.mineJammer?.online?'Online · J':'Offline',s.mineJammer?.online?'good':'')].join('');
  }

  function renderSnapshot(){
    const s=state.snapshot||emptySnapshot();
    $('pendingPlayers').innerHTML=tableOrEmpty(s.pendingPlayers, ['Name / UUID','Requested','Expires','Decision'], p=>`<tr><td><strong>${esc(p.currentName)}</strong><br><code>${esc(p.uuid)}</code></td><td>${date(p.requestedAt)}</td><td>${relative(p.expiresAt)}</td><td class="mini-actions"><button onclick="MineDeck.approve('${escAttr(p.uuid)}')">Approve</button><button class="danger-outline" onclick="MineDeck.deny('${escAttr(p.uuid)}')">Deny</button></td></tr>`,'No authenticated player is waiting for approval.');
    $('enrolledPlayers').innerHTML=tableOrEmpty(s.players,['Player','Role','Access','Session',''],p=>`<tr><td><strong>${esc(p.currentName)}</strong><br><code>${esc(p.uuid)}</code></td><td>${esc(p.role)}</td><td>${esc(p.accessState)}</td><td>${esc(p.sessionMode)}${p.countsTowardFamilyExploration?' · fog counts':''}</td><td><button class="danger-outline" onclick="MineDeck.revoke('${escAttr(p.uuid)}')">Revoke</button></td></tr>`,'No enrolled players yet.');
    $('charactersGrid').innerHTML=s.characters?.length?s.characters.map(characterCard).join(''):empty('Character state will appear after Bridge connects.');
    $('stewardshipEvents').innerHTML=events(s.stewardship,e=>[date(e.at),e.kind,e.directCause,e.confidence]);
    $('rewardEvents').innerHTML=events(s.rewards,e=>[date(e.at),e.kind,`${e.quantity} · ${e.source}`,e.state]);
    $('progressGrid').innerHTML=s.progress?.length?s.progress.map(progressCard).join(''):empty('No advancement snapshot is available. The in-game chart remains authoritative.');
    $('fogGrid').innerHTML=s.fog?.length?s.fog.map(fogCard).join(''):['Currently visible','Previously explored','Never explored'].map((x,i)=>`<div class="data-card"><div class="fog-swatch ${['now','past','never'][i]}"></div><h3>${x}</h3><p>Waiting for Bridge telemetry</p></div>`).join('');
    $('lootEvents').innerHTML=tableOrEmpty(s.loot,['Resolved','Structure','Original roll','Matcha enrichment','Location'],e=>`<tr><td>${date(e.at)}</td><td>${esc(e.structure)}</td><td><code>${esc(e.originalTable)}</code></td><td><code>${esc(e.matchaBonusTable||'none')}</code></td><td>${esc(e.dimension)} · ${e.x}, ${e.y}, ${e.z}</td></tr>`,'No eligible container has been resolved yet.');
    $('settlementsGrid').innerHTML=s.settlements?.length?s.settlements.map(x=>`<div class="data-card"><span class="tag">${esc(x.state)}</span><h3>${esc(x.name)}</h3><p>${esc(x.dimension)} · ${x.centerX}, ${x.centerZ}</p><strong>${x.civicWardCount}</strong> civic wards · ${x.buildingCount} buildings<br><small class="muted">${esc(x.source)}</small></div>`).join(''):empty('No settlement registry snapshot is available.');
  }

  function renderBackups(){ $('backupsList').innerHTML=tableOrEmpty(state.backups,['Created','Archive','Size','World epoch','Integrity'],b=>`<tr><td>${date(b.createdAt)}</td><td><code>${esc(b.fileName)}</code><br><small>${esc(b.reason)}</small></td><td>${bytes(b.bytes)}</td><td>${esc(b.worldEpoch)}</td><td><button onclick="MineDeck.verifyBackup('${escAttr(b.id)}')">Verify SHA-256</button></td></tr>`,'No verified recovery point has been recorded.'); }
  function renderActivity(){ const merged=[...(state.snapshot.activity||[]),...state.activity].sort((a,b)=>String(b.at).localeCompare(String(a.at)));$('activityEvents').innerHTML=events(merged,e=>[date(e.at),e.kind,e.summary,e.outcome]); }
  function renderUpdates(u){$('updatesGrid').innerHTML=Object.entries(u||{}).map(([k,v])=>`<div class="data-card"><h3>${esc(words(k))}</h3><p class="large">${typeof v==='boolean'?(v?'Yes':'No'):esc(v)}</p></div>`).join('');}
  function renderRemote(r){$('remoteMetrics').innerHTML=[metric('Tailscale adapter',r?.adapterAvailable?'Connected':'Not configured',r?.message||''),metric('Public port',r?.publicPortOpen?'Open — investigate':'Closed','Funnel must remain disabled'),metric('Dashboard bind','Loopback only','Remote access through Tailscale Serve')].join('');}

  $('backupForm').addEventListener('submit',async e=>{e.preventDefault();const f=new FormData(e.target);await runMutation(e.submitter,'/api/backups/create','POST',{reason:f.get('reason')},true);});
  $('whitelistForm').addEventListener('submit',async e=>{e.preventDefault();const f=new FormData(e.target);await runMutation(e.submitter,'/api/players/approve','POST',{uuid:f.get('uuid'),role:'Not assigned',note:'UUID entered directly in MineDeck'},true);});
  $('assistForm').addEventListener('submit',async e=>{e.preventDefault();const f=new FormData(e.target);await runMutation(e.submitter,'/api/family-assist','POST',{playerId:f.get('playerId'),action:f.get('action'),reason:f.get('reason')},true);});
  $('settlementForm').addEventListener('submit',async e=>{e.preventDefault();const f=new FormData(e.target);await runMutation(e.submitter,'/api/settlements/register','POST',{name:f.get('name'),dimension:f.get('dimension'),minX:+f.get('minX'),minZ:+f.get('minZ'),maxX:+f.get('maxX'),maxZ:+f.get('maxZ')},true);});
  $('rewardForm').addEventListener('submit',async e=>{e.preventDefault();if(!confirm('Submit this narrowly allowlisted correction? It will be permanently audited.'))return;const f=new FormData(e.target);await runMutation(e.submitter,'/api/rewards/correct','POST',{password:f.get('password'),request:{playerUuid:f.get('playerUuid'),generation:+f.get('generation'),item:'matcha:divine_fragment',quantity:1,reason:f.get('reason'),evidence:f.get('evidence')}},true);e.target.querySelector('[name=password]').value='';});
  $('stewardForm').addEventListener('submit',async e=>{e.preventDefault();if(!confirm('Arm a temporary Steward session? Creative is exceptional and fully audited.'))return;const f=new FormData(e.target);await runMutation(e.submitter,'/api/steward/lease','POST',{password:f.get('password'),request:{playerUuid:f.get('playerUuid'),minutes:+f.get('minutes'),reason:f.get('reason'),creative:f.get('creative')==='on'}},true);e.target.querySelector('[name=password]').value='';});
  $('endSteward').addEventListener('click',e=>runMutation(e.target,'/api/steward/end','POST',{},true));
  $('promotionForm').addEventListener('submit',async e=>{e.preventDefault();if(!confirm('Commit this candidate? Bridge must pass every backup, fog, hash, and bounds gate.'))return;await promotion(e.submitter,'/api/minejammer/commit');});
  $('comparePromotion').addEventListener('click',e=>promotion(e.target,'/api/minejammer/compare'));
  async function promotion(button,path){const f=new FormData($('promotionForm')),request={reservationId:f.get('reservationId'),candidateId:f.get('candidateId'),reason:f.get('reason'),expectedProductionHash:f.get('expectedProductionHash')};if(path.endsWith('/commit')&&!f.get('password')){toast('Administrator password is required to commit.',true);return;}await runMutation(button,path,'POST',path.endsWith('/commit')?{password:f.get('password'),request}:request,true);$('promotionForm').querySelector('[name=password]').value='';}

  async function runMutation(button,path,method,body,refresh=true){
    const prior=button.disabled;button.disabled=true;
    try{const result=await request(path,{method,body});if(result.accepted===false||result.ok===false)throw new Error(result.message||result.error||'Operation was rejected.');toast(result.message||'Operation accepted.');if(refresh)await refreshAll();else await refreshStatus();}
    catch(error){toast(error.message,true);}finally{button.disabled=prior;}
  }

  window.MineDeck={
    approve:async uuid=>{if(!confirm('Record this UUID as a whitelist intention? Current Bridge role/native-sync gates remain visible.'))return;await runMutation(document.body,'/api/players/approve','POST',{uuid,role:'Not assigned',note:'Approved in MineDeck'},true);},
    deny:async uuid=>{if(confirm('Deny this pending request?'))await runMutation(document.body,'/api/players/deny','POST',{uuid,note:'Denied in MineDeck'},true);},
    revoke:async uuid=>{if(confirm('Revoke this UUID from the whitelist?'))await runMutation(document.body,'/api/players/revoke','POST',{uuid,note:'Revoked in MineDeck'},true);},
    verifyBackup:async id=>await runMutation(document.body,`/api/backups/${encodeURIComponent(id)}/verify`,'POST',{},true),
    pardon:async (uuid,generation)=>{const input=await sensitiveInput('Technical pardon','Evidence for the technical or administrative error',true);if(!input)return;await runMutation(document.body,'/api/characters/pardon','POST',{password:input.password,request:{playerUuid:uuid,generation,reason:'technical-error',evidence:input.note}},true);},
    newCharacter:async (uuid,generation)=>{const input=await sensitiveInput('Begin the next character','The retired character remains archived. Add a parent review note.',true);if(!input||!confirm('Begin a new character at ten usable hearts and the public village spawn?'))return;await runMutation(document.body,'/api/characters/new','POST',{password:input.password,request:{playerUuid:uuid,retiredGeneration:generation,reason:input.note}},true);}
  };

  function sensitiveInput(title,noteLabel,noteRequired){
    return new Promise(resolve=>{
      const dialog=$('sensitiveDialog'),form=$('sensitiveForm'),password=$('sensitivePassword'),note=$('sensitiveNote');
      $('sensitiveTitle').textContent=title;$('sensitiveNoteLabel').firstChild.textContent=noteLabel;note.required=noteRequired;note.value='';password.value='';
      const cleanup=value=>{form.onsubmit=null;$('sensitiveCancel').onclick=null;dialog.oncancel=null;dialog.close();resolve(value);};
      form.onsubmit=e=>{e.preventDefault();cleanup({password:password.value,note:note.value});};
      $('sensitiveCancel').onclick=()=>cleanup(null);dialog.oncancel=e=>{e.preventDefault();cleanup(null);};dialog.showModal();password.focus();
    });
  }

  function characterCard(c){const hearts=[];for(let i=0;i<30;i++)hearts.push(`<i class="heart ${i<c.capacity?(i>=c.capacity-c.blocked?'blocked':''):'locked'}"></i>`);return `<div class="data-card"><span class="tag ${c.state!=='Active'?'quiet':''}">${esc(c.state)}</span><h3>${esc(c.playerName)} · generation ${c.generation}</h3><div class="heart-row">${hearts.join('')}</div><p class="heart-note">${c.usable} usable · ${c.blocked} black · ${c.capacity} capacity</p>${c.state==='RetirementPending'?`<div class="mini-actions"><button onclick="MineDeck.pardon('${escAttr(c.playerUuid)}',${c.generation})">Technical pardon</button><button class="danger-outline" onclick="MineDeck.newCharacter('${escAttr(c.playerUuid)}',${c.generation})">Begin next character</button></div>`:''}</div>`;}
  function progressCard(p){const ratio=p.total?Math.round(p.completed/p.total*100):0;return `<div class="data-card"><h3>${esc(p.playerName)}</h3><p class="large">${p.completed} / ${p.total}</p><div class="progress-bar"><i style="width:${ratio}%"></i></div>${(p.roots||[]).map(r=>`<p>${esc(r.title)} <strong>${r.completed}/${r.total}</strong></p>`).join('')}</div>`;}
  function fogCard(f){return `<div class="data-card"><h3>${esc(f.dimension)}</h3><p><strong class="large">${number(f.currentlyVisibleChunks)}</strong> visible now</p><p>${number(f.previouslyExploredChunks)} remembered · ${number(f.neverExploredChunks)} unknown</p><small class="muted">Updated ${relative(f.updatedAt)}</small></div>`;}
  function metric(label,value,note){return `<div class="metric"><span>${esc(label)}</span><strong>${esc(value)}</strong><small>${esc(note)}</small></div>`;}
  function health(label,value,cls=''){return `<div class="health-item"><span>${esc(label)}</span><strong class="${cls}">${esc(value)}</strong></div>`;}
  function tableOrEmpty(items,heads,row,emptyText){return items?.length?`<table><thead><tr>${heads.map(h=>`<th>${esc(h)}</th>`).join('')}</tr></thead><tbody>${items.map(row).join('')}</tbody></table>`:empty(emptyText);}
  function events(items,values){return items?.length?items.map(x=>`<div class="event">${values(x).map((v,i)=>i===0?`<time>${esc(v)}</time>`:i===3?`<small>${esc(v)}</small>`:`<span>${esc(v)}</span>`).join('')}</div>`).join(''):empty('No events recorded.');}
  function empty(message){return `<div class="empty">${esc(message)}</div>`;}
  function emptySnapshot(){return{pendingPlayers:[],players:[],characters:[],fog:[],settlements:[],progress:[],loot:[],stewardship:[],rewards:[],activity:[],unsupportedCapabilities:[]};}
  function showNotice(message){$('globalNotice').hidden=!message;$('globalNotice').textContent=message||'';}
  function toast(message,error=false){const t=$('toast');t.textContent=message;t.className=`toast${error?' error':''}`;t.hidden=false;setTimeout(()=>t.hidden=true,5000);}
  function date(v){if(!v)return'—';return new Date(v).toLocaleString();}
  function relative(v){if(!v)return'—';const seconds=Math.round((new Date(v)-Date.now())/1000),abs=Math.abs(seconds);if(abs<60)return seconds<0?'just now':'in moments';const unit=abs<3600?['minute',60]:abs<86400?['hour',3600]:['day',86400];const n=Math.round(abs/unit[1]);return seconds<0?`${n} ${unit[0]}${n===1?'':'s'} ago`:`in ${n} ${unit[0]}${n===1?'':'s'}`;}
  function bytes(v){if(!v)return'—';const units=['B','KB','MB','GB','TB'];let i=0,n=v;while(n>=1024&&i<units.length-1){n/=1024;i++;}return`${n.toFixed(i>1?1:0)} ${units[i]}`;}
  function number(v){return new Intl.NumberFormat().format(v||0);}
  function title(v){return String(v).replace(/(^|\s)\S/g,x=>x.toUpperCase());}
  function words(v){return title(String(v).replace(/([A-Z])/g,' $1'));}
  function esc(v){return String(v??'').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));}
  function escAttr(v){return esc(v).replace(/`/g,'&#96;');}
  authenticate();
})();
