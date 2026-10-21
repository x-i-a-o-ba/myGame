import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { readFile, readdir, mkdir, writeFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createInterface } from 'node:readline';

const dashboardDir = dirname(fileURLToPath(import.meta.url));
const projectDir = resolve(dashboardDir, '..', '..');
const configuredPort = Number(process.env.PORT || 4177);
if (!Number.isInteger(configuredPort) || configuredPort < 1024 || configuredPort > 65535) {
  throw new Error('PORT must be an integer between 1024 and 65535.');
}
const port = configuredPort;
const localDataDir = join(
  process.env.LOCALAPPDATA || join(homedir(), '.local', 'share'),
  'myGame',
  'AiTeamDashboard'
);
const stateFile = join(localDataDir, 'state.json');
const staticFiles = new Map([
  ['/', ['index.html', 'text/html; charset=utf-8']],
  ['/index.html', ['index.html', 'text/html; charset=utf-8']],
  ['/app.css', ['app.css', 'text/css; charset=utf-8']],
  ['/app.js', ['app.js', 'text/javascript; charset=utf-8']]
]);
const childSourceKinds = ['subAgent', 'subAgentReview', 'subAgentCompact', 'subAgentThreadSpawn', 'subAgentOther'];
const maxBodyBytes = 16 * 1024;
const maxFeedItems = 600;
const maxThreadItems = 350;
const maxMessageChars = 60000;
const sseClients = new Set();
const threads = new Map();
const feed = [];
let roles = [];
let run = null;
let ready = false;
let error = null;
let busy = false;
let closing = false;
let broadcastTimer = null;
let childRefreshActive = false;
let childRefreshTimer = null;
let rpcNextId = 1;
const pendingRpc = new Map();

const coordinatorInstructions = [
  '你是本项目 AI 团队的主 AI，也是唯一的派工、集成和汇报负责人。',
  '遵守 AGENTS.md、Tools/Skills/unity-requirements-first/SKILL.md、Docs/README.md 和 Docs/ai-team/README.md。',
  '根据用户目标自主确定调查范围，按任务需要选择可用工具和协作方式。',
  '每个子代理收到单独目标、输入路径、交接格式和不可写入的边界。所有子代理完成后，你核对他们引用的实际证据、解决交接冲突，必要时让相应代理补查。',
  '普通玩法、体验和验收细节由团队讨论后由你裁决；只把确实无法推断或必须由用户决定的事项列为待确认。',
  '本面板新建任务使用只读权限，只能调查与起草；不得把尚未实施或运行的功能称为已完成或验证通过。',
  '项目功能实施后，由可写的 Codex 原生主任务自行安排适用的 Unity 编译、Editor/Player 运行与功能检查，发现问题先返工并复验；可按需派测试代理，用户试玩仅补充体验反馈。',
  '最终向用户报告：任务拆分、各代理结果、你的验收裁决、事实与推断、未验证处、需要用户决定的事项和下一步。没有完成的实现不能称为已完成。'
].join('\n');

function trimText(value, limit = maxMessageChars) {
  const text = typeof value === 'string' ? value : '';
  return text.length <= limit ? text : text.slice(0, limit) + '\n…（面板显示已截断）';
}

function sameProjectCwd(value) {
  if (typeof value !== 'string' || value.length === 0) return false;
  const normalized = resolve(value);
  return process.platform === 'win32'
    ? normalized.toLowerCase() === projectDir.toLowerCase()
    : normalized === projectDir;
}

async function readRoles() {
  const agentDir = join(projectDir, '.codex', 'agents');
  let names;
  try {
    names = (await readdir(agentDir)).filter((name) => name.endsWith('.toml'));
  } catch {
    return [];
  }
  const found = await Promise.all(names.map(async (filename) => {
    try {
      const source = await readFile(join(agentDir, filename), 'utf8');
      const name = source.match(/^name\s*=\s*"([^"]+)"/m)?.[1];
      const description = source.match(/^description\s*=\s*"([^"]+)"/m)?.[1];
      return name ? { name, description: description || '' } : null;
    } catch {
      return null;
    }
  }));
  return found.filter(Boolean).sort((a, b) => a.name.localeCompare(b.name));
}

function snapshot() {
  return {
    ready,
    error,
    run: run ? { ...run } : null,
    threads: Array.from(threads.values()).map((thread) => ({
      id: thread.id,
      parentThreadId: thread.parentThreadId,
      agentRole: thread.agentRole,
      agentNickname: thread.agentNickname,
      name: thread.name,
      status: thread.status,
      items: thread.items.map((item) => ({ ...item }))
    })),
    feed: feed.map((entry) => ({ ...entry })),
    roles: roles.map((role) => ({ ...role }))
  };
}

function writeSse(client, payload) {
  try {
    client.write('data: ' + JSON.stringify(payload) + '\n\n');
  } catch {
    sseClients.delete(client);
  }
}

function broadcastNow() {
  broadcastTimer = null;
  if (sseClients.size === 0) return;
  const current = snapshot();
  for (const client of sseClients) writeSse(client, current);
}

function broadcastSoon() {
  if (broadcastTimer) return;
  broadcastTimer = setTimeout(broadcastNow, 80);
}

function ensureThread(id) {
  if (!id) return null;
  if (!threads.has(id)) {
    threads.set(id, {
      id,
      parentThreadId: null,
      agentRole: null,
      agentNickname: null,
      name: null,
      status: 'unknown',
      items: []
    });
  }
  return threads.get(id);
}

function applyThreadMetadata(raw) {
  if (!raw?.id) return null;
  const thread = ensureThread(raw.id);
  if (raw.parentThreadId !== undefined) thread.parentThreadId = raw.parentThreadId;
  if (raw.agentRole !== undefined) thread.agentRole = raw.agentRole;
  if (raw.agentNickname !== undefined) thread.agentNickname = raw.agentNickname;
  if (raw.name !== undefined) thread.name = raw.name;
  if (raw.status?.type) thread.status = raw.status.type;
  broadcastSoon();
  return thread;
}

function actorFor(threadId, type) {
  if (type === 'userMessage') return '你';
  if (threadId === run?.rootThreadId) return '主 AI';
  const thread = threads.get(threadId);
  return thread?.agentRole || thread?.agentNickname || thread?.name || '子代理';
}

function normalizeItem(item) {
  if (!item || typeof item.id !== 'string' || typeof item.type !== 'string') return null;
  const base = {
    id: item.id,
    type: item.type,
    text: '',
    phase: null,
    prompt: null,
    tool: null,
    status: null,
    receiverThreadIds: [],
    command: null,
    aggregatedOutput: null
  };
  switch (item.type) {
    case 'userMessage':
      base.text = trimText((item.content || [])
        .filter((part) => part?.type === 'text')
        .map((part) => part.text || '')
        .join('\n'));
      break;
    case 'agentMessage':
      base.text = trimText(item.text);
      base.phase = item.phase || null;
      break;
    case 'collabAgentToolCall':
      base.prompt = trimText(item.prompt);
      base.tool = item.tool || null;
      base.status = item.status || null;
      base.receiverThreadIds = Array.isArray(item.receiverThreadIds)
        ? item.receiverThreadIds.filter((id) => typeof id === 'string')
        : [];
      break;
    case 'commandExecution':
      base.command = trimText(item.command, 5000);
      base.aggregatedOutput = trimText(item.aggregatedOutput, 8000);
      base.status = item.status || null;
      break;
    case 'plan':
      base.text = trimText(item.text);
      break;
    default:
      return null;
  }
  return base;
}

function upsertFeed(threadId, item, at) {
  let kind;
  let text;
  if (item.type === 'userMessage') {
    kind = 'userMessage';
    text = item.text;
  } else if (item.type === 'agentMessage') {
    kind = 'agentMessage';
    text = item.text;
  } else if (item.type === 'collabAgentToolCall') {
    kind = 'collabAgentToolCall';
    text = item.prompt || item.tool || '';
  } else {
    return;
  }
  const id = 'item:' + threadId + ':' + item.id;
  const existing = feed.find((entry) => entry.id === id);
  const next = {
    id,
    threadId,
    kind,
    text,
    actor: actorFor(threadId, item.type),
    at: existing?.at || at || new Date().toISOString(),
    status: item.status || null,
    phase: item.phase || null,
    tool: item.tool || null
  };
  if (existing) Object.assign(existing, next);
  else feed.push(next);
  if (feed.length > maxFeedItems) feed.splice(0, feed.length - maxFeedItems);
}

function upsertItem(threadId, rawItem, at, source = 'event') {
  const item = normalizeItem(rawItem);
  if (!item) return;
  const thread = ensureThread(threadId);
  const index = thread.items.findIndex((existing) => existing.id === item.id);
  if (index >= 0) {
    const existing = thread.items[index];
    if (source === 'history' && existing.text.length > item.text.length) {
      item.text = existing.text;
    }
    thread.items[index] = item;
  } else {
    thread.items.push(item);
    if (thread.items.length > maxThreadItems) thread.items.shift();
  }
  upsertFeed(threadId, item, at);
  if (item.type === 'collabAgentToolCall') {
    for (const childId of item.receiverThreadIds) {
      const child = ensureThread(childId);
      if (child && !child.parentThreadId) child.parentThreadId = threadId;
    }
    if (source === 'event' && item.receiverThreadIds.length > 0) {
      setTimeout(() => {
        refreshChildren().catch((cause) => console.error('Child refresh:', cause.message));
      }, 400);
    }
  }
  broadcastSoon();
}

function addTurnFeed(threadId, turn, status) {
  if (!turn?.id) return;
  const id = 'turn:' + threadId + ':' + turn.id;
  const existing = feed.find((entry) => entry.id === id);
  const entry = {
    id,
    threadId,
    kind: 'turnStatus',
    text: status,
    actor: actorFor(threadId, 'turn'),
    at: existing?.at || new Date().toISOString(),
    status
  };
  if (existing) Object.assign(existing, entry);
  else feed.push(entry);
  if (feed.length > maxFeedItems) feed.splice(0, feed.length - maxFeedItems);
  broadcastSoon();
}

function hydrateFromThread(raw) {
  const thread = applyThreadMetadata(raw);
  if (!thread) return;
  const turns = Array.isArray(raw.turns) ? raw.turns.slice(-40) : [];
  for (const turn of turns) {
    const at = turn.startedAt
      ? new Date(turn.startedAt * 1000).toISOString()
      : new Date(raw.createdAt * 1000).toISOString();
    for (const item of (turn.items || []).slice(-maxThreadItems)) {
      upsertItem(raw.id, item, at, 'history');
    }
  }
}

async function persistRun() {
  if (!run?.rootThreadId) return;
  try {
    await mkdir(localDataDir, { recursive: true });
    await writeFile(stateFile, JSON.stringify({
      rootThreadId: run.rootThreadId,
      goal: run.goal,
      status: run.status,
      turnId: run.turnId
    }, null, 2), { encoding: 'utf8', mode: 0o600 });
  } catch (cause) {
    console.error('Could not save dashboard state:', cause.message);
  }
}

class RpcError extends Error {
  constructor(message, code) {
    super(message);
    this.code = code;
  }
}

const codex = spawn(
  process.platform === 'win32' ? 'codex.exe' : 'codex',
  ['app-server', '--stdio', '--strict-config'],
  { cwd: projectDir, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true }
);

function rpcNotify(method, params) {
  if (!codex.stdin.writable) throw new RpcError('Codex app-server is not running.');
  codex.stdin.write(JSON.stringify({ method, params }) + '\n');
}

function rpcRequest(method, params, timeoutMs = 30000) {
  if (!codex.stdin.writable) return Promise.reject(new RpcError('Codex app-server is not running.'));
  const id = rpcNextId++;
  return new Promise((resolvePromise, rejectPromise) => {
    const timer = setTimeout(() => {
      pendingRpc.delete(id);
      rejectPromise(new RpcError(method + ' timed out.'));
    }, timeoutMs);
    pendingRpc.set(id, { resolvePromise, rejectPromise, timer });
    try {
      codex.stdin.write(JSON.stringify({ method, id, params }) + '\n');
    } catch (cause) {
      clearTimeout(timer);
      pendingRpc.delete(id);
      rejectPromise(cause);
    }
  });
}

function handleRpcMessage(message) {
  if (message.id !== undefined && message.method === undefined) {
    const pending = pendingRpc.get(message.id);
    if (!pending) return;
    clearTimeout(pending.timer);
    pendingRpc.delete(message.id);
    if (message.error) {
      pending.rejectPromise(new RpcError(
        message.error.message || 'Codex RPC request failed.',
        message.error.code
      ));
    } else {
      pending.resolvePromise(message.result);
    }
    return;
  }
  if (message.id !== undefined && message.method) {
    // This read-only dashboard does not expose approvals, shell commands, or
    // arbitrary app-server requests to the browser.
    codex.stdin.write(JSON.stringify({
      id: message.id,
      error: {
        code: -32601,
        message: 'This dashboard cannot answer interactive server requests.'
      }
    }) + '\n');
    error = 'Codex 请求了面板暂不支持的交互输入；请查看主 AI 的报告。';
    broadcastSoon();
    return;
  }
  if (message.method) {
    handleNotification(message.method, message.params || {});
  }
}

function handleNotification(method, params) {
  if (method === 'thread/started') {
    applyThreadMetadata(params.thread);
    return;
  }
  if (method === 'thread/name/updated') {
    const thread = ensureThread(params.threadId);
    if (thread) thread.name = params.threadName || null;
    broadcastSoon();
    return;
  }
  if (method === 'thread/status/changed') {
    const thread = ensureThread(params.threadId);
    if (thread) thread.status = params.status?.type || thread.status;
    broadcastSoon();
    return;
  }
  if (method === 'turn/started') {
    const thread = ensureThread(params.threadId);
    if (thread) thread.status = 'inProgress';
    if (params.threadId === run?.rootThreadId) {
      run.turnId = params.turn?.id || run.turnId;
      run.status = 'inProgress';
      void persistRun();
    }
    if (params.turn?.id) addTurnFeed(params.threadId, params.turn, 'inProgress');
    broadcastSoon();
    return;
  }
  if (method === 'turn/completed') {
    const status = params.turn?.status || 'completed';
    const thread = ensureThread(params.threadId);
    if (thread) thread.status = status;
    if (params.threadId === run?.rootThreadId) {
      run.turnId = params.turn?.id || run.turnId;
      run.status = status;
      if (params.turn?.error?.message) error = params.turn.error.message;
      void persistRun();
      void refreshChildren();
    }
    if (params.turn?.id) addTurnFeed(params.threadId, params.turn, status);
    broadcastSoon();
    return;
  }
  if (method === 'item/started' || method === 'item/completed') {
    const atMs = method === 'item/started' ? params.startedAtMs : params.completedAtMs;
    const at = Number.isFinite(atMs) ? new Date(atMs).toISOString() : new Date().toISOString();
    upsertItem(params.threadId, params.item, at);
    return;
  }
  if (method === 'item/agentMessage/delta') {
    const thread = ensureThread(params.threadId);
    if (!thread || !params.itemId || typeof params.delta !== 'string') return;
    const existing = thread.items.find((item) => item.id === params.itemId);
    if (existing && existing.type === 'agentMessage') {
      existing.text = trimText(existing.text + params.delta);
      upsertFeed(params.threadId, existing, new Date().toISOString());
    } else {
      upsertItem(params.threadId, {
        type: 'agentMessage',
        id: params.itemId,
        text: params.delta,
        phase: null
      }, new Date().toISOString());
    }
    broadcastSoon();
    return;
  }
  if (method === 'warning' || method === 'configWarning') {
    console.error('Codex warning:', params.message || params.summary || method);
    return;
  }
  if (method === 'error') {
    error = params.error?.message || 'Codex reported an error.';
    broadcastSoon();
  }
}

const outputLines = createInterface({ input: codex.stdout });
outputLines.on('line', (line) => {
  try {
    handleRpcMessage(JSON.parse(line));
  } catch (cause) {
    console.error('Invalid app-server message:', cause.message);
  }
});
codex.stderr.setEncoding('utf8');
codex.stderr.on('data', (chunk) => {
  process.stderr.write(chunk);
});
codex.on('error', (cause) => {
  ready = false;
  error = '无法启动 Codex app-server：' + cause.message;
  for (const pending of pendingRpc.values()) {
    clearTimeout(pending.timer);
    pending.rejectPromise(cause);
  }
  pendingRpc.clear();
  broadcastSoon();
});
codex.on('exit', (code) => {
  if (closing) return;
  ready = false;
  error = 'Codex app-server 已退出（代码 ' + String(code) + '）。请重新启动面板。';
  for (const pending of pendingRpc.values()) {
    clearTimeout(pending.timer);
    pending.rejectPromise(new RpcError(error));
  }
  pendingRpc.clear();
  broadcastSoon();
});

async function readRawThread(threadId) {
  const response = await rpcRequest('thread/read', { threadId, includeTurns: true }, 45000);
  if (!response?.thread) throw new RpcError('Codex did not return a thread.');
  return response.thread;
}

async function fetchThread(threadId) {
  const raw = await readRawThread(threadId);
  hydrateFromThread(raw);
  return raw;
}

async function refreshChildren() {
  if (!ready || !run?.rootThreadId || childRefreshActive) return;
  childRefreshActive = true;
  try {
    const rootId = run.rootThreadId;
    const readIds = new Set();
    let cursor = null;
    for (let page = 0; page < 5; page++) {
      const result = await rpcRequest('thread/list', {
        ancestorThreadId: rootId,
        sourceKinds: childSourceKinds,
        cursor,
        limit: 50,
        sortDirection: 'asc'
      }, 25000);
      const found = Array.isArray(result?.data) ? result.data : [];
      for (const raw of found) {
        applyThreadMetadata(raw);
        if (raw.id) {
          readIds.add(raw.id);
          try {
            await fetchThread(raw.id);
          } catch (cause) {
            console.error('Could not read child thread:', cause.message);
          }
        }
      }
      cursor = result?.nextCursor || null;
      if (!cursor) break;
    }
    // A just-spawned child may not have reached the stored thread listing yet.
    for (const thread of threads.values()) {
      if (thread.parentThreadId && thread.id !== rootId && !readIds.has(thread.id)) {
        try {
          await fetchThread(thread.id);
        } catch {
          // The next event or scheduled refresh will retry.
        }
      }
    }
    broadcastSoon();
  } catch (cause) {
    console.error('Could not list subagents:', cause.message);
  } finally {
    childRefreshActive = false;
  }
}

async function attachThread(threadId, savedGoal = null) {
  const raw = await readRawThread(threadId);
  if (raw.parentThreadId) throw new RpcError('只能关联主 AI 的线程。', 400);
  if (!sameProjectCwd(raw.cwd)) throw new RpcError('该线程不属于当前 Unity 项目。', 400);
  const resumed = await rpcRequest('thread/resume', {
    threadId,
    cwd: projectDir,
    approvalPolicy: 'never',
    sandbox: 'read-only',
    excludeTurns: true
  }, 45000);
  threads.clear();
  feed.length = 0;
  run = {
    rootThreadId: threadId,
    goal: savedGoal || raw.preview || '',
    status: raw.status?.type === 'active' ? 'inProgress' : 'idle',
    turnId: null
  };
  const latestTurn = Array.isArray(raw.turns) ? raw.turns.at(-1) : null;
  if (latestTurn) {
    run.turnId = latestTurn.id;
    run.status = latestTurn.status || run.status;
  }
  hydrateFromThread(raw);
  applyThreadMetadata(resumed?.thread || raw);
  await persistRun();
  await refreshChildren();
  broadcastSoon();
  return run;
}

function buildTaskInput(goal) {
  return [
    '请执行一轮 Codex 原生多代理协作。用户给出的大方向：',
    goal,
    '',
    '你负责自主拆任务、给各子代理分别派工、等待回报、静态审查证据、处理冲突，再由你向用户汇报。',
    '按实际需要选择角色，不必把所有角色都叫来；核对已有验证证据，未运行的检查如实标为未验证。',
    '当前看板新建任务为只读调查。需要实现、Unity 编译或游戏功能检查的工作，应在本项目可写的 Codex 原生主任务中由 AI 自行完成和复验；用户可选择试玩。'
  ].join('\n');
}

async function startGoal(goal, mode) {
  if (!ready) throw new RpcError('Codex app-server 尚未就绪。');
  if (busy || run?.status === 'inProgress' || run?.status === 'starting') {
    throw new RpcError('主 AI 正在执行，请等本轮结束后再提交新方向。', 409);
  }
  busy = true;
  try {
    roles = await readRoles();
    error = null;
    if (mode === 'new') {
      const started = await rpcRequest('thread/start', {
        cwd: projectDir,
        approvalPolicy: 'never',
        sandbox: 'read-only',
        serviceName: 'mygame_ai_team_dashboard',
        developerInstructions: coordinatorInstructions
      }, 45000);
      const threadId = started?.thread?.id;
      if (!threadId) throw new RpcError('Codex did not return a root thread ID.');
      threads.clear();
      feed.length = 0;
      applyThreadMetadata(started.thread);
      run = { rootThreadId: threadId, goal, status: 'starting', turnId: null };
    } else if (mode === 'continue') {
      if (!run?.rootThreadId) throw new RpcError('尚未关联主 AI 线程，请先开始新任务。', 400);
      run.goal = goal;
      run.status = 'starting';
    } else {
      throw new RpcError('mode 只能是 new 或 continue。', 400);
    }
    await persistRun();
    broadcastSoon();
    const result = await rpcRequest('turn/start', {
      threadId: run.rootThreadId,
      input: [{ type: 'text', text: buildTaskInput(goal) }],
      cwd: projectDir,
      approvalPolicy: 'never',
      sandboxPolicy: { type: 'readOnly', networkAccess: true }
    }, 60000);
    run.turnId = result?.turn?.id || run.turnId;
    if (run.status === 'starting' || run.status === 'inProgress') {
      run.status = result?.turn?.status || 'inProgress';
    }
    await persistRun();
    broadcastSoon();
    return { ...run };
  } catch (cause) {
    if (run?.status === 'starting') run.status = 'failed';
    error = cause.message;
    broadcastSoon();
    throw cause;
  } finally {
    busy = false;
  }
}

async function interruptRun() {
  if (!ready || !run?.rootThreadId || !run?.turnId) {
    throw new RpcError('当前没有可中断的主 AI 任务。', 400);
  }
  if (run.status !== 'inProgress') {
    throw new RpcError('当前任务不在执行中。', 409);
  }
  await rpcRequest('turn/interrupt', {
    threadId: run.rootThreadId,
    turnId: run.turnId
  }, 30000);
  return { accepted: true };
}

class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

function headers(extra = {}) {
  return {
    'Cache-Control': 'no-store',
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer',
    'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; base-uri 'none'; object-src 'none'; frame-ancestors 'none'",
    ...extra
  };
}

function sendJson(response, status, body) {
  response.writeHead(status, headers({ 'Content-Type': 'application/json; charset=utf-8' }));
  response.end(JSON.stringify(body));
}

function verifyHost(request) {
  const host = request.headers.host;
  if (host !== '127.0.0.1:' + port && host !== 'localhost:' + port) {
    throw new HttpError(403, '只接受本机访问。');
  }
}

function verifyPost(request) {
  const origin = request.headers.origin;
  if (origin !== 'http://127.0.0.1:' + port && origin !== 'http://localhost:' + port) {
    throw new HttpError(403, '请求来源不受信任。');
  }
  const contentType = request.headers['content-type'] || '';
  if (!/^application\/json(?:\s*;|$)/i.test(contentType)) {
    throw new HttpError(415, '请求必须使用 application/json。');
  }
  const length = Number(request.headers['content-length'] || 0);
  if (length > maxBodyBytes) throw new HttpError(413, '请求内容太长。');
}

async function readJsonBody(request) {
  let size = 0;
  const chunks = [];
  for await (const chunk of request) {
    size += chunk.length;
    if (size > maxBodyBytes) throw new HttpError(413, '请求内容太长。');
    chunks.push(chunk);
  }
  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } catch {
    throw new HttpError(400, 'JSON 格式无效。');
  }
}

function verifyGoal(body) {
  if (!body || typeof body.goal !== 'string') {
    throw new HttpError(400, '缺少目标 goal。');
  }
  const goal = body.goal.trim();
  if (goal.length < 1 || goal.length > 4000) {
    throw new HttpError(400, '目标须为 1 至 4000 个字符。');
  }
  return goal;
}

async function handleHttp(request, response) {
  try {
    verifyHost(request);
    const url = new URL(request.url || '/', 'http://127.0.0.1:' + port);
    const pathname = url.pathname;
    if (request.method === 'GET' && pathname === '/api/state') {
      sendJson(response, 200, snapshot());
      return;
    }
    if (request.method === 'GET' && pathname === '/api/events') {
      response.writeHead(200, headers({
        'Content-Type': 'text/event-stream; charset=utf-8',
        'Cache-Control': 'no-cache, no-transform',
        Connection: 'keep-alive'
      }));
      sseClients.add(response);
      writeSse(response, snapshot());
      request.on('close', () => sseClients.delete(response));
      return;
    }
    if (request.method === 'GET' && staticFiles.has(pathname)) {
      const [filename, contentType] = staticFiles.get(pathname);
      const content = await readFile(join(dashboardDir, filename));
      response.writeHead(200, headers({ 'Content-Type': contentType }));
      response.end(content);
      return;
    }
    if (request.method !== 'POST') {
      throw new HttpError(404, '页面不存在。');
    }
    verifyPost(request);
    const body = await readJsonBody(request);
    if (pathname === '/api/goals') {
      const goal = verifyGoal(body);
      const mode = body.mode || 'new';
      if (mode !== 'new' && mode !== 'continue') throw new HttpError(400, 'mode 无效。');
      const current = await startGoal(goal, mode);
      sendJson(response, 200, { ok: true, run: current });
      return;
    }
    if (pathname === '/api/attach') {
      if (busy || run?.status === 'inProgress' || run?.status === 'starting') {
        throw new HttpError(409, '当前任务正在执行。');
      }
      if (typeof body?.threadId !== 'string' || !/^[a-zA-Z0-9_-]{8,100}$/.test(body.threadId)) {
        throw new HttpError(400, 'threadId 无效。');
      }
      const current = await attachThread(body.threadId);
      sendJson(response, 200, { ok: true, run: current });
      return;
    }
    if (pathname === '/api/interrupt') {
      const result = await interruptRun();
      sendJson(response, 200, result);
      return;
    }
    throw new HttpError(404, '接口不存在。');
  } catch (cause) {
    const finalStatus = cause.status || (cause.code === 409 ? 409 : cause.code === 400 ? 400 : 500);
    if (!response.headersSent) {
      sendJson(response, finalStatus, { error: cause.message || '请求失败。' });
    } else {
      response.end();
    }
    if (finalStatus >= 500) console.error('Dashboard request failed:', cause);
  }
}

const server = createServer((request, response) => {
  void handleHttp(request, response);
});
server.listen(port, '127.0.0.1', () => {
  console.log('AI Team Dashboard: http://127.0.0.1:' + port);
  console.log('Project: ' + projectDir);
});

const heartbeat = setInterval(() => {
  for (const client of sseClients) {
    try {
      client.write(': ping\n\n');
    } catch {
      sseClients.delete(client);
    }
  }
}, 20000);

async function initializeCodex() {
  try {
    roles = await readRoles();
    await rpcRequest('initialize', {
      clientInfo: {
        name: 'mygame_ai_team_dashboard',
        title: 'MyGame AI Team Dashboard',
        version: '0.1.0'
      },
      capabilities: {
        experimentalApi: true,
        requestAttestation: false
      }
    }, 45000);
    rpcNotify('initialized', {});
    ready = true;
    error = null;
    broadcastSoon();
    busy = true;
    try {
      const saved = JSON.parse(await readFile(stateFile, 'utf8'));
      if (typeof saved?.rootThreadId === 'string') {
        await attachThread(saved.rootThreadId, saved.goal);
      }
    } catch (cause) {
      if (cause.code !== 'ENOENT') {
        console.error('Could not restore previous dashboard thread:', cause.message);
      }
    } finally {
      busy = false;
    }
    childRefreshTimer = setInterval(() => {
      if (run?.status === 'inProgress') void refreshChildren();
    }, 4500);
  } catch (cause) {
    ready = false;
    error = 'Codex 初始化失败：' + cause.message;
    console.error(error);
    broadcastSoon();
  }
}
void initializeCodex();

function shutdown() {
  if (closing) return;
  closing = true;
  clearInterval(heartbeat);
  if (childRefreshTimer) clearInterval(childRefreshTimer);
  if (broadcastTimer) clearTimeout(broadcastTimer);
  for (const pending of pendingRpc.values()) {
    clearTimeout(pending.timer);
    pending.rejectPromise(new RpcError('Dashboard is shutting down.'));
  }
  pendingRpc.clear();
  for (const client of sseClients) client.end();
  sseClients.clear();
  server.close();
  codex.stdin.end();
  setTimeout(() => codex.kill(), 1000).unref();
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
